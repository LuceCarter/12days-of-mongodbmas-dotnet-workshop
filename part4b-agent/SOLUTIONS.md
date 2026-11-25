# Part 4B — Solutions

Every answer, with the reasoning. **Using this isn't cheating**: reading a worked example
is how most people learn a new technique. Try a step first, then come here.

Each solution below is exactly what the tests check.

<details>
<summary><b>Step 1 — Build</b> (an agent's reasoning, tools and memory; planning its tasks and data)</summary>

```csharp
public static string Build(IEnumerable<string> toolNames)
{
    var tools = toolNames.ToList();

    // What each tool is for, in the elves' terms. Only the tools the agent really has are described.
    var uses = new Dictionary<string, string>
    {
        [FindToysTool] = "Use it for questions about which toys exist, what suits a child of a given age, or what is in stock.",
        [OrderStatusTool] = "Use it when an elf asks about an order: who it is for, whether it is open, packed or delivered, and what is on it.",
        [HandbookTool] = "Use it for the workshop's rules and procedures: toy safety, wrapping, the sleigh, the reindeer, emergencies and so on.",
    };

    var toolLines = string.Join("\n", tools.Select(t => $"- {t}: {(uses.TryGetValue(t, out var use) ? use : "Use it when it helps.")}"));

    return $"""
        You are the Elf Assistant at Santa's Workshop. You help the elves with their questions about toys, orders and the workshop's rules.

        You have these tools:
        {toolLines}

        How to behave:
        - Look things up with a tool instead of answering from memory. Never make up stock levels, order details or handbook rules.
        - If an elf asks about an order but has not given an order number, ask for it. Never guess one.
        - If a tool finds nothing, say so plainly. Do not fill the gap with a guess.
        - You can see the earlier messages of this conversation. Do not ask for something you have already been told.
        """;
}
```

**An agent is a model, given tools, memory and a plan.** The three pieces the badge names:

| Piece | In this part | What it is |
|---|---|---|
| **Reasoning** | the language model, steered by these instructions | decides what to do next: answer, ask, or use a tool |
| **Tools** | `find_toys`, `get_order_status`, `search_elf_handbook` | what it can *do* beyond writing text |
| **Memory** | the conversation, kept in MongoDB (Steps 7–9) | what it remembers from earlier in the conversation |

**Planning is deciding the tasks and the data before you write code.** The question to ask of each thing an elf might want is: *what is the
task, and where does the data for it live?*

| An elf asks… | The task | The data | So the agent needs |
|---|---|---|---|
| "What can I give a four-year-old who loves puzzles?" | find toys by age and category | the `toys` collection | `find_toys` |
| "Where is ORD-1001?" | look up one order | the `orders` collection | `get_order_status` |
| "How do I check a part is too small?" | find the rule | the Elf Handbook (vectors) | `search_elf_handbook` |
| "And is it still open?" (a follow-up) | remember what "it" is | the conversation | memory |

Notice that the table decides *which sources need which treatment*. Facts that change by the hour (stock, order status) are **queried live** with a tool.
Text that is about meaning and changes slowly (the handbook) is **searched** by vector. Part 4A's Step 2 made this exact point about embedding versus querying.

**The instructions are doing real work.** The model can't see your code. Everything it knows about its job is this text and the tool definitions. Three of the rules matter most:

- *Look it up, don't answer from memory*: this is Part 4A's lesson again, now as a rule for an agent.
- *Ask rather than guess*: without it, an agent asked "where's my order?" will often invent a plausible order number and call the tool with it.
- *Say so when a tool finds nothing*: otherwise "not found" becomes a confident guess.

The function names the instructions use come from constants (`FindToysTool` and the others), so the instructions and the tools can't drift apart: the tests check that
every tool the agent has is named, and that a tool it *doesn't* have isn't promised.
</details>

<details>
<summary><b>Step 2 — FindToysAsync</b> (tools)</summary>

```csharp
public async Task<List<ToyInfo>> FindToysAsync(
    [Description("Only toys of this category, such as 'puzzle', 'board game', 'soft toy', 'wooden', 'electronic', 'craft' or 'outdoor'. Leave out for any category.")] string? category = null,
    [Description("The child's age in years. Only toys that suit a child of exactly this age are returned. Leave out to ignore age.")] int? age = null,
    [Description("True (the default) returns only toys that are in stock and not discontinued. False includes the rest.")] bool inStockOnly = true,
    [Description("The most toys to return.")] int limit = 5)
{
    if (limit < 1) return [];

    var filters = new List<FilterDefinition<Toy>>();

    // The catalogue stores categories in lower case, and a model will happily write "Puzzle".
    if (!string.IsNullOrWhiteSpace(category))
        filters.Add(Builders<Toy>.Filter.Eq(t => t.Category, category.Trim().ToLowerInvariant()));

    if (age is not null)
    {
        filters.Add(Builders<Toy>.Filter.Lte(t => t.MinAge, age.Value));
        filters.Add(Builders<Toy>.Filter.Gte(t => t.MaxAge, age.Value));
    }

    if (inStockOnly)
    {
        filters.Add(Builders<Toy>.Filter.Eq(t => t.InStock, true));
        filters.Add(Builders<Toy>.Filter.Eq(t => t.Discontinued, false));
    }

    var filter = filters.Count == 0 ? FilterDefinition<Toy>.Empty : Builders<Toy>.Filter.And(filters);

    var found = await _toys.Find(filter).SortBy(t => t.Name).Limit(limit).ToListAsync();

    return found.Select(t => new ToyInfo(t.Name, t.Category, t.MinAge, t.MaxAge, t.Price, t.InStock)).ToList();
}
```

This is Part 1's `Find`, `Builders` and `Sort` and `Limit`, in a new place: **behind a tool, called by a model**. Two things change when the caller is a model rather than a person:

- **The arguments are untrusted.** A model may send `"Puzzle"` for `"puzzle"`, a `limit` of 0, or one of three arguments and not the others. A tool has to cope with the odd, so every argument is optional, case doesn't matter, and a nonsense limit returns nothing instead of throwing.
- **What you return is read by a model, not rendered for a person.** `ToyInfo` carries what a recommendation needs (name, category, ages, price, whether it is in stock) and nothing it doesn't. Returning whole documents costs tokens and can hand a model fields (an internal id, a vector) it will only get confused by.

**The `[Description]` attributes are part of the tool.** They are sent to the model in the tool's definition, and they are all it has to decide whether and how to call. "Only toys that suit a child of exactly this age" is the difference between a model passing `age: 4` and passing `age: "4-year-old"`.

**Sorting by `Name`** isn't there because anyone cares about alphabetical toys. It makes the result **deterministic**: the same call gives the same answer, which is what you want from a tool you are going to test and reason about.
</details>

<details>
<summary><b>Step 3 — GetOrderStatusAsync</b> (tools)</summary>

```csharp
public async Task<OrderStatusResult> GetOrderStatusAsync(
    [Description("The order number, such as 'ORD-1001'. Capital letters don't matter.")] string orderNumber)
{
    // "ord-1001 " is what a model, or a person, will actually type.
    var wanted = orderNumber.Trim().ToUpperInvariant();

    var order = await _orders.Find(o => o.OrderNumber == wanted).FirstOrDefaultAsync();

    return order is null
        ? new OrderStatusResult(false, orderNumber, null, null, [])
        : new OrderStatusResult(true, order.OrderNumber, order.Child, order.Status, order.Lines.Select(l => $"{l.Quantity} x {l.Toy}").ToList());
}
```

**"Not found" is a result, not an exception.** If a tool throws, the model never sees the problem as a thing it can reason about: depending on the framework the whole run fails, or the model is told
something generic went wrong. If the tool *returns* `Found: false`, the model reads it and can say "I can't find ORD-9999: could you check the number?", which is the behaviour you want.

The tool also tells the model *what the statuses mean* (in its `[Description]`: "open means still to be made"), because the model can't know that from the word alone.

The order book is the one from Part 2 (`orders`), and an order holds toy **names** and quantities, not prices. If an elf asked how much an order costs, that would be a **new tool**: a `$lookup` to the catalogue, exactly Part 2's aggregation, behind a function. Tools are where the rest of this course gets reused.
</details>

<details>
<summary><b>Step 4 — CreateTools</b> (LLM function calling)</summary>

```csharp
public IList<AITool> CreateTools()
{
    return
    [
        AIFunctionFactory.Create(typeof(ElfTools).GetMethod(nameof(FindToysAsync))!, this,
            new AIFunctionFactoryOptions { Name = ElfAgentInstructions.FindToysTool }),

        AIFunctionFactory.Create(typeof(ElfTools).GetMethod(nameof(GetOrderStatusAsync))!, this,
            new AIFunctionFactoryOptions { Name = ElfAgentInstructions.OrderStatusTool }),
    ];
}
```

**Function calling** (also called *tool use*) is how a model asks for something to be done. The model never runs your code. The exchange is:

1. You send the model the conversation **and a list of tool definitions**: for each, a name, a description, and a JSON schema of its parameters.
2. The model replies, instead of text, with a **tool call**: *"call `get_order_status` with `{ "orderNumber": "ORD-1001" }`"*.
3. **Your code** runs the tool and sends the result back.
4. The model uses the result to write its answer, or asks for another tool.

`AIFunctionFactory.Create` builds step 1's definition from your method: the name you give it, the method's `[Description]` as the tool's description, and a JSON schema from the parameters (their names, types, `[Description]`s, and which have
defaults, so which are optional). It also gives you step 3: an `AIFunction` knows how to **invoke itself** from the arguments a model sends.

