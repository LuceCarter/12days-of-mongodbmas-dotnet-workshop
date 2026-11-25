# Part 3B — Finding the Perfect Gift: Vector Search

> **Level:** Intermediate. Part 3A helps, but you only need the catalogue from Part 1.
> **Time:** About an hour for the core path.
> **Badge:** Vector Search Fundamentals (and, optionally, the [Vector Search Performance](#-stretch--vector-search-performance) stretch badge, further down)

> ⚠️ **Heads up: one step in this part has a trap that bites silently.** A vector index has to say how many
> numbers are in each vector, and Atlas will happily build one with the **wrong** number, report it
> **ready**, and then either throw an error you must read, or, if you "fix" the query instead, return **nothing at
> all**. You'll meet it in Step 4. [The trap is explained here.](#the-trap-the-number-of-dimensions)

---

## The story

Part 3A taught the Gift Finder to search for **words**. It found the dinosaurs. It forgave "dinosor".
The elf at the help desk has stopped answering from memory.

Then a letter arrives that has none of the right words in it.

*"Dear Santa. My daughter is five, she can't settle at night, and she's frightened of the dark.
Is there anything calming?"*

The elf types **calming** into the Gift Finder. It finds nothing. They try **dark**, and it offers a
torch shaped like a raptor's claw, which is not what a frightened five-year-old needs. **Bedtime** does find a
night light and a projector, but only because somebody once typed the word *bedtime* into a tag.

Mrs Claus reads the letter twice. "She hasn't asked for a toy," she says. "She's described a *feeling*.
Words can't find that. **Meaning** can."

So today you teach MongoDB to find things by what they *mean*.

---

## Get the code

Part 3B lives in the same repository as the other parts:

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

**Run every command in this challenge from that folder** — the one containing
`docker-compose.yml`. The files you'll work with:

```
part3b-vector-search/
  README.md                       this page
  Starter/GiftFinder.cs           ← you write code here
  Starter/VoyageEmbedder.cs       ← and a little here (Step 7)
  Tests/                          the tests that tell you when a step is right
  SOLUTIONS.md                    every answer, with the reasoning
```

---

## Before you start

Make sure your environment is ready. From the repository folder:

```bash
docker compose up -d
dotnet run --project tools/doctor
```

This part needs **Atlas Vector Search**, which a plain MongoDB container doesn't have. The Atlas Local
container in our `docker-compose.yml` does, and the doctor checks that it's working. If anything is red,
it tells you how to fix it. Full instructions are in [docs/setup.md](../docs/setup.md).

**You don't need an API key.** Every toy's description has already been turned into a vector, by Voyage AI's
`voyage-4` model, and so have the example questions. They're shipped in the repository. (If you do have a
Voyage key, Step 7 and the Level up use it. If you don't, nothing is lost: the tests use a stand-in.)

You'll want something to explore in the shell and Compass. Load the practice data, **with its vectors** this time:

```bash
dotnet run --project tools/seed -- --embeddings
```

It's safe to run again whenever you like. The **tests don't need it**: they use their own database,
`santas_workshop_part3b_tests`, so running them never overwrites the data you're exploring.

---

## How this works

Everything you write goes in two files:
[**`part3b-vector-search/Starter/GiftFinder.cs`**](Starter/GiftFinder.cs) and
[**`part3b-vector-search/Starter/VoyageEmbedder.cs`**](Starter/VoyageEmbedder.cs).
There are eight steps. Six are stubs, a `// TODO` and a `throw new NotImplementedException`, **one is already
written but wrong** (Step 4), and one more method, `GiftsForAgePostFilteredAsync`, is already written *on
purpose* so you can compare it with Step 6.

Run the tests whenever you like:

```bash
dotnet test part3b-vector-search/Tests
```

They start **red**. Each step turns some of them green. When the **45** tests for Steps 1 to 8 pass, you're done; the **19** more that begin "Stretch" belong to the optional [stretch badge](#-stretch--vector-search-performance).

Stuck? [`SOLUTIONS.md`](SOLUTIONS.md) has every answer with an explanation. Looking isn't
cheating — reading a worked example is how most of us learn this.

---

## What is vector search?

Search in Part 3A was about **words**: does this text contain that word, or something spelt close to it?
**Vector search** is about **meaning**: do these two pieces of text say *similar things*, even if they
share no words at all?

It works in three moves.

**1. Turn text into a vector.** A **vector** is a list of numbers. An **embedding model** is a neural
network that reads a piece of text and produces one, so that **text that means similar things gets
similar numbers**. The vector for *"a frosted triceratops that glows a gentle amber"* ends up near the one
for *"something calming at bedtime"*, though they have no word in common.

**2. Store the vectors.** In MongoDB a vector is just an array of numbers in a field, here
`descriptionEmbedding`, next to everything else about the toy.

**3. Find the nearest vectors.** Turn the *question* into a vector as well, then ask Atlas for the stored
vectors **closest** to it. Closest means *most alike in meaning*.

### What is a dimension?

The **number of numbers** in a vector is its **dimensionality**. Ours have **1024**, so each description is a
point in a 1024-dimensional space. You can't picture that, so picture two: a point on a map (two numbers:
how far east, how far north). Two places that are close on the map are close in *meaning*, and 1024 numbers
give the model room to place a description precisely.

Nothing about any one dimension is something you could name. The model decides what each one is for. Only
the **pattern across all of them** means anything.

The dimension count matters for a hard, practical reason: **every vector you compare must have the same
number of dimensions**, and the index must be told what it is. That's the trap below.

### Three ways to say "close"

How *alike* two vectors are can be measured three ways, and an index is built to use one of them:

| Similarity | Asks | Cares about **direction**? | Cares about **length**? |
|---|---|---|---|
| **cosine** | what angle is between them? | yes | **no** |
| **dotProduct** | how far does one go along the other? | yes | **yes** |
| **euclidean** | how far apart are their tips? | yes | yes |

For text, **cosine** is the usual safe choice: it asks only whether two pieces of text *point the same
way*, however long they are. You'll work all three out by hand in Step 2.

### Why a vector index at all?

Comparing a question with every stored vector is **exact** (Step 3 does it), and it's slow:
every comparison is 1024 multiplications, and you do one per document, per question. A million documents
is a billion multiplications for every search.

So Atlas builds an index called **HNSW** (Hierarchical Navigable Small World): a graph in which every vector
is linked to some of its nearest neighbours, with a few long links across the graph. A search starts
somewhere, repeatedly steps to the neighbour closest to your question, and stops when nothing nearer turns up.
It touches a tiny fraction of the vectors. The price is that it's **approximate**: it *usually* finds the true
nearest neighbours, but isn't guaranteed to. MongoDB's own documentation puts it at **around 90 to 95 per
cent** overlap with the exact answer. That's **ANN** (approximate nearest neighbour) search, and it's what
`$vectorSearch` does unless you ask for `exact: true`.

### Who does what?

This is the part people get wrong:

| | |
|---|---|
| **The embedding model** (Voyage AI, a separate service) | turns text into vectors |
| **Atlas** (MongoDB) | stores the vectors, indexes them, and finds the nearest ones |

**Atlas never makes a vector.** Your code asks the embedding model for one, and hands it to Atlas. And the rule
that follows is the most important one in this whole part: **the question must be embedded by the same model
as the data**, with the same number of dimensions. Vectors from different models are different kinds of
numbers: even when they happen to be the same length, they mean different things. Step 7 is where you write
the call, and Step 8 puts it all together.

---

## The trap: the number of dimensions

Creating a vector index asks you to state `numDimensions`: how many numbers are in each vector. Atlas
does **not** check it against your data when you create the index. It builds the index, and says **READY**.

| What you did | What happens |
|---|---|
| Declare `numDimensions: 1536` when the vectors are 1024 long | ✅ The index builds. The status says **ready**. `queryable` is true. |
| Query it with a real, 1024-number vector | ❌ An error: `vector field is indexed with 1536 dimensions but queried with 1024`. This is the helpful one: **read it**. |
| "Fix" that by querying with 1536 numbers | 🫥 **No error. No results. No clue.** Every stored vector is 1024 long, so none of them was ever indexed. |

That second row is the classic mistake: **changing the query to satisfy the index.** The index was wrong,
not the query. And note that nothing is wrong with your *data*: a vector that doesn't match the index's length
is simply skipped, quietly.

The number must equal **exactly what your embedding model produces**: not what a tutorial used and
not a round figure. `voyage-4` produces **1024** unless you ask it for another size. You can't change
`numDimensions` on an existing index: drop it and build it again.

You'll meet this in **Step 4**, where an elf has already walked into it for you.

---

## The data

The `toys` collection is the one from Part 1, with one extra field that the earlier parts never show you:

```csharp
public sealed class Toy
{
    public string Name { get; set; }                 // "Triceratops Night Light"
    public string Description { get; set; }          // "A frosted triceratops that glows a gentle amber..."
    public float[]? DescriptionEmbedding { get; set; }   // 1024 numbers: what the description MEANS
    public int MinAge { get; set; }                  // 3
    public int MaxAge { get; set; }                  // 9
    public bool InStock { get; set; }
    public string Category { get; set; }             // "electronic"
    // ... and the rest, from Part 1
}
```

`DescriptionEmbedding` is the **description** run through `voyage-4` as a *document*. (Only the description:
not the name or the tags, so that the meaning comes from the sentence.) It's generated by
[`tools/embed`](../tools/embed/Program.cs) and committed, so you never have to.

There are also **eight example questions**, such as *"something calming for a restless child at bedtime"*,
each already turned into a vector as a *query*. The tests use them, and so can you.

---

## Part A — What is a vector?

### Step 1 — Look at a vector · *objective 1*

Implement `DescribeEmbeddingAsync`: find a toy and say how many numbers its vector has, and how long it is.

```csharp
var toy = await _toys.Find(t => t.Name == toyName).FirstOrDefaultAsync();
var vector = toy?.DescriptionEmbedding ?? throw new InvalidOperationException(...);

var magnitude = Math.Sqrt(vector.Sum(x => (double)x * x));
return new EmbeddingFacts(vector.Length, magnitude);
```

Run it for the Rocking Horse. **Dimensions** is the count of numbers: the number the whole of Part 3B hinges on.
**Magnitude** is the vector's length as an arrow: square every number, add them up, take the square root.
You should see **1024** and a magnitude of **1.0**. Voyage's vectors come out already length 1: its FAQ says so
("normalized to length 1"), and we measured every one to check.
Hold on to both. Step 2 uses the second, and Step 4 uses the first.

> **Shell:** `const v = db.toys.findOne({ name: "Rocking Horse" }).descriptionEmbedding; v.length`

---

### Step 2 — Three ways to say "alike" · *objectives 1 and 4*

Implement `Compare(a, b)`: given two vectors, work out all three similarity measures by hand.

```csharp
dot        += a[i] * b[i];                      // dot product: add up the products
cosine      = dot / (|a| * |b|);                // cosine: the dot product, ignoring how long each one is
distance    = Math.Sqrt(sum of (a[i] - b[i])²); // Euclidean distance: how far apart the tips are
```

It's pure maths, with no database. And it makes the table above concrete. Take `[1, 0]` and `[2, 0]`:
they point exactly the same way, but one is twice as long. **Cosine** says **1**: identical.
**Dot product** says **2**: it rewards the longer one. **Euclidean distance** says they're **1 apart**.

> 🧠 **Which should an index use?** Whichever the embedding model was trained for, and its docs say. For
> text, **cosine** is the usual safe choice. **Dot product** is the same as cosine when every vector has
> length 1 (it's *normalised*). Yours are, as Step 1 showed (a magnitude of 1.0), so for these vectors
> cosine and dot product rank everything identically. You'll meet dot product anyway: for normalised vectors
> it's the cheaper sum.

**Atlas doesn't give you the raw number.** It rescales each so a **score is always between 0 and 1, and
higher is closer**:

| Similarity | Atlas score |
|---|---|
| cosine | `(1 + cosine) / 2` |
| euclidean | `1 / (1 + distance²)` |
| dotProduct | `(1 + dot) / 2` |

So a cosine score of **0.5 means "at right angles"**: unrelated, not "half as good". You'll check these
formulas against the real server in Step 4's tests.

A vector can only be compared with one of the **same length**. `Compare` throws if they differ, and the
server enforces the same rule. You'll meet its version in Step 4.

---

### Step 3 — Search the slow way · *objective 2*

Implement `ClosestByHandAsync`: load every toy, compare the query with each one using your `Compare`, sort,
and keep the best few.

```csharp
var everyToy = await _toys.Find(_ => true).ToListAsync();

return everyToy
    .Select(t => new ToyHit(t.Name, Compare(queryVector, t.DescriptionEmbedding!).Cosine))
    .OrderByDescending(h => h.Score)
    .Take(count)
    .ToList();
```

It works, and it's the **exact** answer: that is the definition of "closest". For 34 toys it takes no time.
For a million documents it's a billion multiplications for every question, plus a million documents dragged
over the network just to be compared. **That is what a vector index is for.** The tests in Step 5 check that
the index agrees with the answer you just wrote.

---

## Part B — The vector index

### Step 4 — Fix the elf's index · *objectives 3 and 4*

An elf wrote `CreateVectorIndexAsync` and went home. It's already in the Starter file. It runs. It builds an
index and waits for it. It reports **ready**. And nothing that queries it works.

Read the [trap](#the-trap-the-number-of-dimensions) above, then run the tests *before* you change anything,
and read what the first one says. Then find the number that doesn't match the data, and fix it. (Step 1 told
you the right answer.)

> 🧠 **`numDimensions` must equal what the embedding model produces.** The elf's number, 1536, is what a
> *different* popular model produces, and what the tutorial they copied from used. The index has no
> way to know the tutorial was wrong.

You can watch the server tell you, in the shell. Load the practice data (`dotnet run --project tools/seed -- --embeddings`)
and make a wrong index of your own:

```js
db.toys.createSearchIndex("elf-vectors", "vectorSearch", { fields: [
  { type: "vector", path: "descriptionEmbedding", numDimensions: 1536, similarity: "cosine" } ] })

const v = db.toys.findOne({ name: "Rocking Horse" }).descriptionEmbedding

db.toys.aggregate([ { $vectorSearch: { index: "elf-vectors", path: "descriptionEmbedding",
                      queryVector: v, numCandidates: 50, limit: 3 } } ])
// → "vector field is indexed with 1536 dimensions but queried with 1024"

db.toys.aggregate([ { $vectorSearch: { index: "elf-vectors", path: "descriptionEmbedding",
                      queryVector: v.concat(Array(512).fill(0)), numCandidates: 50, limit: 3 } } ])
// → no error. No results.
```

The second query is the one that makes people lose an afternoon.

The method also takes the **similarity** as a parameter (cosine unless you say otherwise), because it is baked
into the index when it's built. The tests build all three and check that each one scores the way the table in
Step 2 says. That is [the options the badge asks you to understand](#three-ways-to-say-close).

The definition also declares four **`filter`** fields (`minAge`, `maxAge`, `inStock`, `category`). They're for
Step 6: a query can only narrow by a field the index declared.

#### 🖐️ Make the same index in the Atlas UI

The badge may ask you to create the index in the UI as well as in code, and a test can't click through it
for you, so this part is on the checklist below. On a cluster in Atlas, it's the same path as in Part 3A:
**Search & Vector Search** → **Create Search Index**, but choose the **Vector Search** index type instead
of Search. Name it `toys-vectors-ui`, pick the `santas_workshop` database and the `toys` collection, choose
the **JSON Editor**, and paste the definition from `SOLUTIONS.md`. The **Visual Editor** builds the same
document with menus: try it, and compare the JSON it produces with yours.

> **Shell:**
> ```js
> db.toys.createSearchIndex("toys-vectors", "vectorSearch", { fields: [
>   { type: "vector", path: "descriptionEmbedding", numDimensions: 1024, similarity: "cosine" },
>   { type: "filter", path: "minAge" }, { type: "filter", path: "maxAge" },
>   { type: "filter", path: "inStock" }, { type: "filter", path: "category" } ] })
> ```

---

### Step 5 — Your first `$vectorSearch` · *objective 5*

Implement `FindSimilarAsync`: the toys closest to a query vector, best first.

```csharp
var options = new VectorSearchOptions<Toy>
{
    IndexName = _indexName,
    NumberOfCandidates = Math.Min(count * 20, 10_000),
};

return await _toys.Aggregate()
    .VectorSearch(t => t.DescriptionEmbedding, new QueryVector(queryVector), count, options)
    .Project<ToyHit>(HitProjection)
    .ToListAsync();
```

Print the query, and you'll see the stage you'd type in the shell:

```js
{ $vectorSearch: { index: "toys-vectors", path: "descriptionEmbedding", queryVector: [ ... ],
                   numCandidates: 100, limit: 5 } }
```

| Field | What it is |
|---|---|
| `index` | the vector index to use. Misspell it and you get **no results and no error**. |
| `path` | which field holds the vectors |
| `queryVector` | the question, as a vector: **same length, same model** as the stored ones |
| `limit` | how many results come back |
| `numCandidates` | how many neighbours the search looks at before keeping the best `limit` |

> 🧠 **`numCandidates` is the dial between speed and accuracy.** The search is approximate, so it explores the
> graph until it has *that many* candidates, then keeps the best few. Bigger looks harder and finds the true
> best more often, at a cost in time. The rules are enforced: it can't be less than `limit`, and can't exceed
> **10,000**. MongoDB's docs suggest about **20 times `limit`**: look at many, keep few.

The score comes from `{ $meta: "vectorSearchScore" }`, between 0 and 1. The tests check that **it finds the
same toys as your Step 3**, because on 34 toys the approximate answer and the exact one should agree.

Try it on the first example question (`something calming for a restless child at bedtime`) and you get:

| | Toy | Score |
|---|---|---|
| 1 | **Stegosaurus Soft Toy** | 0.7154 |
| 2 | Triceratops Night Light | 0.7032 |
| 3 | Rocking Horse | 0.7007 |
| 4 | Snakes and Ladders | 0.7003 |
| 5 | Beeswax Modelling Set | 0.6932 |

Neither of the top two descriptions says *calming*, *restless* or *child*. The soft toy is *"An extremely huggable
stegosaurus with felt plates down its back and embroidered eyes. Safe from birth."* and the night light is
*"A frosted triceratops that glows a gentle amber and fades out over twenty minutes once the room goes quiet."*
That's the model reading the **meaning**.

Notice how close the scores are: **0.7154, 0.7032, 0.7007, 0.7003**. The vector search has found the right *neighbourhood*,
but it can't say much about the order inside it, and a small night light for a restless child at bedtime is arguably a better
answer than a huggable toy for a newborn. Putting the best answer first is a different job, and it's what Stretch S1 hands to a reranking model.

> 🧠 **A score is not a percentage.** These are all in a narrow band, because every one is rescaled
> from a cosine (`(1 + cosine) / 2`) and **at right angles** already scores 0.5. What matters is the *order*,
> and the *gap* between the first and the rest. Some questions have a clear winner (*budding palaeontologist*
> puts the Dinosaur Dig Kit well ahead); some don't (*a present for someone who loves the outdoors* finds a
> torch, a glider and a telescope, all close: vector search is good, not magic).

Like `$search` in Part 3A, `$vectorSearch` must be **the first stage**.

> **Shell:**
> ```js
> db.toys.aggregate([
>   { $vectorSearch: { index: "toys-vectors", path: "descriptionEmbedding", queryVector: v, numCandidates: 100, limit: 5 } },
>   { $project: { _id: 0, name: 1, score: { $meta: "vectorSearchScore" } } }
> ])
> ```

---

## Part C — Narrowing the search

### Step 6 — Only toys a child can have · *objective 6*

A five-year-old can't be given the telescope. Implement `GiftsForAgeAsync`: the same search, but only toys
that suit the age and are in stock. Put the conditions in the **`filter`** of the `$vectorSearch` stage:

```csharp
var filter = Builders<Toy>.Filter.And(
    Builders<Toy>.Filter.Lte(t => t.MinAge, age),
    Builders<Toy>.Filter.Gte(t => t.MaxAge, age),
    Builders<Toy>.Filter.Eq(t => t.InStock, true));

var options = new VectorSearchOptions<Toy> { IndexName = _indexName, NumberOfCandidates = ..., Filter = filter };
```

This is a **pre-filter**: the search runs over **only the toys that pass it**, then returns up to `limit` of them.

Now run the other one, `GiftsForAgePostFilteredAsync`. It's already written: it searches first, **then** throws
away the unsuitable toys. Try both with the Pocket Telescope's vector (it's for ages 8 to 14), asking for toys
a **three-year-old** can have, five of them:

| | Returns |
|---|---|
| **pre-filter** (`GiftsForAgeAsync`) | **5**: Wooden Tool Bench, Rocking Horse, Elf Express Train Set, Triceratops Night Light, Stegosaurus Soft Toy |
| **post-filter** (`GiftsForAgePostFilteredAsync`) | **0**: nothing at all |

The post-filter searched first and found five toys *like a telescope*, nearly all too old for a three-year-old,
and threw **all five** away. The pre-filter never considered them. (A test checks exactly this.)

**What a pre-filter changes**, and this is objective 6:

- **Accuracy.** You get the best matches *among the toys you can actually use*, the full number asked for.
- **Not the score.** A toy scores the same filtered or not. Filtering decides who competes, not how they're marked.
- **Speed: usually slower, not faster.** MongoDB's documentation says it plainly: *filtered queries are
  typically slower than an otherwise equivalent unfiltered query*. Better results at a higher price.
- **Too restrictive is a problem.** If almost nothing qualifies there may be fewer than `limit` results
  however long it looks. **Broad filters** (an age band, in stock) suit a vector search well. A filter that
  picks out one document is a job for a plain `Find()`.
- **You pay at index time.** Only fields declared as `filter` in the index can be filtered on (Step 4):
  every one is more to build and keep. Declare what you will really filter by.

Filter on a field the index never declared and you get an error: `Path 'x' needs to be indexed as filter`.

Ask the bedtime question again for a **two-year-old** and the Triceratops Night Light (for ages 3 to 9)
disappears from the list: the soft stegosaurus, the rocking horse, the knitted snowman. The scores of the toys
that are left are exactly what they were before.

> **Shell:**
> ```js
> { $vectorSearch: { index: "toys-vectors", path: "descriptionEmbedding", queryVector: v, numCandidates: 100, limit: 5,
>     filter: { $and: [ { minAge: { $lte: 5 } }, { maxAge: { $gte: 5 } }, { inStock: true } ] } } }
> ```

---

## Part D — From a question to a gift

### Step 7 — Ask the embedding model · *objective 2*

Everything so far searched with a vector you already had. Implement `VoyageEmbedder.EmbedAsync`: turn
*text* into a vector by calling Voyage's API.

```csharp
POST https://api.voyageai.com/v1/embeddings
Authorization: Bearer <your key>

{ "input": [ "a rainy-day game" ], "model": "voyage-4", "input_type": "query", "output_dimension": 1024 }
```

The reply holds `data[0].embedding`: the vector. Return it as a `float[]`.

This is the missing half of the picture from the start of this page: **Atlas stores and searches vectors,
and a different service makes them**, and your code is the go-between.

Two things to get right:

- **`input_type`** is `"document"` for text you are *storing* and `"query"` for a *question*. Voyage prepends
  a short instruction to your text before embedding it, so the vector is shaped for its job. The toys were
  embedded as documents, so your questions are embedded as queries.
- **Check the length of what comes back.** A vector of the wrong size should fail *here*, with a message that
  names both numbers, not later in a `$vectorSearch` that errors or, worse, quietly finds nothing.

And never put the key in a message you throw. Read it from `VOYAGE_API_KEY`.

> **Which key do you have?** A key created on **voyageai.com** works with `api.voyageai.com`, which is the default.
> A key created in **MongoDB Atlas** (**AI Model APIs → Create model API key**) works with `ai.mongodb.com` instead,
> and a key scoped to a region with `us.ai.mongodb.com` or `eu.ai.mongodb.com`. Send a key to the wrong host and you
> get `403 … This API key cannot access this endpoint`. The tests never touch the network, so they don't care.
> To call Voyage for real with an Atlas key, set the host once and `VoyageEmbedder.FromEnvironment()` uses it:
> `export VOYAGE_BASE_URL="https://ai.mongodb.com"`.

> 🧠 **You don't need a key to do this step.** The tests give `EmbedAsync` a stand-in for the network, and check
> what it *sends* (the endpoint, the key header, the model, the dimensions, the input type) and how it handles a
> reply, an error and a vector of the wrong size. If you have a key, `VoyageEmbedder.FromEnvironment()` makes a real one.

> **Shell (curl):**
> ```bash
> curl https://api.voyageai.com/v1/embeddings -H "Authorization: Bearer $VOYAGE_API_KEY" -H "Content-Type: application/json" \
>   -d '{ "input": ["a rainy-day game"], "model": "voyage-4", "input_type": "query", "output_dimension": 1024 }'
> ```

---

### Step 8 — Put it together · *objectives 2 and 5*

Implement `FindGiftsAsync`: someone types a question and, maybe, an age. Embed the question as a **query**, then
search with the vector you get back, with the age filter if there is one.

```csharp
var vector = await embedder.EmbedAsync(question, "query");

return age is { } years
    ? await GiftsForAgeAsync(vector, years, count)
    : await FindSimilarAsync(vector, count);
```

**Question → embedding model → vector → `$vectorSearch` → toys.** That's the whole thing.

Notice what you *didn't* write: any code that knows what a dinosaur is, or what "calming" means. The
meaning came from the embedding model, the nearest-neighbour search came from Atlas, and the only thing
between them was a list of numbers. The same few lines would work for recipes, or support tickets, or anything
else you can describe in words.

---

## ✅ You're done

```bash
dotnet test part3b-vector-search/Tests
```

45 green (and 64 if you did the stretch too).

The elf types the letter's own words into the Gift Finder: *"something calming for a restless child at
bedtime."* The Stegosaurus Soft Toy comes first and the Triceratops Night Light second. Not one of those words is in
either description.

---

## Badge checklist — Vector Search Fundamentals

Every objective the badge assesses, and the step that teaches it.

| ✓ | Objective | Step | Taught by |
|---|---|---|---|
| ☐ | **1** Define vectors, how they work, and the purpose of dimensions | Steps 1, 2 | `DescribeEmbeddingAsync`, `Compare` |
| ☐ | **2** Explain how Atlas and an embedding model interact; HNSW at a high level | Steps 3, 7, 8 | `ClosestByHandAsync`, `EmbedAsync`, `FindGiftsAsync` |
| ☐ | **3** Create a vector search index with the correct number of dimensions | Step 4 | `CreateVectorIndexAsync` |
| ☐ | **4** Understand similarity options (euclidean, cosine, dot product) | Steps 2, 4 | `Compare`, `CreateVectorIndexAsync` |
| ☐ | **5** Write a `$vectorSearch` aggregation query | Steps 5, 6, 8 | `FindSimilarAsync`, `GiftsForAgeAsync`, `FindGiftsAsync` |
| ☐ | **6** Understand how prefiltering affects performance | Step 6 | `GiftsForAgeAsync` |
| ☐ 🖐️ | **3** …and create the index in the **Atlas UI**: a test can't click, so tick this one yourself | Step 4 | the Atlas UI walk-through |
| ☐ ⚠️ | **3** …and know **the dimensions trap**: the index is built and "ready" with the wrong number, and nothing says so | Step 4 | `CreateVectorIndexAsync` |

**Do you have to finish the exercises to take the badge?** No — the badge is open and you can
take it whenever you like. But it assesses these concepts, so doing the steps first is the
easier route.

---

## 📋 From C# to the badge

The assessment is likely to show MongoDB Shell or Atlas UI syntax rather than C#. The index definition and the
`$vectorSearch` stage are the same JSON either way. The full set lives in
[docs/csharp-to-badge.md](../docs/csharp-to-badge.md).

| What you want | Shell / Atlas | C# |
|---|---|---|
| Create a vector index | `db.toys.createSearchIndex("n", "vectorSearch", { fields: [ ... ] })` | `SearchIndexes.CreateOneAsync(new CreateSearchIndexModel("n", SearchIndexType.VectorSearch, definition))` |
| A vector field | `{ type: "vector", path, numDimensions: 1024, similarity: "cosine" }` | the same, as a `BsonDocument` |
| A filter field | `{ type: "filter", path: "minAge" }` | the same |
| Find the nearest | `{ $vectorSearch: { index, path, queryVector, numCandidates, limit } }` | `.VectorSearch(t => t.Field, new QueryVector(v), limit, options)` |
| Narrow it first | `filter: { minAge: { $lte: 5 } }` | `VectorSearchOptions { Filter = ... }` |
| Read the score | `{ $meta: "vectorSearchScore" }` | the same, in a `Project` |
| Exact, not approximate | `exact: true` (no `numCandidates`) | `VectorSearchOptions { Exact = true }` |
| Make a vector | `POST https://api.voyageai.com/v1/embeddings` | `HttpClient` |

**The rules to remember.** The query vector has the **same length** as the index's `numDimensions`.
`numCandidates` is **at least `limit`** and **at most 10,000** (and is left out for `exact: true`).
You can only filter on fields the index declares as `filter`. And a vector search, like a text search, must
be **the first stage**.

---

## 🏅 Stretch — Vector Search Performance

Optional, and a **separate badge**: everything above is the Vector Search Fundamentals badge, and nothing below is
needed for it. This is about the questions that come **after** the search works: how good are the vectors, how
much do they cost, and can the answers be better?

Some of it is code you write, with tests (**S1 to S4**). Some of it is thinking, and it's marked:

- 🛠️ **a step**: code, and tests that prove it.
- 📖 **taught, not built**: explained from MongoDB's and Voyage's documentation, with no code to run here.
- 🖐️ **try it yourself**: needs an Atlas cluster, or a key, and a test can't do it for you.

The part that was run is stated, and so is the part that wasn't. Your stretch checklist is [at the end of this section](#stretch-badge-checklist).

### How embeddings are created (generate with Voyage AI) · 🛠️ Step 7 and `tools/embed`

You wrote the call in Step 7, and the vectors the toys ship with were made by the same call, in bulk, by
[`tools/embed`](../tools/embed/Program.cs). Read it: it is about 150 lines, and it does what a production loader does.

- **One request, many texts.** All 34 descriptions go in a single call (the API takes up to 1,000 texts in a list).
- **`input_type`.** Toys are embedded as `"document"` and the example questions as `"query"`.
- **It trusts nothing it hasn't checked.** It uses each reply item's `index` rather than assuming the order, checks that
  every vector is the right length, and **writes nothing unless every vector passed**: a half-finished run can't leave
  the repository with half the data.
- **It measures.** It prints the vectors' length: every one came out at 1.0000.

### Choosing embedding models by workload / data type / quality / latency / cost · 📖

The models Voyage lists as current for text, from its documentation:

| Model | Context | Dimensions | What Voyage says it's for |
|---|---|---|---|
| `voyage-4-large` | 32,000 tokens | 1024 (default), 256, 512, 2048 | "The best general-purpose and multilingual retrieval quality." |
| `voyage-4` | 32,000 | 1024 (default), 256, 512, 2048 | "Optimized for general-purpose and multilingual retrieval quality." **← this part** |
| `voyage-4-lite` | 32,000 | 1024 (default), 256, 512, 2048 | "Optimized for latency and cost." |
| `voyage-code-4` | 32,000 | 1024 (default), 256, 512, 2048 | "Optimized for code retrieval and coding agent use cases." |
| `voyage-finance-2` | 32,000 | 1024 | "Optimized for finance retrieval and RAG." |
| `voyage-law-2` | 16,000 | 1024 | "Optimized for legal retrieval and RAG." |

Voyage's own advice is **`voyage-4-large` for the best quality, `voyage-4-lite` for the lowest latency and cost, and
`voyage-4` for a balance between the two**. It adds that the `voyage-4` series outperforms its domain-specific models,
so the finance and law models are worth testing against, not assuming. The `voyage-4` models are described as
**compatible with each other**: vectors from one can be searched with a query embedded by another, so you could embed
a big collection once with the large model and answer queries with the lite one.

How to choose, then, is five questions:

| Choose by | Ask |
|---|---|
| **Workload** | Is it search over documents (use `document`/`query` types), or something else? |
| **Data type** | Text, or code (`voyage-code-4`)? Voyage also has multimodal models, which this part doesn't cover. |
| **Quality** | Does a better model change *your* answers? The only way to know is to run your own questions through two of them. |
| **Latency** | Every query embeds the question, so the model sits on your request path. |
| **Cost** | Billed per token: for every document you embed, and for every question. Check Voyage's current pricing. |

**Doing it properly.** Embed your own example questions with two models and compare the winners: that's what the
tests in this part do for `voyage-4`, and what you'd repeat for `voyage-4-lite`. Nothing here ran that comparison: only
`voyage-4` was used.

### Compare database options for vector search · 📖

*This is design reasoning, not a benchmark: nothing here measured any other database.*

You've seen what a vector index needs: a place to store the vectors, an index to search them, and a way to **narrow
before searching** (Step 6). Options split into three families, and the honest comparison is what each one asks of you:

| | **A dedicated vector database** | **A search engine that added vectors** | **Your main database, with vector search** (what you used) |
|---|---|---|---|
| Where the vectors live | in their own system | in the search system | in the same documents as everything else |
| Keeping it in step with your data | **you** copy data across, and keep it in sync | the same, if it isn't your main store | nothing to sync: a vector is a field |
| Narrowing by other fields | only the fields you also copied | the fields indexed there | any field you declare as a `filter` (Step 6) |
| What you run | another system to run and secure | possibly one you already have | one fewer thing |
| Where it can be better | purpose-built tuning at very large scale | text and vectors together | simplicity, and data in one place |

The questions that decide it: *Where does the rest of the data live? How often does it change? Do you need to filter
or join on other fields? How big will it get, and who runs it?* For toys, recipes and tickets that already live in
MongoDB, "a vector is just a field" removes a whole class of problems (stale copies, two security models, a sync job).
For a billion vectors with no other data, the answer might differ, and you should measure it.

### Reranking, and when to use it · 🛠️ Steps S1 and S2

See [S1](#stretch-s1--ask-the-reranking-model) and [S2](#stretch-s2--search-wide-then-narrow). Vector search compares the question with
each document's vector separately. A **reranker** reads the question and a candidate **together**, which is more
accurate and costs a call per query, so you run it over a short list.

### Using Voyage reranking models · 🛠️ Step S1

`rerank-3` is Voyage's highest-accuracy current reranker, and `rerank-3-lite` is the faster, cheaper one. S1 calls them.
(Voyage's older `rerank-2.5` pages still circulate; the newer docs page lists `rerank-3` and `rerank-3-lite` as current.)

### `voyage-context-3` for chunking challenges · 📖

*Voyage's current contextualized-chunk model is `voyage-context-4`, with `voyage-context-3` its predecessor. Both
work the same way.*

The toys in this part are easy: each description stands alone. Real documents aren't. Cut a handbook into chunks and
a chunk that says *"Rule one: it must be tested by someone else"* has lost what *it* is. That is the **chunking
challenge**: a chunk embedded alone is only as good as its own words.

A **contextualized chunk embedding** model takes the chunks of one document **together** and embeds each in the
context of the others, so the vector for the "rule one" chunk knows it's about toy safety. The API takes a *list of
lists*: one inner list per document, holding that document's chunks.

```json
POST /v1/contextualizedembeddings
{ "inputs": [ [ "The Elf Handbook, chapter 3: toy safety.",
                "Rule one: every toy is tested by an elf who is not the one who made it." ] ],
  "input_type": "document", "model": "voyage-context-4" }
```

It can also chunk for you (`enable_auto_chunking`, with `chunk_size` and `chunk_overlap`), and its limits are 32,000
tokens per chunk, up to 120,000 tokens in total, and 1,000 inputs per request.

**When to use it:** when you chunk long documents, and chunks lean on their neighbours. **When not to:** short,
self-contained records like these toys, where it adds a step and no value. It's the right tool for splitting up a long
document such as the workshop's Elf Handbook, and is built to be used then. Here it is 📖 taught, not built:
`tools/voyage-check` makes one call to the endpoint so the shape above is a thing that was seen.

### Effects of dimensionality on quality, latency and cost · 🛠️ Steps S3 and S4

A vector's length is a dial. **Fewer dimensions** and **lower precision** (quantization) both make an index smaller and
faster, at some cost in accuracy. Voyage's docs say storage and search costs "scale linearly" with the dimensions and
the precision, so the sums are easy:

| One vector | Bytes | vs. a plain 1024 |
|---|---|---|
| 1024 numbers, float (what you have) | 4,096 | 1× |
| 1024, **scalar**-quantized (1 byte each) | 1,024 | 4× smaller |
| 1024, **binary**-quantized (1 bit each) | 128 | 32× smaller |
| **256** numbers, float | 1,024 | 4× smaller |
| 256, scalar | 256 | 16× smaller |
| 256, binary | 32 | 128× smaller |

(Arithmetic, from the sizes of the types. The 4× and 32× for quantization are the figures Voyage gives for its
own quantized outputs.) And what it costs in answers is measured in S3 and S4, on these toys.

### API-generated vs. database-managed auto-embeddings · 📖 🖐️

So far **you** made the vectors: you called an embedding API, stored the result and, at query time, called it again.
Atlas can instead **make them for you**: you declare the field as `autoEmbed` in the index, and Atlas calls the model, stores
the vectors, and keeps them current.

```json
{ "fields": [ { "type": "autoEmbed", "modality": "text", "path": "description", "model": "voyage-4" } ] }
```

and queries with text instead of a vector (`"query": { "text": "..." }`, with no `queryVector`). From MongoDB's docs:

| | **API-generated** (this part) | **Database-managed** (`autoEmbed`) |
|---|---|---|
| Who calls the embedding model | **your code**, for every document and every question | **Atlas**, when documents change and when you query |
| Where the vectors are | in your documents (`descriptionEmbedding`) | in an internal database Atlas manages |
| Keeping them current | **you**: re-embed when the text changes | **Atlas**: new and changed text is embedded automatically; unchanged fields aren't |
| Choice of model | any model, any provider, any dimensions | Voyage's `voyage-4` family (and `voyage-code`); fixed when the index is created |
| Changing your mind | re-embed, rebuild | create a new index: model, path, dimensions and quantization can't be edited |
| Freshness | exactly when you write | "eventually consistent": the latest data might not be queryable immediately |
| Cost | whatever your embedding calls cost | pay-as-you-go per token, for indexing, updates **and** queries |
| Where it's available | anywhere, including Atlas Local | **Atlas** (generally available, including free clusters); a *preview* for self-managed deployments. The docs don't say whether Atlas Local supports it. |

Which to pick is a question of **control against convenience**. Auto-embedding removes a pipeline: no loader, no sync
job, no query-time call in your code, and you can't forget to re-embed. In return you give up the choice of model and
the ability to see, store and compare the vectors yourself. If you need a model outside the list, vectors you can
reuse elsewhere, or full control of cost, you generate them yourself. If you want the shortest path to working
search, let the database do it.

📖 **Taught, not built, and unrun**: it needs an Atlas cluster, and there is none here. 🖐️ **Try it yourself** on a free
Atlas cluster: create an `autoEmbed` index on a copy of the toys' `description`, query it with text, and compare the answers
with the ones you got from Voyage.

### Stretch S1 — Ask the reranking model

🛠️ *Stretch objectives: reranking, and when to use it; using Voyage reranking models.*

Implement `VoyageReranker.RerankAsync`: send a question and a short list of texts, and get back the order a reranker
would put them in.

```csharp
POST https://api.voyageai.com/v1/rerank
Authorization: Bearer <your key>

{ "query": "...", "documents": [ "...", "..." ], "model": "rerank-3", "top_k": 3 }
```

The reply is `{ "data": [ { "index": 2, "relevance_score": 0.81 }, ... ] }`: **most relevant first**, and `index` is the position
of the document **in the list you sent**. Return each as a `Reranked(index, relevance_score)`.

> 🧠 **A reranker is a cross-encoder.** An embedding model reads each text on its own, and the question and each
> candidate only meet afterwards, as two vectors. A reranker reads the question **and one candidate together**, so it
> can judge how well *that* text answers *that* question. That's more accurate and much slower, and the cost is
> per candidate per question. So it's for short lists, never whole collections: Voyage's API takes at most 1,000
> documents in a call.
>
> **The score only means something next to the others from the same call.**
>
> **When to use it:** when the order inside the top few matters and the first stage isn't getting it right: answers a
> person will read one by one, or a RAG prompt where only a few chunks fit. **When not to:** when the first stage is
> already good enough, when the extra call's latency or cost matters more than a better order, or when the list is
> huge. (The docs describe the two-stage pattern but give no "when not to" guidance, so that part is reasoning, not
> quotation: measure on your own questions.)

**What it did to the bedtime question.** This is real output (`rerank-3`, the ten nearest toys by vector, best five shown):

| | Vector search (cosine) | | Reranked (relevance) |
|---|---|---|---|
| 1 | Stegosaurus Soft Toy 0.431 | → | **Triceratops Night Light** 0.636 |
| 2 | Triceratops Night Light 0.406 | → | Rocking Horse 0.569 |
| 3 | Rocking Horse 0.401 | → | Knitted Snowman 0.535 |
| 4 | Snakes and Ladders 0.401 | → | Stegosaurus Soft Toy 0.475 |
| 5 | Beeswax Modelling Set 0.386 | → | Snakes and Ladders 0.467 |

The reranker moved the night light from second to first, and dropped the soft toy from first to fourth. It also swapped the
modelling set for a snowman. Is that "better"? For *a restless child at bedtime* we think so, but that's a judgement from one
question, not a measurement: the two columns are different kinds of score, so compare the **orders**, never the numbers.
(Run `dotnet run --project tools/voyage-check` with your own key to see it for yourself.)

As in Step 7, the tests give `RerankAsync` a stand-in for the network. An Atlas-created key needs
`VOYAGE_BASE_URL=https://ai.mongodb.com`, as before.

---

### Stretch S2 — Search wide, then narrow

🛠️ *Stretch objectives: reranking, and when to use it; using Voyage reranking models.*

Implement `GiftFinder.FindThenRerankAsync(question, candidates, keep)`: a **two-stage** search. Stage one, the vector search
you wrote, finds `candidates` toys quickly. Stage two reranks their descriptions, and the best `keep` are returned.

```csharp
var firstStage = await FindSimilarAsync(vector, candidates);                // fast, approximate, wide
...
var ranked = await reranker.RerankAsync(question, documents, keep);         // slow, accurate, narrow
return ranked.Select(r => new ToyHit(names[r.Index], r.RelevanceScore)).ToList();
```

Two numbers are the whole design:

- `candidates` **too small** and the right toy never reaches the reranker. It can only **re-order** what it's shown, never
  recover a miss.
- `candidates` **too large** and you pay to read texts that had no chance.

And one thing to get right: the reranker returns **positions**, so keep the names in stage-one order and map each
`Index` back through them. Getting that wrong is the classic bug: the order looks right and the names are wrong.

The score also changes meaning, from a vector similarity to a reranker's relevance. Don't mix them in one list.

---

### Stretch S3 — Make a vector shorter

🛠️ *Stretch objectives: effects of dimensionality on quality, latency and cost.*

Implement `GiftFinder.Shorten`: keep only the **first** *n* numbers of a vector and scale them back to length 1.

```csharp
var head = vector.Take(dimensions).ToArray();
var magnitude = Math.Sqrt(head.Sum(x => (double)x * x));
return head.Select(x => (float)(x / magnitude)).ToArray();
```

Cutting a vector off normally destroys it. These aren't normal: Voyage trains its models with **Matryoshka** learning,
so the first numbers carry the most information and a vector is a **nested family**: its first 256 numbers are themselves
a valid, slightly less precise 256-number embedding, like nested dolls. Re-scaling matters because cutting makes the
arrow shorter, and dot product (and Atlas) expect length 1.

What it costs is in the answers, and the tests measure it, on these toys, with each of the eight example questions:

| Numbers kept | Questions that still find the same best toy |
|---|---|
| 1024 (all) | 8 of 8 |
| 512 | 5 of 8 |
| 256 | 5 of 8 |
| 128 | 6 of 8 |
| 64 | 3 of 8 |

Read that carefully, because it's a **trade, not a free lunch**: with the winner as the measure, cutting to a half or a
quarter loses three of the eight, and the count isn't even smooth (128 scored 6, better than 256's 5). On a 34-toy catalogue
with scores this close, one place in the order is a coin-flip, so a steadier measure is how many of the **top three**
toys survive. Averaged over the same eight questions that is **3.0** of 3 at 1024 numbers, then **2.5** at 512, **2.5** at 256,
**2.0** at 128 and **1.6** at 64. The first half of the numbers does most of the work, and it still isn't free.
Measure on **your** questions before you cut.

---

### Stretch S4 — A smaller index

🛠️ *Stretch objectives: effects of dimensionality on quality, latency and cost.*

Implement `GiftFinder.CreateCompactIndexAsync(indexName, path, dimensions, quantization)`: a vector index like Step 4's,
but pointed at any field, with any length, and optionally **quantized**.

```csharp
{ "type", "vector" }, { "path", path }, { "numDimensions", dimensions },
{ "similarity", "cosine" }, { "quantization", quantization }
```

`quantization` stores each number in less space: **`scalar`** in one byte, **`binary`** in one bit, instead of the four bytes
of a float. `binary` needs `numDimensions` to be a multiple of 8. A short index needs short vectors to match:
`StoreShortVectorsAsync` is already written, and puts a `Shorten`ed copy of every toy's vector in `descriptionEmbeddingShort`.

> 🧠 **The dimensions trap applies to the short index too.** Ask a 256-number index a 1024-number question and it
> says so (`indexed with 256 dimensions but queried with 1024`): **shorten the question** the same way you shortened the toys.
> A test shows it.

The tests check both dials on these toys: a 256-number index finds the toy whose shortened vector you ask for, and scalar and binary
indexes still put the best toy first for two of the example questions. That's a small check, not a benchmark.

---

### Stretch badge checklist

Every stretch objective, and where it's taught. 🛠️ is built and tested, and 📖 is taught from the docs and was not run here.

| ✓ | Objective | Where | Built? |
|---|---|---|---|
| ☐ | **How embeddings are created (generate with Voyage AI)** | Step 7, `tools/embed` | 🛠️ tested through a stand-in; `tools/embed` ran against the real API |
| ☐ | **Compare database options for vector search** | the comparison table | 📖 reasoning, not a benchmark |
| ☐ | **Reranking, and when to use it** | S1, S2 | 🛠️ tested through a stand-in |
| ☐ | **Choosing embedding models by workload / data type / quality / latency / cost** | the model table | 📖 only `voyage-4` was run |
| ☐ | **`voyage-context-3` for chunking challenges** | the contextualized-chunk section | 📖 one real call to the endpoint, by `tools/voyage-check` |
| ☐ | **Effects of dimensionality on quality, latency and cost** | S3, S4 | 🛠️ measured on these toys; latency not measured |
| ☐ | **API-generated vs. database-managed auto-embeddings** | the auto-embedding table | 📖 🖐️ not run: needs an Atlas cluster |
| ☐ | **Using Voyage reranking models** | S1 | 🛠️ tested through a stand-in; checked against the real API by `tools/voyage-check` |

---

## 🚀 Level up

Optional. None of this is assessed by the Vector Search Fundamentals badge. (The ground the
**Vector Search Performance** stretch badge covers is in [its own section](#-stretch--vector-search-performance).)

### Exact versus approximate

Run the same search twice, once with `exact: true` and once the normal way. Does the order agree? For the
bedtime question over 34 toys, it does. What changes with a million?

```js
{ $vectorSearch: { index: "toys-vectors", path: "descriptionEmbedding", queryVector: v, exact: true, limit: 5 } }
```

Note there is no `numCandidates`: an exact search looks at everything. MongoDB's docs warn that it's
*computationally intensive* and can hurt latency on a large collection. When is it the right choice anyway?
(When the collection is small, or when a pre-filter has left only a handful of candidates.)

### Break the filter

Write a pre-filter so restrictive that only one toy passes, and ask for five. What comes back? Then widen it
a little at a time.

---

## What's next

**Part 4A — Santa's Elf Assistant: RAG.** You can now find the right thing by what it *means*. Next, a
language model gets to read what you found, and answer the elves' questions about the workshop, in the
workshop's own words.
