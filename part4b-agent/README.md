# Part 4B — Santa's Elf Assistant: Agents

> **Level:** Intermediate. Part 4A helps (this part reuses its handbook search), but you don't need to have done it: the retriever is already written for you.
> **Time:** About two hours for the core path (an estimate: nobody has timed it yet).
> **Badge:** AI Agents with MongoDB

> ⚠️ **Heads up: two things in this part fail quietly.** A loop that calls a model and runs its tools **has no natural end**: without a limit, a confused
> model can keep going and keep billing ([Step 5](#step-5--write-the-cycle-yourself--objectives-4-and-5)). And **trimming a conversation in the wrong place** leaves
> a tool's result with no call before it, which most model APIs reject, on the *next* turn, far from the code that caused it ([Step 8](#step-8--trim-without-breaking-a-turn--objective-6)).

---

## The story

Last week the elves' assistant learned to read the handbook. They love it. Then the questions got harder.

*"Where is ORD-1001?"* an elf asks. The assistant has read every page of the handbook, and none of them says. It can only **read**. It can't look anything up.

*"What can I give a four-year-old who likes puzzles, that we actually have in stock?"* It can't see the shelves.

So the elf tries to help it along. *"Where is ORD-1001?"* Then, when it finally gets an answer from the order book: *"And who is it for?"*

*"Which order do you mean?"* says the assistant.

Mrs Claus puts her tea down. "It reads, but it has no hands," she says, "and it has no memory. It's a very good library. We need an **assistant**: something that can *decide* it needs the order book, go and look, come back, and
remember what we were talking about."

So today you give it hands (**tools**), you let it **decide** when to use them, and you give it a **memory** that lives in MongoDB.

---

## Get the code

Part 4B lives in the same repository as the other parts:

**[github.com/LuceCarter/12days-of-mongodbmas-dotnet-workshop](https://github.com/LuceCarter/12days-of-mongodbmas-dotnet-workshop)**

If you already have it, skip to the next section. If not, get it either way:

- **Codespaces (easiest).** On the repository page choose **Code → Codespaces → Create
  codespace on main**. Everything is installed and the terminal opens in the right folder.
- **On your machine.** You'll need the [.NET 10 SDK](https://dotnet.microsoft.com/download)
  and [Docker](https://www.docker.com/products/docker-desktop/). Then clone it:

```bash
git clone https://github.com/LuceCarter/12days-of-mongodbmas-dotnet-workshop.git
cd 12days-of-mongodbmas-dotnet-workshop
```

**Run every command in this challenge from that folder**: the one containing
`docker-compose.yml`. The files you'll work with:

```
part4b-agent/
  README.md                          this page
  Starter/ElfAgentInstructions.cs    ← you write code here (Step 1)
  Starter/ElfTools.cs                ← (Steps 2, 3 and 4)
  Starter/ToolLoop.cs                ← (Step 5)
  Starter/ElfAgent.cs                ← (Step 6)
  Starter/ConversationMemory.cs      ← (Steps 7, 8 and 9)
  Starter/HandbookSearch.cs          already written: Part 4A's retriever, as a tool
  Starter/VoyageEmbedder.cs          already written (you wrote it in Part 3B)
  Starter/ElfLlm.cs                  already written: the language model
  Tests/                             the tests that tell you when a step is right
  SOLUTIONS.md                       every answer, with the reasoning
```

---

## Before you start

Make sure your environment is ready. From the repository folder:

```bash
docker compose up -d
dotnet run --project tools/doctor
```

This part needs **Atlas Vector Search** (for the handbook tool), which a plain MongoDB container doesn't have. The Atlas Local container in our `docker-compose.yml`
does, and the doctor checks that it's working. Full instructions are in [docs/setup.md](../docs/setup.md).

**You don't need an API key.** The handbook's chunks and their vectors are shipped in the repository, and the tests use a **scripted stand-in for the language model**: it says
exactly what the test tells it to, so you can check precisely what your code does with it. Nothing here calls the internet.

(If you *do* want to chat with a real agent at the end, [the Level up](#-level-up) shows how. Keys go in environment variables or user secrets and never in code: **never commit a key.**)

You'll want something to explore in the shell and Compass. Load the toys, the orders and the handbook:

```bash
dotnet run --project tools/seed -- --handbook
```

It's safe to run again whenever you like. The **tests don't need it**: they use their own database, `santas_workshop_part4b_tests`, so running them never overwrites the data you're exploring.

---

## How this works

Everything you write goes in the files under [`part4b-agent/Starter/`](Starter/). There are nine steps, and each is a `// TODO` and a `throw new NotImplementedException` in one of them. Run
the tests whenever you like:

```bash
dotnet test part4b-agent/Tests
```

They start **red**. Each step turns some of them green. When all **76** pass, you're done. (Six tests, the ones that begin *Already written*, check code that is already done for you, so they're green from the start.)

Stuck? [`SOLUTIONS.md`](SOLUTIONS.md) has every answer with an explanation. Looking isn't
cheating: reading a worked example is how most of us learn this.

---

## What is an agent?

In Part 4A, **you** decided what the program did: take the question, search the handbook, build a prompt, ask the model. The model only ever wrote the last sentence. An **agent** turns that around: **the model decides what to do next.**

An agent has three parts, and they are the three things the badge asks you to define:

| Part | What it is | In this part |
|---|---|---|
| **Reasoning** | a language model that decides: answer now, ask a question, or use a tool? | the model, steered by instructions you write (Step 1) |
| **Tools** | things it can *do* beyond writing text: look something up, calculate, change something | `find_toys`, `get_order_status`, `search_elf_handbook` (Steps 2 to 4 and 6) |
| **Memory** | what it remembers: this conversation (short-term), and what's worth keeping across them (long-term) | the conversation, in MongoDB (Steps 7 to 9) |

### RAG, and an agent

| | **RAG** (Part 4A) | **An agent** (this part) |
|---|---|---|
| **Who decides what happens** | your code: the same steps every time | the model, each time |
| **How many times it calls the model** | once | as many as it takes: one per trip round the cycle |
| **Where the data comes from** | one place, searched every time | whichever tool it chooses |
| **It's good for** | questions with one source of truth | questions that need *different* sources, or one answer to feed the next question |
| **Cost and predictability** | cheap, predictable | slower, costs more, and can surprise you |

That last row is worth taking seriously. **An agent is not an upgrade.** If every question needs the same single lookup, RAG is simpler, cheaper and easier to test, and the model can't wander. Reach for an agent when the *steps* depend on what is found: when you can't know in advance which source the answer is in, or when the answer to one lookup decides the next.

### The cycle

What makes an agent different is a **loop**:

```
            ┌────────────── results go back ──────────────┐
            ▼                                              │
   start ─► MODEL ── asked for tools? ── yes ──► TOOLS ────┘
              │
              no
              ▼
            answer ─► end
```

The model is asked. If it replies with a **tool call** ("run `get_order_status` with `ORD-1001`"), your code runs the tool and sends the result **back to the model**, which decides again. Round and round, until it replies with
an answer instead of a request. Draw it as a graph and it has two **nodes** (*model*, *tools*) and an edge that points **backwards**: that back-edge is what "cyclical" means, and it is why an agent can use what it finds to decide what to do next.

> 🧠 **If you know LangGraph**, the badge may describe this the way LangGraph draws it: an *agent* node, a *tools* node, and a *conditional edge* out of the agent that goes to the tools node when the last message contains tool calls and to the end when it doesn't. That is exactly the diagram above. Microsoft Agent Framework
> doesn't ask you to draw the graph: its `ChatClientAgent` runs this loop for you, as plain code. In Step 5 you write the loop yourself once; in Step 6 you hand it to the framework. Same cycle, different vocabulary.

### Planning an agent: tasks first, then data

Before any code, an agent is planned by asking, of each thing a user might want: **what is the task, and where does the data for it live?**

| An elf asks… | The task | The data | So the agent needs |
|---|---|---|---|
| "What can I give a four-year-old who loves puzzles?" | find toys by age and category | the `toys` collection | `find_toys` |
| "Where is ORD-1001?" | look up one order | the `orders` collection | `get_order_status` |
| "How do I check a part is too small?" | find the rule | the Elf Handbook (as vectors) | `search_elf_handbook` |
| "And is it still open?" (a follow-up) | remember what "it" means | the conversation | memory |

Look at *how* each source is reached. Facts that change by the hour (stock, order status) are **queried live**, with a tool. Text that is about meaning and changes slowly (the handbook) is **searched by vector**. That is the lesson of Part 4A's Step 2 again: **embed what is about meaning; query what is about facts.**

### Memory: short-term and long-term

| | **Short-term memory** | **Long-term memory** |
|---|---|---|
| **What it holds** | this conversation's messages: what was said, and every tool call and result | facts and preferences worth keeping *across* conversations: "Ingrid prefers wooden toys" |
| **How it's used** | **replayed** to the model every turn, in order | **searched**: the few relevant items go into the prompt (it is RAG over what the agent has learned) |
| **How it grows** | with every message, so it **must be trimmed** | by choosing what is worth keeping, so it needs **distilling** |
| **How long it lives** | a conversation, so it should **expire** | until it's wrong, or someone asks for it to go |
| **In this part** | Steps 7 to 9, built and tested | explained here and in [SOLUTIONS.md](SOLUTIONS.md), **not built** |

You build the first. For the second, a sketch: after a conversation, something extracts what is worth keeping ("Ingrid prefers wooden toys"), stores each fact **with an embedding**, and at the start of a later conversation *searches* those facts with the new question. It is Part 4A's pipeline,
pointed at what the agent has *learned* instead of at a handbook. The hard parts are the choices: **what to keep** (not everything), **when to forget**, and **who may see it**. A fact about a child is personal data.

---

## The data

The agent's tools use the data you already know:

- the **toy catalogue** (`toys`, from Part 1): 34 toys, with age ranges, categories and stock;
- the **order book** (`orders`, from Part 2): 46 orders, each with a number like `ORD-1001`, a child, a status (`open`, `packed` or `delivered`) and lines of toy names and quantities;
- the **Elf Handbook**, as the chunks and vectors Part 4A made.

---

## Part A — Plan

### Step 1 — Write the agent's instructions · *objectives 1 and 2*

The model can't see your code. Everything it knows about its job is **the instructions** and **the tool definitions**. Open [`Starter/ElfAgentInstructions.cs`](Starter/ElfAgentInstructions.cs) and implement `Build`: the text that says
who the agent is, which tool is for what, and how to behave.

Three rules do most of the work: *look things up rather than answer from memory*; *if the elf hasn't given an order number, **ask**: never guess one*; and *if a tool finds nothing, **say so***. Without the second, an agent asked "where's my order?" will often invent a
plausible order number and call the tool with it.

The instructions must name **only the tools the agent really has**. An agent promised a tool it doesn't have will try to call it.

---

## Part B — Tools

A **tool** is just a method, and what makes it a tool is that a model can **ask for it by name, with arguments it chooses, and get the result back**. That is **function calling**.

The model never runs your code. The exchange is: you send it the conversation **and a list of tool definitions** (name, description, JSON schema of the parameters); it replies, instead of text, with *"call `get_order_status` with `{ orderNumber: "ORD-1001" }`"*; **your code** runs it and sends the result back; the
model uses the result.

### Step 2 — A tool that finds toys · *objective 4*

Implement `ElfTools.FindToysAsync` in [`Starter/ElfTools.cs`](Starter/ElfTools.cs): filter the catalogue by category, age and stock. This is Part 1's `Find`, `Builders`, sort and limit, now **behind a tool**.

Two things change when the caller is a model. **Its arguments are untrusted**: it may send `"Puzzle"` for `"puzzle"`, or a limit of 0, or three of the four arguments. A tool has to cope. And **what you return is read by a model**: return what a recommendation needs and nothing more.

The `[Description]` attributes on the method are part of the tool: they are sent to the model, and they are all it has to decide whether and how to call it.

### Step 3 — A tool that looks up an order · *objective 4*

Implement `ElfTools.GetOrderStatusAsync`. Capitals and stray spaces don't matter (`" ord-1001 "` is `ORD-1001`), and an order that doesn't exist comes back as a **result** (`Found: false`), **not an exception**. A model can read a result and say "I can't find that: could you check the number?". It can't read an exception.

### Step 4 — Offer them to a model · *objective 4*

Implement `ElfTools.CreateTools`: turn the two methods into `AIFunction`s with `AIFunctionFactory.Create`. It reads each method's parameters and `[Description]`s and builds the JSON schema a model is shown. For `find_toys` **none** of the four parameters is required (each has a default), so a model can call it with no arguments at
all; for `get_order_status`, `orderNumber` **is**. Whether a parameter is optional is something the schema tells the model, so it matters that it's right.

These types come from `Microsoft.Extensions.AI`, not from Agent Framework: the same tools work with **any** `IChatClient`, and with the loop you're about to write.

---

## Part C — The cycle

### Step 5 — Write the cycle yourself · *objectives 4 and 5*

Implement `ToolLoop.RunAsync` in [`Starter/ToolLoop.cs`](Starter/ToolLoop.cs): ask the model; if it wants tools, run them and ask again with the results; repeat until it answers. It's about twenty lines, and it is the whole of "cyclical graph".

Four details are real engineering:

- **Every tool call gets a result with the same `CallId`.** The model matches results to requests by id, and several calls can arrive in one reply: run them all, and answer them together in one `tool` message.
- **Unknown tools and failing tools both become results**, so the model can recover or explain. A loop that throws on the first hiccup is an agent that falls over.
- **The turn limit is not optional.** A model can keep asking for tools for ever. Every turn costs time and money, so a cycle needs a cap, and an answer on the last allowed turn is still an answer.
- The method adds to the list you give it, so a caller keeping a conversation has all of it.

### Step 6 — Hand it to the framework · *objectives 3 and 1*

Implement `ElfAgent.CreateAgent` in [`Starter/ElfAgent.cs`](Starter/ElfAgent.cs): build a `ChatClientAgent` with your instructions and tools, and the handbook as a **retrieval tool**. `ChatClientAgent` runs the cycle you just wrote, and adds a **session** that holds the
conversation's state, and **providers** that plug in around each call.

The handbook goes in through a `TextSearchProvider`, which has two modes. This setting is the difference between Part 4A and this part:

| `SearchTime` | What happens | It is |
|---|---|---|
| `BeforeAIInvoke` | the search runs on **every** message and the results go in the prompt | RAG, as in Part 4A |
| `OnDemandFunctionCalling` | the search is offered as a **tool**; the model decides whether to call it | a **retrieval tool**, which is an agent |

Ask this agent where ORD-1001 is, and the handbook is **never searched**: the model knows that's a job for `get_order_status`. A RAG pipeline would have searched the handbook for it, and found nothing useful.

---

## Part D — Memory

### Step 7 — Remember a conversation · *objective 7*

Implement `MongoChatStore` in [`Starter/ConversationMemory.cs`](Starter/ConversationMemory.cs): `AppendAsync` stores messages at the end of a conversation, `LoadAsync` reads one back in order, and `EnsureIndexesAsync` creates the indexes.

Three decisions in it are worth understanding:

- **One document per message**, not one per conversation. A conversation document would grow without limit and be rewritten on every message. A document per message makes appending one insert, and an index on `(conversationId, sequence)` makes reading a conversation in order an index scan.
- **The whole message is kept as JSON**, because a message is not just text: it can hold a tool call with its arguments, or a tool's result. Store only the text and the next turn would show the model a tool result with no call before it.
- **A unique index and a TTL index.** The unique one means a message can't be stored twice at the same position. The **TTL** index makes MongoDB delete documents for you once they are older than the limit (it checks about once a minute, so not to the second). Short-term memory that never expires is just a log that grows for ever.

### Step 8 — Trim without breaking a turn · *objective 6*

A model's context window is finite and every message costs tokens, so short-term memory needs a limit. Implement `ChatWindow.TrimToRecent`: keep the most recent messages, but **only ever start at a user message**.

Why not just take the last N? Because one *turn* is a user message, an assistant message asking for a tool, the tool's result, and the answer, and the call and its result **belong together**. Cut between them and most model APIs will reject the conversation as malformed, on the next turn, far from the code that caused it. So the
window may keep slightly fewer than you asked for, **never more**, and if no whole turn fits it keeps nothing rather than half of one.

What the window forgets is worth saying aloud: an elf who gave an order number twenty turns ago will be asked for it again. Fixes include a bigger window, **summarising** old messages, or moving the important facts into long-term memory. Each trades tokens, fidelity and complexity.

### Step 9 — Plug it into the agent · *objective 7*

Implement `RecallAsync` and `RememberAsync` on `MongoChatHistoryProvider`. A `ChatHistoryProvider` is asked two questions on every run: **before** it, "what has been said so far?", and **after** it, "here is what happened: remember it". The "after" gives you the new user message **and everything the agent produced**: its tool call, the
tool's result and its answer. That's why the memory works with tools: the next turn can show the model that it already looked the order up.

**Which conversation is it?** The conversation id lives **in the session**, not in the provider. That is deliberate: one provider serves many sessions, and a session can be **serialised and restored**. After a restart, a new copy of the program restores the saved session, the provider sees the same conversation id, and **picks up from MongoDB where the last one stopped**.
The test for this builds new objects for everything except the saved session.

A run that **fails remembers nothing** (that part is already written): a half-finished turn would otherwise be replayed next time, and would break it.

---

## ✅ You're done

```bash
dotnet test part4b-agent/Tests
```

All **76** green.

Now try it for real, if you have a key: [`tools/elf-chat`](#-level-up) runs **your** agent. Ask *"Where is ORD-1001?"*, then *"Who is it for?"*, then type `/quit` and run it again with the same conversation. The assistant that didn't know which order you meant now picks the conversation up where you left off, because it lives in MongoDB and not in the program.

---

## Badge checklist — AI Agents with MongoDB

Every objective the badge assesses, and the step that teaches it.

| ✓ | Objective | Step | Taught by |
|---|---|---|---|
| ☐ | **1** Define an agent's reasoning, tools and memory | Steps 1, 6, and *What is an agent?* | `Build`, `CreateAgent` |
| ☐ | **2** Plan an agent's tasks and required data | Step 1, and *Planning an agent* | `Build` |
| ☐ | **3** Retrieval tools | Step 6 | `CreateAgent` |
| ☐ | **4** Tools in general, and LLM function calling | Steps 2, 3, 4, 5 | `FindToysAsync`, `GetOrderStatusAsync`, `CreateTools`, `RunAsync` |
| ☐ | **5** Cyclical graphs | Step 5, and *The cycle* | `RunAsync` |
| ☐ | **6** Short- vs. long-term memory | Step 8, and *Memory* | `TrimToRecent` |
| ☐ | **7** Implement short-term agent memory with MongoDB | Steps 7, 9 | `AppendAsync`, `LoadAsync`, `EnsureIndexesAsync`, `RecallAsync`, `RememberAsync` |
| ☐ ⚠️ | **5** …and know **the turn limit**: a cycle with no cap is a bill you didn't agree to | Step 5 | `RunAsync` |
| ☐ ⚠️ | **6** …and know **where a window may start**: at a user message, or a tool result is orphaned | Step 8 | `TrimToRecent` |

🖐️ **Objectives 1, 2 and 6 include definitions.** The tests prove the instructions name the tools and the rules, that the plan's tools exist and work, and that the window never breaks a turn. But *defining* an agent, planning one, and telling short-term memory from long-term (which this part explains and does not build) is something you tick yourself, in your own words.

**Do you have to finish the exercises to take the badge?** No: the badge is open and you can
take it whenever you like. But it assesses these concepts, so doing the steps first is the
easier route.

---

## 📋 From C# to the badge

The assessment is likely to show Python, LangGraph or shell syntax rather than C#. The ideas, and the MongoDB parts, are the same. The full set lives in
[docs/csharp-to-badge.md](../docs/csharp-to-badge.md).

| What you want | Elsewhere | C# |
|---|---|---|
| Define a tool | a function with a docstring and typed arguments (a LangChain `@tool`) | a method with `[Description]`s, wrapped by `AIFunctionFactory.Create` |
| The agent loop | a graph: an *agent* node, a *tools* node, a conditional edge, and an edge back | `ChatClientAgent` (or the `for` loop in `ToolLoop`) |
| A tool call | a message with `tool_calls: [{ id, name, args }]` | `FunctionCallContent(callId, name, arguments)` |
| A tool result | a message with role `tool` and the call's `tool_call_id` | `FunctionResultContent(callId, result)` |
| A retrieval tool | a retriever exposed as a tool | `TextSearchProvider` with `SearchTime = OnDemandFunctionCalling` |
| Short-term memory | a checkpointer / the message history | `ChatHistoryProvider` |
| Store messages in MongoDB | `insertOne({ conversationId, sequence, role, message })` | `InsertManyAsync` of `StoredMessage` |
| Read a conversation in order | `find({ conversationId }).sort({ sequence: 1 })` | `Find(...).SortBy(m => m.Sequence)` |
| Expire old conversations | `createIndex({ createdAt: 1 }, { expireAfterSeconds: 86400 })` | `CreateIndexOptions { ExpireAfter = ... }` |

---

## 🚀 Level up

Optional. None of this is assessed by the badge.

### Chat with a real agent

To see it all work for real you need **a language model** and **an embedding model** (for the handbook search). Put keys in environment variables, or in .NET user secrets. **Never commit a key.**

```bash
dotnet run --project tools/seed -- --handbook

export GEMINI_API_KEY="your-gemini-key"             # a free key from https://aistudio.google.com/apikey
export VOYAGE_API_KEY="your-voyage-key"
export VOYAGE_BASE_URL="https://ai.mongodb.com"     # only if you made the Voyage key in MongoDB Atlas

dotnet run --project tools/elf-chat
```

No Gemini key? If your organisation blocks creating one, use **Google Cloud** instead (`gcloud auth login`, then `export GOOGLE_CLOUD_PROJECT=...`); [docs/setup.md](../docs/setup.md) explains it. It's the same agent: only `ElfLlm` knows the difference.

`tools/elf-chat` runs **your** instructions, tools, agent and memory, and shows each tool the agent chooses to use as it goes (🔧). Ask *"Where is ORD-1001?"*, then *"Who is it for?"*. Type `/quit`, run it again with the same `--conversation`, and ask *"What did I just ask you about?"*

### Add a tool

Give the agent a third tool, `get_order_total`, that uses Part 2's `$lookup` to price an order from the catalogue. Add it to `CreateTools` and to the instructions. Notice how little else changes: that is what tools buy you.

### Add a tool that changes something

Everything here only *reads*. What if there were a `cancel_order` tool? A tool that **changes** things needs a person in the loop: the model proposes, a human approves. Agent Framework has settings for tool-approval flows, which this part doesn't use and which we haven't tried. Where would you put the check, and what should the tool refuse to do even if approved?

### Swap the model

`ElfLlm` is the only place that names Gemini. Point it at another provider's OpenAI-compatible endpoint with `GEMINI_BASE_URL` and `GEMINI_MODEL`, and nothing else changes. **Does the agent still choose the right tools?** Not every model is as good at it, and now you can test that.

### Break the instructions

Delete the "ask, never guess" rule from your instructions and ask a real model *"Where is my order?"*. What does it do? Then put the rule back.

### Build long-term memory

Add a `facts` collection. After each conversation, ask the model to extract anything worth remembering and store each fact **with an embedding**. At the start of the next conversation, search the facts with the first question and put the best few in the prompt. (It's Part 4A again.) What should the agent **never** remember about a child?

---

## What's next

That's the series. Over twelve days you've gone from `InsertOne` to an agent that decides for itself which of three data sources to use, and remembers the conversation in MongoDB. The same pieces, a collection, an index, a pipeline, a vector search, are what the real thing is built from. Take the badges, and have a wonderful December.