The schema for `get_order_status` ends up as (shortened):

```json
{ "type": "object",
  "properties": { "orderNumber": { "type": "string", "description": "The order number, such as 'ORD-1001'. ..." } },
  "required": [ "orderNumber" ] }
```

and for `find_toys`, **none** of its four parameters is `required`, because each has a default: that is why a model can call it with no arguments at all. Optional versus required is something the schema tells the model, so it matters that it is right.

The `AITool`/`AIFunction` types are from `Microsoft.Extensions.AI`, not from Agent Framework: **the same tools work with any `IChatClient`**, and with the hand-written loop in the next step.
</details>

<details>
<summary><b>Step 5 — RunAsync</b> (cyclical graphs; tools and function calling)</summary>

```csharp
public static async Task<ToolLoopResult> RunAsync(
    IChatClient chat,
    IList<ChatMessage> messages,
    IList<AITool> tools,
    int maxTurns = DefaultMaxTurns,
    CancellationToken cancellationToken = default)
{
    var added = new List<ChatMessage>();
    var options = new ChatOptions { Tools = tools };

    for (var turn = 1; turn <= maxTurns; turn++)
    {
        // The MODEL node.
        var response = await chat.GetResponseAsync(messages, options, cancellationToken);

        foreach (var message in response.Messages)
        {
            messages.Add(message);
            added.Add(message);
        }

        var calls = response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>().ToList();

        // No tool wanted: the edge out of the model, to the end.
        if (calls.Count == 0)
            return new ToolLoopResult(response.Text, turn, added);

        // The TOOLS node.
        var results = new List<AIContent>();
        foreach (var call in calls)
        {
            object? result;
            var function = tools.OfType<AIFunction>().FirstOrDefault(f => f.Name == call.Name);

            if (function is null)
            {
                result = $"There is no tool called '{call.Name}'.";
            }
            else
            {
                try
                {
                    result = await function.InvokeAsync(new AIFunctionArguments(call.Arguments), cancellationToken);
                }
                catch (Exception ex)
                {
                    // A model can read a result. It can't read an exception.
                    result = $"The tool '{call.Name}' failed: {(ex.InnerException ?? ex).Message}";
                }
            }

            results.Add(new FunctionResultContent(call.CallId, result));
        }

        var toolMessage = new ChatMessage(ChatRole.Tool, results);
        messages.Add(toolMessage);
        added.Add(toolMessage);

        // ... and the edge BACK to the model, which is the next time round the loop.
    }

    throw new InvalidOperationException($"The model was still asking for tools after {maxTurns} turns, so the loop stopped.");
}
```

