# Part 3B — Finding the Perfect Gift: Vector Search

> **Level:** Intermediate. Part 3A helps, but you only need the catalogue from Part 1.
> **Time:** About an hour for the core path.
> **Badge:** Vector Search Fundamentals

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
`voyage-3.5` model, and so have the example questions. They're shipped in the repository. (If you do have a
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

They start **red**. Each step turns some of them green. When all 44 pass, you're done.

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
not a round figure. `voyage-3.5` produces **1024** unless you ask it for another size. You can't change
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

`DescriptionEmbedding` is the **description** run through `voyage-3.5` as a *document*. (Only the description:
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
You should see **1024** and a magnitude of **1.0**. Voyage's vectors come out already length 1
(we measured every one: the Voyage docs don't promise it, so it's worth checking on any model you use).
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
| 1 | **Triceratops Night Light** | 0.7715 |
| 2 | Constellation Projector | 0.7637 |
| 3 | Stegosaurus Soft Toy | 0.7575 |
| 4 | Beeswax Modelling Set | 0.7466 |
| 5 | Rocking Horse | 0.7463 |

The night light is first, and nothing in its description says *calming*, *restless* or *child*:
*"A frosted triceratops that glows a gentle amber and fades out over twenty minutes once the room goes quiet."*
That's the model reading the **meaning**.

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
| **pre-filter** (`GiftsForAgeAsync`) | **5**: Wooden Tool Bench, Triceratops Night Light, Rocking Horse, Magnetic Fishing Game, Stegosaurus Soft Toy |
| **post-filter** (`GiftsForAgePostFilteredAsync`) | **1**: Wooden Tool Bench |

The post-filter searched first and found five toys *like a telescope*, nearly all too old for a three-year-old,
and threw four away. The pre-filter never considered them. (A test checks exactly this.)

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

Ask the bedtime question again for a **five-year-old** and the Constellation Projector (for ages 6 to 12)
disappears from the list: the night light, the soft stegosaurus, the modelling set. The scores of the toys
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

{ "input": [ "a rainy-day game" ], "model": "voyage-3.5", "input_type": "query", "output_dimension": 1024 }
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

> **Which key do you have?** A key created on **voyageai.com** works with `api.voyageai.com`, which is what `Endpoint`
> says. A key created in **MongoDB Atlas** (**AI Model APIs → Create model API key**) works with
> `ai.mongodb.com` instead, and a key scoped to a region with `us.ai.mongodb.com` or `eu.ai.mongodb.com`.
> Send a key to the wrong host and you get `403 … This API key cannot access this endpoint`. The tests never
> touch the network, so they don't care. To call Voyage for real with an Atlas key, change the host in `Endpoint`.

> 🧠 **You don't need a key to do this step.** The tests give `EmbedAsync` a stand-in for the network, and check
> what it *sends* (the endpoint, the key header, the model, the dimensions, the input type) and how it handles a
> reply, an error and a vector of the wrong size. If you have a key, `VoyageEmbedder.FromEnvironment()` makes a real one.

> **Shell (curl):**
> ```bash
> curl https://api.voyageai.com/v1/embeddings -H "Authorization: Bearer $VOYAGE_API_KEY" -H "Content-Type: application/json" \
>   -d '{ "input": ["a rainy-day game"], "model": "voyage-3.5", "input_type": "query", "output_dimension": 1024 }'
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

44 green.

The elf types the letter's own words into the Gift Finder: *"something calming for a restless child at
bedtime."* The Triceratops Night Light comes first. Not one of those words is in its description.

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

## 🚀 Level up

Optional. None of this is assessed by the Vector Search Fundamentals badge. It's the ground that the
**Vector Search Performance** stretch badge covers, so do it if you're curious.

### Exact versus approximate

Run the same search twice, once with `exact: true` and once the normal way. Does the order agree? For the
bedtime question over 34 toys, it does. What changes with a million?

```js
{ $vectorSearch: { index: "toys-vectors", path: "descriptionEmbedding", queryVector: v, exact: true, limit: 5 } }
```

Note there is no `numCandidates`: an exact search looks at everything. MongoDB's docs warn that it's
*computationally intensive* and can hurt latency on a large collection. When is it the right choice anyway?
(When the collection is small, or when a pre-filter has left only a handful of candidates.)

### Compress the vectors

A vector index can hold its numbers at lower precision, which makes it much smaller and faster for a small
cost in accuracy. Build another index with `quantization: "scalar"` (one byte per number) or `"binary"` (one
bit; `numDimensions` must then be a multiple of 8, and 1024 is) next to the plain one. Run the same
query on each and compare the scores and the order. For the bedtime question the order doesn't change, and the
night light scores 0.7715 plain and binary, and 0.7711 under scalar: a hair of precision, in an index that stores one byte (scalar) or one bit (binary) per number instead of four bytes.

```js
{ type: "vector", path: "descriptionEmbedding", numDimensions: 1024, similarity: "cosine", quantization: "scalar" }
```

### Fewer dimensions

`voyage-3.5` can return **2048, 1024, 512 or 256** numbers (`output_dimension`). Fewer dimensions are cheaper
to store, search and send, and usually a little less precise. You would need a Voyage key to see the effect:
re-embed the toys at 256 with `new VoyageEmbedder(http, key, dimensions: 256)`, build an index with
`numDimensions: 256`, and compare. What have you *got* to change to keep the whole thing consistent? (The
question's vectors, too: a 256-number question can't search a 1024-number index.)

### Who makes the vectors?

You called an API and stored the result. Another way is to have the *database* make them. Think about what
each costs: who's responsible when the embedding model changes? What happens to the data you've already stored?
What do you pay for, and when? When would you pick each?

### Rank again

Vector search is fast and approximate. A second step, **reranking**, takes the few dozen best candidates and
asks a slower, more careful model to put them in order. When would that be worth the extra call? (This part
doesn't build it, and nothing here has been run for it.)

### Break the filter

Write a pre-filter so restrictive that only one toy passes, and ask for five. What comes back? Then widen it
a little at a time.

---

## What's next

**Part 4A — Santa's Elf Assistant: RAG.** You can now find the right thing by what it *means*. Next, a
language model gets to read what you found, and answer the elves' questions about the workshop, in the
workshop's own words.
