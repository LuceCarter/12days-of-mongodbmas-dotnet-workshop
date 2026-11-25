# Part 4A — Santa's Elf Assistant: RAG

> **Level:** Intermediate. Parts 3A and 3B help, but Part 3B is the one that matters: this part builds on vector search.
> **Time:** About an hour and a half for the core path (an estimate: nobody has timed it yet).
> **Badge:** RAG with MongoDB

> ⚠️ **Heads up: two things in this part fail quietly.** A vector search **always returns something**, however
> little it has to do with the question, so a RAG system has to decide when the answer is "I don't know"
> ([Step 8](#step-8--put-it-together--objectives-3-and-8)). And chunking can cut an idea in half, which no error will ever tell
> you ([Step 3](#step-3--cut-it-into-chunks--objective-6)).

---

## The story

The elves have a new assistant. It's a language model, and it's fluent, friendly and quick.

On its first day, a young elf asks it how to tell whether a toy part is too small for a baby.

*"Use the silver test tube in the safety cupboard,"* says the assistant, warmly. *"Any part that fits through the
top is a small part."*

There is no silver test tube. There is no safety cupboard. The real one is **brass**, it hangs by the sanding bench, and
it is in the Elf Handbook, on a page the assistant has never read.

Mrs Claus reads the answer twice. "It isn't lying," she says. "It doesn't know what it doesn't know. It has
never seen our handbook, so it did what these models do and wrote the most *likely-sounding* answer.
We don't need a cleverer assistant. We need one that **looks it up first**."

So today you teach it to read the handbook: to find the right paragraph, give it to the model, and say where the answer came from.

---

## Get the code

Part 4A lives in the same repository as the other parts:

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
part4a-rag/
  README.md                     this page
  Starter/HandbookLoader.cs     ← you write code here (Step 1)
  Starter/ToyFacts.cs           ← (Step 2)
  Starter/Chunker.cs            ← (Step 3)
  Starter/Ingestor.cs           ← (Step 4)
  Starter/Retriever.cs          ← (Steps 5 and 6)
  Starter/GroundedPrompt.cs     ← (Step 7)
  Starter/ElfAssistant.cs       ← (Steps 8 and 9)
  Starter/VoyageEmbedder.cs     already written (you wrote it in Part 3B)
  Starter/ElfLlm.cs             already written: the language model
  Tests/                        the tests that tell you when a step is right
  SOLUTIONS.md                  every answer, with the reasoning
```

---

## Before you start

Make sure your environment is ready. From the repository folder:

```bash
docker compose up -d
dotnet run --project tools/doctor
```

This part needs **Atlas Vector Search**, which a plain MongoDB container doesn't have. The Atlas Local
container in our `docker-compose.yml` does, and the doctor checks that it's working. Full instructions are in
[docs/setup.md](../docs/setup.md).

**You don't need an API key.** The Elf Handbook has already been cut into chunks and turned into vectors, by Voyage AI's
`voyage-4` model, and so have the example questions. They're shipped in the repository. The tests use stand-ins for both
models, so nothing here calls the internet.

(If you *do* want to ask a real model at the end, [the Level up](#-level-up) shows how, with a free key. Keys go in
environment variables and never in code: **never commit a key.**)

You'll want something to explore in the shell and Compass. Load the chunks:

```bash
dotnet run --project tools/seed -- --handbook
```

It's safe to run again whenever you like. The **tests don't need it**: they use their own database,
`santas_workshop_part4a_tests`, so running them never overwrites the data you're exploring.

---

## How this works

Everything you write goes in the files under [`part4a-rag/Starter/`](Starter/). There are nine steps, and each is a `// TODO` and a
`throw new NotImplementedException` in one of the files above. Run
the tests whenever you like:

```bash
dotnet test part4a-rag/Tests
```

They start **red**. Each step turns some of them green. When all **73** pass, you're done. (Five tests, the ones that begin *Already written*, check code that is already done for you, so they're green from the start.)

The steps build on each other, in order: Step 4 uses your `Chunker`, and Step 8 uses your `Retriever` and your prompt. Where
a step needs *data* from an earlier one, the tests supply it: Step 6 searches a **reference index** they build themselves, so a
half-finished Step 5 doesn't turn Step 6 red.

Stuck? [`SOLUTIONS.md`](SOLUTIONS.md) has every answer with an explanation. Looking isn't
cheating: reading a worked example is how most of us learn this.

---

## What is RAG?

**RAG** stands for **retrieval-augmented generation**, and the name is the recipe:

1. **Retrieve** the pieces of your own data that are relevant to the question.
2. **Augment** the prompt by putting those pieces in it.
3. **Generate** the answer with a language model that now has what it needs.

### Why bother? What a language model can't do

| The limit | What goes wrong | How RAG helps |
|---|---|---|
| **It has never seen your data** | Ask about *your* brass test tube and it invents a silver one, fluently | Your text is in the prompt |
| **Its knowledge stops at a training date** | Last year's rule comes out as today's | You retrieve from what's in the collection *now* |
| **It can't show its working** | You can't tell a fact from a plausible guess | The prompt carries sources, so the answer can cite them |
| **It would rather guess than say "I don't know"** | A confident wrong answer is worse than none | Instructions tell it to use only what it's given, and you can refuse to call it at all when nothing relevant was found |

RAG isn't the only fix. You could **fine-tune** a model on your data (slow, costly, and out of date again by next month),
or paste the whole handbook into every prompt (it won't fit, and you'd pay for all of it every time). Retrieval finds the three
paragraphs that matter and sends only those.

### Where it's used

Anywhere the answer lives in *your* documents and has to be right: a help desk answering from product docs, an internal
assistant over policies and wikis (this one), question-answering over contracts, tickets or papers, and any tool where
"show me where you got that" matters.

### The pieces

```
  ingestion (once, and again when the handbook changes)         asking (every question)

  Elf Handbook ──► sections ──► chunks ──► embed ──► Atlas      question ──► embed ──► Atlas $vectorSearch
   (Step 1)        (Step 1)    (Step 3)   (Step 4)  (Step 4,5)   (Step 8)    (Step 8)    + prefilter (Step 6)
                                                                                              │
                                                                          answer ◄── model ◄── prompt (Step 7)
                                                                          + sources  (Step 8)   ◄── best chunks
```

| Component | In this part | Its job |
|---|---|---|
| **Data sources** | the handbook (and the toys) | what the assistant knows |
| **Chunker** | `Chunker` | cut it into findable pieces |
| **Embedding model** | `IEmbedder`, Voyage AI | text → vector, for the chunks **and** for the question |
| **Vector store** | MongoDB Atlas | keeps each vector beside its text and its metadata; finds the nearest |
| **Retriever** | `Retriever` | question → the few best chunks, filtered first |
| **Prompt** | `GroundedPrompt` | chunks + question → what the model is shown |
| **Generator** | `IChatClient`, Gemini | writes the answer |

### Two models, on purpose

There are **two different models** here, from two different companies, and it matters that they're separate:

- **Voyage AI** makes the *vectors*. Its job is to put similar meanings near each other so that Atlas can *find* things.
- **Google Gemini** writes the *answer*. Its job is to read some text and say something sensible about it.

You can change either without touching the other. Swap Gemini for another model and the stored vectors stay valid.
Swap the embedding model and every vector must be remade. That separation is something the badge assesses, and
you can see it in the code: `ElfAssistant`'s constructor takes **an `IEmbedder` and an `IChatClient`**, not one thing that does both.

---

## The data

The **Elf Handbook** is a markdown file, [`src/Shared/SeedData/elf-handbook.md`](../src/Shared/SeedData/elf-handbook.md): ten sections of
rules and procedures, each written for an **audience**.

| Section | Audience | Words | Chunks |
|---|---|---|---|
| Toy Safety | toy-makers | 263 | 5 |
| Quality Control | toy-makers | 225 | 4 |
| Wrapping and Labelling | wrapping-elves | 231 | 4 |
| Loading the Sleigh | sleigh-crew | 233 | 4 |
| Reindeer Care | stable-elves | 224 | 4 |
| Reading and Answering Letters | letter-elves | 228 | 4 |
| The Naughty and Nice Ledger | ledger-keepers | 200 | 3 |
| Workshop Rules | all-elves | 217 | 4 |
| Elf Wellbeing | all-elves | 206 | 4 |
| Emergencies | all-elves | 235 | 4 |

That's **40 chunks** of up to 80 words each, with 20 words of overlap. `handbook-chunks.json` holds every one, with its
vector. `handbook-questions.json` holds ten example questions and *their* vectors. One of them (*"What is the capital of France?"*)
is deliberately not in the handbook.

A stored chunk looks like this (the vector's 1024 numbers left out):

```json
{ "_id": "toy-safety-1", "section": "Toy Safety", "audience": "toy-makers",
  "source": "The Elf Handbook › Toy Safety", "chunkIndex": 1,
  "text": "Any toy meant for a child under three must pass the small-parts test. Drop each loose part into the brass test tube ...",
  "embedding": [ ... 1024 numbers ... ] }
```

**Why does a chunk carry `section`, `audience` and `source`?** Because that metadata is what lets you **filter** the search
(Step 6) and **cite** the answer (Step 8). A chunk that is only a vector can do neither.

---

## Part A — Collect and prepare the data

### Step 1 — Read the handbook · *objectives 4 and 5*

A RAG system starts with a decision about **what it should know**, and that decision is a *source*. The handbook is
**unstructured**: prose, with no schema. It's the commonest thing people want to ask questions of: manuals, wikis,
policies, tickets, transcripts, papers.

Open [`Starter/HandbookLoader.cs`](Starter/HandbookLoader.cs) and implement `ParseHandbook`: split the markdown into
sections, each with its heading, its audience and its body.

**Preparing** the data is where RAG systems quietly go wrong: a heading lost, a stray `\r` from a Windows checkout, a section that
bled into the next. Every later step inherits whatever you get wrong here, so the tests check the awkward cases too.

### Step 2 — Turn a structured record into text · *objective 4*

The elves' world isn't all prose. The toy catalogue is **structured**: named, typed fields. An embedding model reads
*text*, so a record has to become a sentence before it can be embedded or quoted.

Implement `ToyFacts.ToyToText` in [`Starter/ToyFacts.cs`](Starter/ToyFacts.cs). What goes in the sentence is a design decision.
Put in **everything that changes what a sensible answer is**: a model that's told only that a toy exists will happily recommend
one that's out of stock.

> 🧠 **Embed it, or query it?** Structured data can feed RAG two ways. Turn it into text and **embed** it, which suits
> facts that are about *meaning* and change slowly. Or leave it in its fields and let the assistant **query** it live,
> which suits facts that change fast: a stock count is out of date as soon as it's embedded. Good systems use both.
> Part 4B gives the assistant a tool for the second.

---

## Part B — Chunking

### Step 3 — Cut it into chunks · *objective 6*

A vector summarises everything in the text it was made from. Embed a whole section and it blurs into the average of all of it. A few
sentences are about **one thing**, so a question about reindeer lands near the chunk that answers it.

But a cut falls wherever the word count says, not where an idea ends. **Overlap** repeats the end of one chunk at the start
of the next, so something that sits on a boundary is whole in at least one of them:

```
text:      a b c d e f g h i j        size 5, overlap 2
chunk 1:   a b c d e
chunk 2:         d e f g h            ← starts 3 words (size − overlap) later, repeating d e
chunk 3:               g h i j
```

Implement `Chunker.SplitIntoChunks` in [`Starter/Chunker.cs`](Starter/Chunker.cs).

**It isn't free.** More overlap means more chunks to embed, store and search, and near-duplicate chunks can crowd the same answer into
the top results. Ten to twenty per cent is a common start, then you measure on real questions. We split on **words**
for clarity; real systems often use tokens, sentences, or the document's own headings.

> ⚠️ The vectors in the repository were made from chunks of **80 words with 20 overlap**, cut exactly the way the worked example
> above shows. The last test for this step checks that **your** chunker makes the very same 40 chunks. If it's one word out,
> the vectors in the repo no longer describe your text.

---

## Part C — Ingest, and index

### Step 4 — Embed and store · *objective 5*

**Ingestion** is a pipeline: load, clean, chunk, embed, store. Implement `Ingestor.IngestAsync` in
[`Starter/Ingestor.cs`](Starter/Ingestor.cs): chunk each section, embed each chunk, and store it **with** its section, audience and source.

Two things to get right:

- Embed chunks with input type `"document"`, and (in Step 8) questions with `"query"`. Voyage's model expects the two differently.
- **Run it twice and nothing doubles; edit the handbook and nothing stale stays.** Ingestion happens again every time the
  handbook changes. If a section got shorter, its old last chunk must disappear, or your assistant keeps quoting a rule someone deleted.

The tests give your `Ingestor` a stand-in for the embedding model, so no key is needed. It makes one up from the text.

### Step 5 — The index, with filter fields · *objective 7*

This is Part 3B's Step 4 again, with different fields. Implement `Retriever.CreateIndexAsync` in
[`Starter/Retriever.cs`](Starter/Retriever.cs): a vector index over `embedding`, which **also declares `section` and `audience` as `filter` fields**.

> 🧠 **The same traps, still true.**
> `numDimensions` must be **1024**, the length of these vectors: wrong, and the index builds and says ready, then queries error
> or quietly return nothing. Only declared fields can be filtered on: ask for another and you get `Path 'x' needs to be indexed as filter`.
> And creating an index **returns straight away**: wait for `queryable`. ([Part 3B](../part3b-vector-search/README.md#the-trap-the-number-of-dimensions) explains the first.)

---

## Part D — Retrieve

### Step 6 — Search, narrowed first · *objective 7*

A **retriever** is the "R" in RAG: it takes the question's vector and returns the few chunks most worth showing the model. A
**prefiltered** one narrows what it searches *before* it searches.

Why? Because the elf asking might be a sleigh-crew member who only wants loading rules, or the question might be about one
section. A filter is a promise that no other chunk is even considered, and a *pre*-filter, one inside `$vectorSearch`,
still returns the `k` you asked for. Filter *after* the search and you get whichever of the nearest `k` happened to be in the right section,
which can be none.

Implement `Retriever.RetrieveAsync` in [`Starter/Retriever.cs`](Starter/Retriever.cs): `section` and `audience` are each optional,
each an **equality**, and together they must both match.

```js
{ $vectorSearch: { index: "handbook-vectors", path: "embedding", queryVector: v, numCandidates: 80, limit: 4,
                   filter: { section: "Toy Safety" } } }
```

The score comes back from `{ $meta: "vectorSearchScore" }`, as in Part 3B. For this index it is `(1 + cosine) / 2`, between 0 and 1.

Eight of this step's tests ask the **real vectors** a plain-words question and check the right section of the handbook comes
first: *"I'm worn out and it's the busiest week of the year"* finds **Elf Wellbeing**, a section that doesn't contain the word *worn*.

---

## Part E — Generate

### Step 7 — The prompt that grounds the model · *objectives 1 and 2*

Now the "A" and the "G". A model reads what's in its prompt. Implement `GroundedPrompt.BuildPrompt` in
[`Starter/GroundedPrompt.cs`](Starter/GroundedPrompt.cs): a **system** message of rules, and a **user** message that holds the
extracts, each with its source, and the question **last**.

The rules are the whole trick: *answer using only these extracts; say you don't know when they don't contain the answer; name your sources.*
Look back at the table of what a model can't do. Each rule closes one of them.

Nothing in this step calls a model. It only builds text, which is why a test can check it exactly. **Prompts are code:** keep them
where you can read them, and test them.

> 🧠 **RAG's ceiling is retrieval.** If the right chunk isn't retrieved, the model answers from the wrong ones, or declines.
> The quality of the answer is capped by the quality of Steps 3 to 6, which is why they got most of this part. And a model can still ignore
> its rules: they lower the risk, they don't remove it.

### Step 8 — Put it together · *objectives 3 and 8*

Implement `ElfAssistant.AskAsync` in [`Starter/ElfAssistant.cs`](Starter/ElfAssistant.cs). A question comes in; embed it as a
`"query"`, retrieve, keep what is relevant, build the prompt, ask the model, and return the answer **with the chunks it came from**.

**The relevance bar.** A vector search *always* returns its nearest neighbours, even when nothing is near. Ask for the capital of France and you still
get four chunks back: the least-unlike ones. Hand those to a model and it may build an answer out of them. So the assistant has a **minimum score**: below it, there is
no relevant chunk, the assistant says so, and **the model isn't called at all**. That's cheaper, faster and safer than asking a model to refuse.

**Where to set the bar is a measurement**, on your model and your data. Here is what the best chunk scored for each of the ten example questions, against the real `voyage-4` vectors:

| Question | Best chunk | Score |
|---|---|---|
| How do I know if a toy is safe for a baby? | `toy-safety-3` | 0.831 |
| What should I do when a reindeer won't eat? | `reindeer-care-1` | 0.850 |
| How do I wrap something fragile? | `wrapping-and-labelling-0` | 0.738 |
| In what order do presents go onto the sleigh? | `loading-the-sleigh-0` | 0.836 |
| A child wrote to say they are sorry for being naughty. How is that counted? | `the-naughty-and-nice-ledger-2` | 0.775 |
| I'm worn out and it's the busiest week of the year. | `elf-wellbeing-0` | 0.717 |
| Someone knocked over a pot of glitter by the machines. | `workshop-rules-2` | 0.763 |
| Can I show my family around the toy floor? | `workshop-rules-1` | 0.776 |
| What should I do if a machine catches fire? | `emergencies-0` | 0.772 |
| **What is the capital of France?** *(not in the handbook)* | `toy-safety-4` | **0.627** |

Every answerable question found its right section first, and scored **0.717 or more**. The question the handbook can't answer still got a "best" chunk, and it scored **0.627**. The bar is `0.67`,
between the two. Below it, `AskAsync` says it doesn't know and never calls the model.

Be wary of these numbers. **Ten questions is a tiny sample**, and the gap is only about 0.09. The score isn't a probability: 0.7 doesn't mean "70% likely to be right". And it belongs
to *this* model and *this* handbook: another embedding model, or another chunk size, scores differently, so the bar has to be measured again. Notice that the glitter question's second-best chunk scored
0.669, just under the bar, so only one chunk reaches the model. That is the bar working, not failing.

**Two models.** Look at `ElfAssistant`'s constructor again: an `IEmbedder` and an `IChatClient`. They do different jobs, and the test for this
step checks that the embedding model only ever sees the question, and the generator only ever sees the prompt.

### Step 9 — A retrieval tool · *objective 7, and the bridge to Part 4B*

Microsoft Agent Framework has a ready-made RAG building block, **`TextSearchProvider`**, and it takes a **function**, not a collection: *given a question,
return a few text results*. Implement `ElfAssistant.AsTextSearch` to give it exactly that, built from your retriever.

Because the framework doesn't care *how* you search, everything you built (the chunks, the prefilter, the relevance bar) plugs straight in. It can run two ways: search on
**every turn** and put the results in the prompt (RAG as you've built it), or give the model the search as a **tool** and let *it* decide when to call it. That second
way is an agent, and it is Part 4B.

#### Going further: the same search through `MongoVectorStore`

Agent Framework's sibling, Semantic Kernel, has a **MongoDB connector** (`Microsoft.SemanticKernel.Connectors.MongoDB`) that wraps vector search behind a common
interface. We ran it against the data in this part, on Atlas Local, to see what it really does. **It is a preview package**, version `1.74.0-preview`, with no stable release, so
expect it to change.

What we saw:

- **It can read the collection and index you already made**, if you map the field names (it otherwise uses the property names, so `Section`, not `section`) and name your index:

```csharp
public sealed class HandbookRecord
{
    [VectorStoreKey, BsonId] public string Id { get; set; } = "";
    [VectorStoreData(IsIndexed = true), BsonElement("section")] public string Section { get; set; } = "";
    [VectorStoreData(IsIndexed = true), BsonElement("audience")] public string Audience { get; set; } = "";
    [VectorStoreData, BsonElement("source")] public string Source { get; set; } = "";
    [VectorStoreData, BsonElement("text")] public string Text { get; set; } = "";
    [VectorStoreVector(1024, DistanceFunction = DistanceFunction.CosineSimilarity), BsonElement("embedding")]
    public ReadOnlyMemory<float> Embedding { get; set; }
}

var collection = new MongoCollection<string, HandbookRecord>(database, "handbook_chunks",
    new MongoCollectionOptions { VectorIndexName = "handbook-vectors" });

var hits = await collection.SearchAsync(queryVector, 4,
    new Microsoft.Extensions.VectorData.VectorSearchOptions<HandbookRecord> { Filter = r => r.Section == "Toy Safety" }).ToListAsync();
```

- **Silent again.** Leave out `VectorIndexName` and it looks for an index called `vector_index`; yours isn't, so the search returns **nothing, with no error**. (Same family as Part 3B's traps.)
- **Name clash.** `VectorSearchOptions<T>` exists in both `MongoDB.Driver` and `Microsoft.Extensions.VectorData`, so you have to write one of them out in full.
- **`EnsureCollectionExistsAsync` creates the index for you**, named `vector_index`, with the fields you marked `IsIndexed` as filters.
- **The score is the same** as `$vectorSearch`'s: it matched our raw query to four decimal places.
- **Its filters went further than the docs we read said.** Equality, `!=`, `&&`, `||`, `Contains` and a numeric range all worked. A *string* comparison (`>=`) threw `NotSupportedException`.

The trade is the usual one: less code, in exchange for a preview dependency and less control over the exact pipeline. Whether that is worth it is your call.
This part's Starter uses the raw driver, because you already know it from Part 3B, and because it works the same in every version.

---

## ✅ You're done

```bash
dotnet test part4a-rag/Tests
```

All **73** green.

Ask the assistant something close to the question that started this. For *"How do I know if a toy is safe for a baby?"*, the three best chunks are all from **Toy Safety**, and
two of them (`toy-safety-0` and `toy-safety-1`) contain the **brass test tube**. That is overlap at work: the sentence about the tube sits across a cut, and Step 3 made sure it is whole in both.
The model is shown those chunks and told to use only them, so it answers from the handbook and names its source. The silver tube is gone.

---

## Badge checklist — RAG with MongoDB

Every objective the badge assesses, and the step that teaches it.

| ✓ | Objective | Step | Taught by |
|---|---|---|---|
| ☐ | **1** Define RAG and its use cases | Step 7, and *What is RAG?* | `BuildPrompt` |
| ☐ | **2** LLM limitations and how RAG addresses them | Step 7, and *What is RAG?* | `BuildPrompt`, `AskAsync` |
| ☐ | **3** Atlas Vector Search RAG architecture components | Step 8, and *The pieces* | `AskAsync` |
| ☐ | **4** Suitable structured and unstructured sources | Steps 1, 2 | `ParseHandbook`, `ToyToText` |
| ☐ | **5** Implement data collection, preparation and ingestion | Steps 1, 4 | `ParseHandbook`, `IngestAsync` |
| ☐ | **6** Chunk overlap | Step 3 | `SplitIntoChunks` |
| ☐ | **7** A prefiltered retriever | Steps 5, 6, 9 | `CreateIndexAsync`, `RetrieveAsync`, `AsTextSearch` |
| ☐ | **8** A generator separate from the embedding model | Step 8, and *Two models, on purpose* | `AskAsync`, `ElfLlm` |

| ☐ ⚠️ | **2** …and know **the relevance bar**: a vector search always returns its nearest neighbours, so something has to say "not close enough" | Step 8 | `AskAsync` |

🖐️ **Objectives 1 and 2 are definitions.** The tests prove the retrieved text reaches the prompt, that the rules are in it, and that the model isn't asked when nothing is relevant. But *explaining* RAG, its use
cases and the limits of a language model is something you tick yourself, in your own words.

**Do you have to finish the exercises to take the badge?** No: the badge is open and you can
take it whenever you like. But it assesses these concepts, so doing the steps first is the
easier route.

---

## 📋 From C# to the badge

The assessment is likely to show MongoDB Shell, Atlas UI or Python syntax rather than C#. The index, the
`$vectorSearch` stage and the shape of the prompt are the same either way. The full set lives in
[docs/csharp-to-badge.md](../docs/csharp-to-badge.md).

| What you want | Shell / Atlas | C# |
|---|---|---|
| Store a chunk with its metadata | `db.handbook_chunks.insertOne({ _id, section, audience, source, text, embedding })` | `ReplaceOneModel` with `IsUpsert` |
| A vector index with filter fields | `createSearchIndex("n", "vectorSearch", { fields: [ vector, filter, filter ] })` | `SearchIndexes.CreateOneAsync(...)` |
| Search, narrowed first | `{ $vectorSearch: { ..., filter: { section: "Toy Safety" } } }` | `VectorSearchOptions<T> { Filter = ... }` |
| The score | `{ $meta: "vectorSearchScore" }` | the same, in a `Project` |
| Chunk with overlap | a loop over words: window `size`, step `size − overlap` | `Chunker.SplitIntoChunks` |
| Call the language model | `POST .../chat/completions` with `messages` | `IChatClient.GetResponseAsync(messages)` |

---

## 🚀 Level up

Optional. None of this is assessed by the badge.

### Ask a real model

Everything so far used stand-ins. To see the whole thing work for real you need **two free keys**: one for Voyage AI (to embed the question) and one
for Google Gemini (to write the answer). Put them in environment variables. **Never commit a key**, and never paste one into code.

```bash
dotnet run --project tools/seed -- --handbook

export VOYAGE_API_KEY="your-voyage-key"
export VOYAGE_BASE_URL="https://ai.mongodb.com"     # only if you made the Voyage key in MongoDB Atlas
export GEMINI_API_KEY="your-gemini-key"             # a free key from https://aistudio.google.com/apikey

dotnet run --project tools/ask -- "How do I tell whether a toy part is a small part?"
```

`tools/ask` runs **your** code: your `Retriever`, your prompt and your `AskAsync`. It prints what the retriever found and how each chunk scored
against the relevance bar (✔ clears it, ✘ doesn't), then the answer and its sources. Try `--section "Reindeer Care"` or `--audience toy-makers` to
see the prefilter at work, and try a question the handbook can't answer.

A few honest notes:

- `ElfLlm.DefaultModel` is the Gemini model we pinned. **Google changes which models are on its free tier**, so if the call is refused, set `GEMINI_MODEL` to one your
  [AI Studio](https://aistudio.google.com) account lists as free.
- Google's pricing page lists free-tier use as *"Used to improve our products: Yes"*. Check the terms yourself, and don't send anything private to a free key.
- **Can't create a Gemini key** (some organisations block them)? Use **Google Cloud** instead. Run `gcloud auth login` and `export GOOGLE_CLOUD_PROJECT="your-project-id"`
  (billing must be enabled on the project), and leave `GEMINI_API_KEY` unset. `ElfLlm` then talks to Gemini on Vertex AI, with a short-lived token from `gcloud`. Vertex model names start
  with `google/`, and the models available differ, so you may need `GEMINI_MODEL`. Same code, same tests: only `ElfLlm` knows the difference, which is the point of keeping the generator separate.
- Keys for the Voyage call can be created on voyageai.com or in MongoDB Atlas. They use **different hosts**, which is what `VOYAGE_BASE_URL` is for.

### Change the chunk size

Re-cut the handbook at 40 words with 10 overlap, and at 160 with 40. (You'll need a Voyage key to embed them, and your own index.) What happens to the
scores? To the *number* of chunks that clear the bar? There's no right answer: measure on questions you care about.

### Swap the generator

`ElfLlm` is the only place that names Gemini. Point it at another provider's OpenAI-compatible endpoint with `GEMINI_BASE_URL` and `GEMINI_MODEL`, and nothing else changes: that's what
keeping the generator separate buys you.

### Break the bar

Set the relevance bar to 0 and ask for the capital of France. What does the model say when it's handed four paragraphs about reindeer and toy safety? Then raise it until
the right questions stop working. Where's the gap?

---

## What's next

**Part 4B: Santa's Elf Assistant: Agents.** The assistant answers from the handbook, but only when it's asked. Next it decides **for itself**
when to look something up, which tool to use, and what to remember about the conversation.