**An agent is a loop.** Draw this method as a graph and you get two **nodes**, *model* and *tools*, and these **edges**:

```
            ┌────────────── results go back ──────────────┐
            ▼                                              │
   start ─► MODEL ── asked for tools? ── yes ──► TOOLS ────┘
              │
              no
              ▼
            answer ─► end
```

The edge from *tools* back to *model* is what makes the graph **cyclical**, and it is the whole reason an agent can do something a single prompt can't: **use what a tool found to decide what to do next**. In the test where the model first finds an order number and then looks the
order up, it couldn't have made the second call without the first result. Each trip round the cycle is one "turn".

**Bridging to other frameworks.** The badge may describe this in LangGraph's terms, where you draw the same picture explicitly: an *agent* node (the model), a *tools* node, and a *conditional edge* from the agent that goes to the tools node if
the last message holds tool calls, and to the end if it doesn't. Same cycle. In Agent Framework you don't draw the graph: `ChatClientAgent` runs this loop for you (Step 6), and the loop's edges are just `if` and `for`. The vocabulary
differs; the shape doesn't.

**Three details that are real engineering:**

- **Every tool call gets a result with the same `CallId`.** The model matches results to requests by id, and several calls can arrive in one reply (the "several at once" test): all are run, and answered together in one `tool` message.
- **Unknown tools and failing tools both become results** (see Step 3). A loop that throws on the first hiccup is an agent that falls over; one that reports it lets the model recover or explain.
- **The turn limit is not optional.** A model can keep asking for tools for ever (a tool that always returns something unhelpful, a model that keeps retrying). Every turn costs time and money, so a cycle needs a cap. Get the boundary right, too: if the model *answers* on the last allowed turn, that is a success, and
  only a model still asking for tools after that turn is an error. There is a test for that edge, because it is the easy one to get off by one.

The method adds to the `messages` list you pass in, as well as returning what it added, so a caller that is keeping a conversation has all of it.
</details>

<details>
<summary><b>Step 6 — CreateAgent</b> (retrieval tools; an agent's reasoning, tools and memory)</summary>

```csharp
public static ChatClientAgent CreateAgent(
    IChatClient chat,
    IList<AITool> tools,
    Func<string, CancellationToken, Task<IEnumerable<TextSearchProvider.TextSearchResult>>>? handbook = null,
    ChatHistoryProvider? history = null)
{
    var toolNames = tools.Select(t => t.Name).ToList();
    var providers = new List<AIContextProvider>();

    if (handbook is not null)
    {
        providers.Add(new TextSearchProvider(handbook, new TextSearchProviderOptions
        {
            // The handbook is a TOOL the model may call, not a search that runs on every message.
            SearchTime = TextSearchProviderOptions.TextSearchBehavior.OnDemandFunctionCalling,
            FunctionToolName = ElfAgentInstructions.HandbookTool,
            FunctionToolDescription = "Searches the Elf Handbook for the workshop's rules and procedures. Use it for questions about how things are done here.",
        }));

        toolNames.Add(ElfAgentInstructions.HandbookTool);
    }

    return chat.AsAIAgent(new ChatClientAgentOptions
    {
        Name = "Elf Assistant",
        ChatOptions = new ChatOptions { Instructions = ElfAgentInstructions.Build(toolNames), Tools = tools },
        ChatHistoryProvider = history,
        AIContextProviders = providers,
    });
}
```

`ChatClientAgent` is the loop from Step 5, plus what you'd otherwise write yourself: it sends the **instructions** with every call (as `ChatOptions.Instructions`: you will not see them as a message), runs tool calls and feeds the results back, keeps a **session**
that holds the conversation's state, and gives **providers** a place to plug in: a *context provider* can add tools and messages before each call, and a *chat history provider* stores and replays the conversation (Step 9).

**RAG versus an agent, in one setting.** `TextSearchProvider` has two modes, and the choice is the difference between Part 4A and this part:

| `SearchTime` | What happens | It is |
|---|---|---|
| `BeforeAIInvoke` | the search runs **on every message** and the results are put in the prompt | RAG, as in Part 4A |
| `OnDemandFunctionCalling` | the search is offered as a **tool**; the model decides if and when to call it | a **retrieval tool**: an agent |

The test for this: ask the agent about an order, and the embedding model is **never called**. A RAG pipeline would have searched the handbook for "Where is ORD-1001?" and found nothing useful. An agent knows that is a job for `get_order_status`.

The retrieval tool is your Part 4A retriever, wrapped (`HandbookSearch`, already written for you): embed the question, `$vectorSearch`, keep what clears the relevance bar. Nothing about the search changed: only **who decides to run it**.

The instructions are built from the tools the agent *has*: the ones you pass, plus the handbook tool only if there is a handbook. An agent whose instructions promise a tool it doesn't have will call it, and fail.
</details>

<details>
<summary><b>Step 7 — AppendAsync, LoadAsync, EnsureIndexesAsync</b> (short-term agent memory with MongoDB)</summary>

```csharp
public async Task AppendAsync(string conversationId, IEnumerable<ChatMessage> newMessages)
{
    var toStore = newMessages.ToList();
    if (toStore.Count == 0) return;

    // Each message's place in the conversation: after however many are already there.
    var next = await _messages.CountDocumentsAsync(m => m.ConversationId == conversationId);

    await _messages.InsertManyAsync(toStore.Select((message, i) => new StoredMessage
    {
        ConversationId = conversationId,
        Sequence = next + i,
        Role = message.Role.Value,
        Json = JsonSerializer.Serialize(message, AIJsonUtilities.DefaultOptions),
        CreatedAt = DateTime.UtcNow,
    }));
}

public async Task<List<ChatMessage>> LoadAsync(string conversationId)
{
    var stored = await _messages.Find(m => m.ConversationId == conversationId).SortBy(m => m.Sequence).ToListAsync();

    return stored.Select(m => JsonSerializer.Deserialize<ChatMessage>(m.Json, AIJsonUtilities.DefaultOptions)!).ToList();
}

public async Task EnsureIndexesAsync(TimeSpan keepFor)
{
    await _messages.Indexes.CreateManyAsync(
    [
        new CreateIndexModel<StoredMessage>(
            Builders<StoredMessage>.IndexKeys.Ascending(m => m.ConversationId).Ascending(m => m.Sequence),
            new CreateIndexOptions { Unique = true }),

        new CreateIndexModel<StoredMessage>(
            Builders<StoredMessage>.IndexKeys.Ascending(m => m.CreatedAt),
            new CreateIndexOptions { ExpireAfter = keepFor }),
    ]);
}
```

**Memory in an agent is two different things.**

| | **Short-term memory** | **Long-term memory** |
|---|---|---|
| **What it holds** | the messages of *this conversation*: what was said, and the tool calls and results | facts and preferences worth keeping *across* conversations ("Ingrid's family is vegetarian", "this elf always works the night shift") |
| **How it's used** | replayed to the model **every turn**, in order | **searched**, and the few relevant items put in the prompt (it is RAG over what the agent has learned) |
| **How it grows** | with every message, so it **must be trimmed** | by deciding what is worth remembering, so it needs **distilling** |
| **How long it lives** | the length of a conversation, so it should **expire** | indefinitely, until it's wrong or someone asks for it to go |
| **In this part** | Steps 7–9 | explained below, not built |

This step stores **short-term** memory. Three decisions in it are worth understanding:

- **One document per message**, not one document per conversation. A conversation document would grow without limit towards MongoDB's 16 MB document size, and you'd rewrite the whole thing for every new message. A document per message makes *appending* a single insert, and
  the `(conversationId, sequence)` index makes *reading a conversation in order* an index scan.
- **The whole message is kept as JSON** (`AIJsonUtilities.DefaultOptions`), because a `ChatMessage` is not just text: it can hold a tool call, with its arguments and id, or a tool result. If you stored only the text, the next turn would show the model a tool result with no call before it,
  which most model APIs reject. The `role` is also stored as its own field so you can read a conversation in Compass without parsing JSON.
- **The unique index** means a message can't be stored twice at the same position: two writers that raced to append to the same conversation get an error instead of a corrupted conversation. (Counting then inserting isn't safe for *concurrent* writers by itself, which is why the index is there; a real system would retry.)

**The TTL index** is MongoDB deleting for you. A document is removed some time after `createdAt` plus `keepFor` (the background task runs about once a minute, so not to the second). Short-term memory that never expires is just a log that grows for ever, and a conversation from last month is not "short-term".

**Long-term memory**, which isn't built here, would add a second collection of **facts**: after a conversation, the agent (or a second model call) extracts what is worth keeping ("Ingrid prefers wooden toys"), stores each fact with an **embedding**, and at the start of a later conversation *searches* them with the new question and puts the best few in the prompt. It
is Part 4A's pipeline, pointed at what the agent has *learned* instead of at a handbook. The choices that matter are **what to keep** (not everything), **when to forget**, and **who can see it** (a fact about a child is personal data).
</details>

<details>
<summary><b>Step 8 — TrimToRecent</b> (short-term memory)</summary>

```csharp
public static List<ChatMessage> TrimToRecent(IReadOnlyList<ChatMessage> messages, int maxMessages)
{
    ArgumentOutOfRangeException.ThrowIfLessThan(maxMessages, 1);

    if (messages.Count <= maxMessages) return [.. messages];

    // Start where a TURN starts: at a user message.
    for (var start = messages.Count - maxMessages; start < messages.Count; start++)
        if (messages[start].Role == ChatRole.User)
            return [.. messages.Skip(start)];

    return [];
}
```

**Why trim at all?** A model's **context window** is finite, every message in it costs tokens (and so money and time), and a long conversation eventually won't fit. Short-term memory needs a limit, and the simplest one is "the most recent N messages".

**Why not just take the last N?** Because a conversation isn't a flat list. One *turn* is a user message, then possibly an assistant message asking for a tool, then the tool's result, then the assistant's answer. The call and its result **belong together**. Take the last 6 of 8 messages in the test's example and you'd start at a tool *result* whose call has been cut off: **most model APIs reject that** as malformed, and the failure shows up
on the next turn, far from the code that caused it.

So the window may only **begin at a user message**: the start of a turn. The cost is that it may keep fewer than `maxMessages`, and if no whole turn fits, it keeps nothing. It never keeps *more*, and it never cuts a turn in half.

What this simple window **forgets** is worth saying aloud: an elf who gave their order number twenty turns ago will be asked for it again. Fixes include a larger window, **summarising** the old messages into one short message, or moving the important facts into long-term memory. Each is a trade between tokens, fidelity and complexity.
</details>

<details>
<summary><b>Step 9 — RecallAsync, RememberAsync</b> (short-term agent memory with MongoDB)</summary>

```csharp
public async Task<List<ChatMessage>> RecallAsync(AgentSession session)
{
    var history = await _store.LoadAsync(ConversationOf(session));

    return ChatWindow.TrimToRecent(history, _maxMessages);
}

public async Task RememberAsync(AgentSession session, IEnumerable<ChatMessage> request, IEnumerable<ChatMessage> response)
{
    await _store.AppendAsync(ConversationOf(session), request.Concat(response));
}
```

**A `ChatHistoryProvider` is asked two questions on every run**, and these two methods are the answers:

- *Before the run:* "what has been said so far?" The framework combines your answer with the new message. (`ProvideChatHistoryAsync` hands over to `RecallAsync`.)
- *After the run:* "here is what happened: remember it." You are given **the messages that came in** (just the new user message) and **everything the agent produced** (its tool call, the tool's result, and the answer). (`StoreChatHistoryAsync` hands over to `RememberAsync`.)

That second point is easy to miss and it is why the memory works with tools: the response isn't only the final answer, it is **the whole exchange**, so the next turn can show the model that it already looked the order up.

**How the provider knows which conversation it is.** The conversation id lives **in the session** (`AgentSession.StateBag`), not in the provider. That is deliberate:

- One provider serves many sessions (many elves, many conversations at once), and it has no idea which one it is being asked about except through the session.
- A session can be **serialised** (`SerializeSessionAsync`) and restored (`DeserializeSessionAsync`). The id goes with it. After a restart, a new copy of the program restores the saved session, the provider sees the same conversation id, and **picks up from MongoDB where the last one stopped**. The test for this builds new objects for everything except the session's saved state.
- Two sessions can be pointed at one conversation with `UseConversation`, which is how a conversation could be handed from one elf to another; and a session that was never told gets a fresh id of its own.

**A failed run remembers nothing.** If the model throws halfway through, the framework tells the provider so, and the already-written `StoreChatHistoryAsync` skips the store. Otherwise a half-finished turn (a user message with no answer, or a tool call with no result) would be replayed next time, and would break it.

**Where the pieces meet.** `ElfAgent.CreateAgent(chat, tools, handbook.AsTextSearch(), history: new MongoChatHistoryProvider(store))` is the whole agent: a model for reasoning, tools it can use, a handbook it can search when it chooses, and a conversation that lives in MongoDB. Run it with `tools/elf-chat` and restart the program mid-conversation: it remembers.
</details>

---

## If something goes wrong

**Step 4's schema test fails on a parameter's description.** The `[Description]` attributes are on the methods already, in Steps 2 and 3. If you rewrote a signature and dropped one, put it back: the model reads them.

**Step 5 hangs, or the loop test never ends.** Check the `for` loop's bound and that every pass either returns an answer or adds a `tool` message. A loop that doesn't add the tool results asks the model the same question for ever.

**Step 5: "the tool call has no result" or the model repeats itself.** The `FunctionResultContent` must carry the **same `CallId`** as the call it answers.

**Step 6: the handbook tool isn't offered.** It is only offered when a handbook function is passed in. And the instructions must be built from the names of the tools the agent *has*, plus the handbook tool when there is one.

**Step 7: `E11000 duplicate key`.** Two messages were given the same `Sequence` in one conversation. Count the existing messages *once*, then add each message's position in the batch.

**Step 9 passes alone but the restart test fails.** The conversation id has to come from the **session** (`ConversationOf(session)`), not from a field on the provider: a new provider object knows nothing.

**Steps 7 to 9 or the handbook test time out waiting for an index.** Atlas Local's search engine isn't running: `docker compose up -d`, then `dotnet run --project tools/doctor`.
