# Part 3B — Solutions

Every answer, with the reasoning. **Using this isn't cheating** — reading a worked example
is how most people learn a new query. Try a step first, then come here.

Each solution below is exactly what's verified by the tests.

<details>
<summary><b>Step 1 — DescribeEmbeddingAsync</b> (objective 1)</summary>

```csharp
public async Task<EmbeddingFacts> DescribeEmbeddingAsync(string toyName)
{
    var toy = await _toys.Find(t => t.Name == toyName).FirstOrDefaultAsync();

    var vector = toy?.DescriptionEmbedding
        ?? throw new InvalidOperationException($"There is no toy called '{toyName}' with a vector.");

    var magnitude = Math.Sqrt(vector.Sum(x => (double)x * x));

    return new EmbeddingFacts(vector.Length, magnitude);
}
```

A vector is nothing more exotic than a **list of numbers**, and the number of them is its
**dimensionality**. Every toy's vector has the same length, because one model made them all: **1024**.

Each dimension isn't something you could name ("dinosaur-ness"): the model learns what to put in each
one, and only the *pattern across all of them* means anything. Two descriptions that mean similar things
get patterns that point the same way.

`magnitude` is the vector's length as an arrow in 1024-dimensional space: square every number, add them
up, take the square root. It matters in Step 2, because two of the three ways of comparing vectors care
about it and one doesn't.

**Shell:** `const v = db.toys.findOne({ name: "Rocking Horse" }).descriptionEmbedding; v.length`
</details>

<details>
<summary><b>Step 2 — Compare</b> (objectives 1 and 4)</summary>

```csharp
public static SimilarityScores Compare(float[] a, float[] b)
{
    if (a.Length != b.Length)
        throw new ArgumentException(
            $"Can't compare a {a.Length}-dimension vector with a {b.Length}-dimension one.");

    double dot = 0, magnitudeA = 0, magnitudeB = 0, squaredGaps = 0;

    for (var i = 0; i < a.Length; i++)
    {
        dot += (double)a[i] * b[i];
        magnitudeA += (double)a[i] * a[i];
        magnitudeB += (double)b[i] * b[i];
        squaredGaps += ((double)a[i] - b[i]) * ((double)a[i] - b[i]);
    }

    var cosine = dot / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));

    return new SimilarityScores(cosine, dot, Math.Sqrt(squaredGaps));
}
```

Three ways to say "how alike", and they answer different questions:

| Measure | Asks | Cares about **direction**? | Cares about **length**? | Bigger means |
|---|---|---|---|---|
| **cosine** | what angle is between them? | yes | **no** | more alike |
| **dot product** | how far does one go along the other? | yes | **yes** | more alike |
| **Euclidean distance** | how far apart are their tips? | yes | yes | **less** alike |

Take `[1, 0]` and `[2, 0]`. They point exactly the same way, so cosine says **1** (identical). But
one is twice as long, so dot product says **2**, and Euclidean distance says they are **1** apart.

**Which should you pick?** Whichever the embedding model was trained for, and its documentation says. For
text, **cosine** is the usual safe choice, because it asks only whether two pieces of text *point the same
way*, however long they happen to be. Dot product is identical to cosine when every vector has length 1
(**normalised**), and quicker, which is why you will meet it.

**Atlas's score is not the raw number.** It rescales so a score is always between 0 and 1, **higher is
closer**, whichever you choose. Checked against the server, with a query of `[1, 0]`:

| Similarity | Atlas score | Checked: `[1,1]` | `[0,1]` | `[-1,0]` |
|---|---|---|---|---|
| cosine | `(1 + cosine) / 2` | 0.854 | 0.5 | 0 |
| euclidean | `1 / (1 + distance²)` | 0.5 | 0.333 | 0.2 |
| dotProduct | `(1 + dot) / 2` (for length-1 vectors) | 1 | 0.5 | 0 |

So a cosine score of 0.5 means *at right angles*: unrelated, not "half as good".

**Why throw on different lengths?** A 1024-number vector and a 1536-number one aren't different sizes of
the same thing: they are from different models, and the comparison is meaningless. The server enforces the
same rule. You will meet its version in Step 4.
</details>

<details>
<summary><b>Step 3 — ClosestByHandAsync</b> (objective 2)</summary>

```csharp
public async Task<List<ToyHit>> ClosestByHandAsync(float[] queryVector, int count)
{
    var everyToy = await _toys.Find(_ => true).ToListAsync();

    return everyToy
        .Select(t => new ToyHit(t.Name, Compare(queryVector, t.DescriptionEmbedding!).Cosine))
        .OrderByDescending(h => h.Score)
        .Take(count)
        .ToList();
}
```

This is **exact nearest-neighbour search**, and it is the definition of the problem: compare the query with
*every* vector, and keep the best. For 34 toys it takes no time. Each comparison is 1024 multiplications,
though, so a million documents means **a billion multiplications for every question**.

The way out is to give up a little certainty for a lot of speed. Atlas builds a graph called **HNSW**
(Hierarchical Navigable Small World), where each vector is linked to some of its nearest neighbours,
with a few long links across the graph on top. A search starts somewhere, repeatedly steps to whichever
neighbour is closer to the query, and stops when nothing nearer turns up. It touches a small fraction of
the vectors and almost always lands on the right neighbourhood. That is **approximate nearest neighbour**
(ANN) search: not guaranteed to find the true best match, but typically around 90 to 95 per cent
overlap with the exact answer, at a fraction of the cost.

You can have the exact version from the database too: `exact: true`. See Level up.
</details>

<details>
<summary><b>Step 4 — CreateVectorIndexAsync</b> (objectives 3 and 4)</summary>

```csharp
public async Task CreateVectorIndexAsync(string similarity = "cosine")
{
    var definition = new BsonDocument("fields", new BsonArray
    {
        new BsonDocument
        {
            { "type", "vector" },
            { "path", "descriptionEmbedding" },
            { "numDimensions", 1024 },
            { "similarity", similarity },
        },

        // Fields a query can narrow by BEFORE it searches (Step 6). Every one has to be declared.
        new BsonDocument { { "type", "filter" }, { "path", "minAge" } },
        new BsonDocument { { "type", "filter" }, { "path", "maxAge" } },
        new BsonDocument { { "type", "filter" }, { "path", "inStock" } },
        new BsonDocument { { "type", "filter" }, { "path", "category" } },
    });

    await _toys.SearchIndexes.CreateOneAsync(
        new CreateSearchIndexModel(_indexName, SearchIndexType.VectorSearch, definition));

    await WaitUntilQueryableAsync(_indexName);
}
```

The elf's number was **1536**. That is what a different, popular embedding model produces, and it is what
the tutorial the elf copied from used. Our toys' vectors are **1024** long. The fix is one number, and the
lesson is where the number comes from: **`numDimensions` must equal exactly what your embedding model
produces.** Not what a tutorial used, and not a round number. Step 1 told you the answer.

**The index gives no warning.** It builds, its status says **READY**, and `queryable` is true. And then:

| What you query with | What you get |
|---|---|
| a 1024-number vector (from the real model) | ❌ an error: `vector field is indexed with 1536 dimensions but queried with 1024` |
| a 1536-number vector (to make that error go away) | 🫥 **no error and no results**: every stored vector is 1024 long, so none of them was ever indexed |

The first is the helpful one: **read the error**; it tells you the index's number. The second is the
nasty one, and it is the classic mistake: change the query to satisfy the index and the search silently
returns nothing, because the *documents* were never wrong-sized for the index. The index was.
A vector whose length doesn't match is simply skipped.

(Because it is a different index, there is no "alter". Drop it and create it again, with the right number.)

**Similarity** is the other half of the definition: `cosine`, `euclidean` or `dotProduct`, which Step 2
explained. It is baked into the index when it's built, which is why the method takes it as a
parameter: change your mind later and you build a new index.

**The `filter` fields** are for Step 6. A query can narrow by a field *only if the index declares it*.
Ask for one it doesn't, and you get an error: `Path 'cat' needs to be indexed as filter`.

**Shell:**
```js
db.toys.createSearchIndex("toys-vectors", "vectorSearch", { fields: [
  { type: "vector", path: "descriptionEmbedding", numDimensions: 1024, similarity: "cosine" },
  { type: "filter", path: "minAge" }, { type: "filter", path: "maxAge" },
  { type: "filter", path: "inStock" }, { type: "filter", path: "category" } ] })
```
</details>

<details>
<summary><b>Step 5 — FindSimilarAsync</b> (objective 5)</summary>

```csharp
public async Task<List<ToyHit>> FindSimilarAsync(float[] queryVector, int count)
{
    var options = new VectorSearchOptions<Toy>
    {
        IndexName = _indexName,
        NumberOfCandidates = Math.Min(count * 20, 10_000),
    };

    return await _toys.Aggregate()
        .VectorSearch(t => t.DescriptionEmbedding, new QueryVector(queryVector), count, options)
        .Project<ToyHit>(HitProjection)
        .ToListAsync();
}
```

Printed, it is the stage you would type in the shell:

```js
{ $vectorSearch: { index: "toys-vectors", path: "descriptionEmbedding", queryVector: [ ... ],
                   numCandidates: 100, limit: 5 } }
```

The five fields to know:

| Field | What it is |
|---|---|
| `index` | the vector index to use. Misspell it and you get **no results and no error**. |
| `path` | which field holds the vectors |
| `queryVector` | the question, as a vector. **Same length, and from the same model**, as the stored ones. |
| `limit` | how many results to return |
| `numCandidates` | how many neighbours the search looks at before choosing the best `limit` |

**`numCandidates` is the dial between speed and accuracy.** The search is approximate: it explores the
graph until it has *that many* candidates, then keeps the best few. Bigger looks harder and finds the true
best more often, at a cost in time. The rules are enforced: it can't be less than `limit`, and can't exceed
10,000. The docs suggest **about 20 times `limit`**: look at many, keep few.

Neither is optional: leave `numCandidates` out of an approximate search and you get
`numCandidates is required for approximate vector search`.

`$vectorSearch` must be **the first stage** in the pipeline, like `$search` in Part 3A, and the score
comes from `{ $meta: "vectorSearchScore" }`.

**Shell:**
```js
db.toys.aggregate([
  { $vectorSearch: { index: "toys-vectors", path: "descriptionEmbedding", queryVector: v, numCandidates: 100, limit: 5 } },
  { $project: { _id: 0, name: 1, score: { $meta: "vectorSearchScore" } } }
])
```
</details>

<details>
<summary><b>Step 6 — GiftsForAgeAsync</b> (objective 6)</summary>

```csharp
public async Task<List<ToyHit>> GiftsForAgeAsync(float[] queryVector, int age, int count)
{
    var filter = Builders<Toy>.Filter.And(
        Builders<Toy>.Filter.Lte(t => t.MinAge, age),
        Builders<Toy>.Filter.Gte(t => t.MaxAge, age),
        Builders<Toy>.Filter.Eq(t => t.InStock, true));

    var options = new VectorSearchOptions<Toy>
    {
        IndexName = _indexName,
        NumberOfCandidates = Math.Min(count * 20, 10_000),
        Filter = filter,
    };

    return await _toys.Aggregate()
        .VectorSearch(t => t.DescriptionEmbedding, new QueryVector(queryVector), count, options)
        .Project<ToyHit>(HitProjection)
        .ToListAsync();
}
```

**This is a pre-filter.** The `filter` is part of the `$vectorSearch` stage, so the search runs **over only
the toys that pass it**, and still returns up to `count` of them.

Compare `GiftsForAgePostFilteredAsync`, already written: search first, then throw away the unsuitable
toys. Whatever it finds, it finds **before** filtering. Ask for 5, and if three of those are the wrong age
you are left with **two**. A pre-filter would have kept looking and handed you five.

What a pre-filter changes:

- **Accuracy.** You get the best matches *among the toys you can actually use*, the full number asked for.
- **The score doesn't change.** A toy scores the same filtered or not: filtering decides who competes, not
  how they are marked.
- **Speed: usually slower, not faster.** The docs say plainly: *filtered queries are typically slower than
  an otherwise equivalent unfiltered query*. The result can be better and the query can be slower, and
  **that is the trade**.
- **A filter that is too restrictive is a problem of its own**: if almost nothing qualifies, there may be
  fewer than `count` results however long it looks. Broad filters (an age band, in-stock) work well; a
  filter that picks out one document is a job for a plain `Find()`, not a vector search.

You can only filter on fields the index declares as `filter` (Step 4), which is also a performance
decision: every filter field is more index to build and hold.

**Shell:**
```js
{ $vectorSearch: { index: "toys-vectors", path: "descriptionEmbedding", queryVector: v, numCandidates: 100, limit: 5,
    filter: { $and: [ { minAge: { $lte: 5 } }, { maxAge: { $gte: 5 } }, { inStock: true } ] } } }
```
</details>

<details>
<summary><b>Step 7 — EmbedAsync</b> (objective 2)</summary>

```csharp
public async Task<float[]> EmbedAsync(string text, string inputType)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
    {
        Content = new StringContent(
            new JsonObject
            {
                ["input"] = new JsonArray(text),
                ["model"] = _model,
                ["input_type"] = inputType,
                ["output_dimension"] = _dimensions,
            }.ToJsonString(),
            Encoding.UTF8,
            "application/json"),
    };
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

    using var response = await _http.SendAsync(request);
    var body = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
        throw new HttpRequestException(
            $"Voyage answered {(int)response.StatusCode} {response.ReasonPhrase}: {body}", null, response.StatusCode);

    var vector = JsonNode.Parse(body)!["data"]![0]!["embedding"]!.AsArray()
        .Select(number => number!.GetValue<float>()).ToArray();

    if (vector.Length != _dimensions)
        throw new InvalidOperationException(
            $"Voyage returned {vector.Length} dimensions but the index expects {_dimensions}. " +
            "A vector of a different length can't be searched against it.");

    return vector;
}
```

**This is the missing half of the picture.** Atlas stores and searches vectors; it doesn't make them. A
**separate embedding model**, here Voyage's `voyage-4`, turns text into a vector, and that is a network
call to a different service. The two never talk to each other: **your code is the go-between**.

The rule that follows is the most important one in vector search: **the question must be embedded by the
same model as the data**, producing the same number of dimensions. A vector from a different model is a
list of numbers of the wrong kind: even at the right length, the numbers mean different things.

**`input_type`** tells Voyage whether the text is a **`document`** being stored or a **`query`** being
searched with. For both it quietly prepends a short instruction to your text before embedding it, so the
vector is shaped for its job: retrieval models work best when questions and documents are embedded
slightly differently. Embeddings made with and without it are compatible, but use it: the toy
descriptions were embedded as `document`, so questions are embedded as `query`.

**Why check the length?** Because this is where a dimension mismatch is cheapest to catch: here, with a
message that names both numbers, rather than in a `$vectorSearch` that errors or, worse, returns nothing.

**Keys.** The key goes in a header, from an environment variable, and **never** in a message you
throw. `FromEnvironment()` reads `VOYAGE_API_KEY`, and the exercises never need it: the tests supply a
stand-in for the network.

**Shell (curl):**
```bash
curl https://api.voyageai.com/v1/embeddings -H "Authorization: Bearer $VOYAGE_API_KEY" -H "Content-Type: application/json" \
  -d '{ "input": ["a rainy-day game"], "model": "voyage-4", "input_type": "query", "output_dimension": 1024 }'
```
</details>

<details>
<summary><b>Step 8 — FindGiftsAsync</b> (objectives 2 and 5)</summary>

```csharp
public async Task<List<ToyHit>> FindGiftsAsync(string question, int? age, int count)
{
    var embedder = _embedder
        ?? throw new InvalidOperationException("FindGiftsAsync needs an embedder to turn the question into a vector.");

    var vector = await embedder.EmbedAsync(question, "query");

    return age is { } years
        ? await GiftsForAgeAsync(vector, years, count)
        : await FindSimilarAsync(vector, count);
}
```

Everything you have built, in one method: **question → embedding model → vector → `$vectorSearch` → toys**.

Notice what you *didn't* write: any code that knows what a dinosaur is. The meaning came from the
embedding model, the nearest-neighbour maths came from Atlas, and the only thing in between was a
vector. That is why the same few lines work for toys, for recipes and for support tickets.

The question is embedded with `"query"`, the toys were embedded with `"document"` (Step 7).
</details>

---

# Stretch — Vector Search Performance

<details>
<summary><b>Stretch S1 — RerankAsync</b> (reranking, and when to use it; using Voyage reranking models)</summary>

```csharp
public async Task<List<Reranked>> RerankAsync(string query, IReadOnlyList<string> documents, int topK)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
    {
        Content = new StringContent(
            new JsonObject
            {
                ["query"] = query,
                ["documents"] = new JsonArray(documents.Select(d => (JsonNode)JsonValue.Create(d)!).ToArray()),
                ["model"] = _model,
                ["top_k"] = topK,
            }.ToJsonString(),
            Encoding.UTF8,
            "application/json"),
    };
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

    using var response = await _http.SendAsync(request);
    var body = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
        throw new HttpRequestException(
            $"Voyage answered {(int)response.StatusCode} {response.ReasonPhrase}: {body}", null, response.StatusCode);

    return JsonNode.Parse(body)!["data"]!.AsArray()
        .Select(row => new Reranked(row!["index"]!.GetValue<int>(), row["relevance_score"]!.GetValue<double>()))
        .ToList();
}
```

A reranker is a different kind of model from an embedding model, and the difference is the whole point.
An **embedding** model reads each text **on its own** and turns it into a vector; the question and each
candidate only meet afterwards, as two vectors being compared. A **reranker** is a **cross-encoder**: it reads the
question and one candidate **together** and judges how well *that* text answers *that* question.
Reading them together is more accurate and much more expensive, because it has to be redone for every candidate
and every question, so you never run it over a whole collection. You run it over the few dozen candidates a cheap
first stage already picked.

What comes back is a list of `{ index, relevance_score }`, **most relevant first**. The `index` is the
position of the document **in the list you sent**, which is why S2 has to keep the candidates in order.

**The score is only meaningful next to the others from the same call.** Don't compare a score from one query with
a score from another, and don't set a threshold from one example.

The request limits are worth knowing: at most **1,000 documents** per request, and a cap on total tokens, so a
reranker is a *short-list* tool by design.

**Which model?** `rerank-3` is the highest-accuracy current model; `rerank-3-lite` is described as fast and
cost-effective for latency-sensitive uses. The same trade as the embedding models: quality against speed and cost.

**When to rerank, and when not to.** Rerank when the *order within the top few* matters and the first stage's
order isn't good enough: a question the embeddings only half understand, answers a person will read one by one,
or a RAG prompt where only a few chunks fit. Don't when the first stage is already good enough for the task, when
the extra call's latency or cost matters more than a better order, or when the candidate list is huge (rerank the
top handful, never the collection). The docs state the two-stage pattern but give no "when not" guidance, so
that part is reasoning, not a quotation: measure on your own questions.

**Shell (curl):**
```bash
curl https://api.voyageai.com/v1/rerank -H "Authorization: Bearer $VOYAGE_API_KEY" -H "Content-Type: application/json" \
  -d '{ "query": "something calming for bedtime", "documents": ["...", "..."], "model": "rerank-3", "top_k": 3 }'
```
</details>

<details>
<summary><b>Stretch S2 — FindThenRerankAsync</b> (reranking, and when to use it; using Voyage reranking models)</summary>

```csharp
public async Task<List<ToyHit>> FindThenRerankAsync(string question, int candidates, int keep)
{
    var embedder = _embedder
        ?? throw new InvalidOperationException("FindThenRerankAsync needs an embedder to turn the question into a vector.");
    var reranker = _reranker
        ?? throw new InvalidOperationException("FindThenRerankAsync needs a reranker to put the candidates in order.");

    // Stage one: fast, approximate, and wide.
    var vector = await embedder.EmbedAsync(question, "query");
    var firstStage = await FindSimilarAsync(vector, candidates);

    // The reranker reads text, so give it each candidate's description, in the order stage one returned them:
    // it will say which one it means by that position.
    var names = firstStage.Select(h => h.Name).ToList();
    var descriptionOf = (await _toys.Find(t => names.Contains(t.Name)).ToListAsync())
        .ToDictionary(t => t.Name, t => t.Description);
    var documents = names.Select(n => descriptionOf[n]).ToList();

    // Stage two: slow, accurate, and narrow.
    var ranked = await reranker.RerankAsync(question, documents, keep);

    return ranked.Select(r => new ToyHit(names[r.Index], r.RelevanceScore)).ToList();
}
```

**Wide, then narrow.** Stage one casts a net (`candidates`, say 20 to 50) and stage two chooses (`keep`, say 3 to 5).
The two numbers are the whole design:

- `candidates` too small and the right answer never reaches the reranker, which can't fix what it never sees.
  The reranker can only reorder, never recover a miss.
- `candidates` too large and you pay for reading texts that had no chance.

**The map back.** The reranker returns *positions*, so the code keeps `names` in stage-one order and maps each
`Index` back through it. Getting that wrong is the classic bug: the order is right and the names are wrong.

Note the score changes meaning: it started as a vector similarity and ends as a reranker's relevance. They are
different scales, so never mix them in one list.
</details>

<details>
<summary><b>Stretch S3 — Shorten</b> (effects of dimensionality on quality, latency and cost)</summary>

```csharp
public static float[] Shorten(float[] vector, int dimensions)
{
    if (dimensions < 1 || dimensions > vector.Length)
        throw new ArgumentOutOfRangeException(nameof(dimensions),
            $"A {vector.Length}-dimension vector can't be shortened to {dimensions}.");

    var head = vector.Take(dimensions).ToArray();
    var magnitude = Math.Sqrt(head.Sum(x => (double)x * x));

    return head.Select(x => (float)(x / magnitude)).ToArray();
}
```

Cutting a vector off after *n* numbers normally destroys it. These aren't normal vectors: Voyage trains its models with
**Matryoshka** learning, so the first numbers carry the most important information and the vector is a **nested
family**: the first 256 numbers are themselves a valid 256-number embedding, the first 512 a valid 512 one, and so on,
like nested dolls. The cost of cutting is "a slight loss of retrieval quality", in the docs' words.

**Why renormalise?** Chopping numbers off makes the arrow shorter. Cosine similarity ignores length, but dot
product doesn't, and Atlas expects unit vectors for it. Scaling back to length 1 keeps every similarity honest.
(`[3, 4, 12]` cut to two numbers is `[3, 4]`, length 5, and dividing by 5 gives `[0.6, 0.8]`.)

**What it buys.** The docs say storage and search costs "scale linearly in the embedding dimensionality and
precision". A quarter of the dimensions is a quarter of the numbers to store, to compare, and to send over the
network, for every vector. The cost is a little accuracy, and it is **a trade, not a win**: for a collection of 34
toys it makes no visible difference to the answers. Across millions of vectors the saving adds up to something you
can feel in memory, in latency and in the bill.

**You can ask Voyage for a short vector directly** (`output_dimension: 256`), and `tools/voyage-check` compares that
with truncating a 1024-number vector, so you can see whether the two agree.
</details>

<details>
<summary><b>Stretch S4 — CreateCompactIndexAsync</b> (effects of dimensionality on quality, latency and cost)</summary>

```csharp
public async Task CreateCompactIndexAsync(string indexName, string path, int dimensions, string quantization = "none")
{
    var definition = new BsonDocument("fields", new BsonArray
    {
        new BsonDocument
        {
            { "type", "vector" },
            { "path", path },
            { "numDimensions", dimensions },
            { "similarity", "cosine" },
            { "quantization", quantization },
        },
    });

    await _toys.SearchIndexes.CreateOneAsync(
        new CreateSearchIndexModel(indexName, SearchIndexType.VectorSearch, definition));

    await WaitUntilQueryableAsync(indexName);
}
```

Two ways to make an index smaller, and this one method does both:

| Way | How | What each number costs |
|---|---|---|
| **fewer dimensions** | `path` = the short vectors, `numDimensions` = 256 | 256 numbers instead of 1024 |
| **quantization** | `quantization: "scalar"` or `"binary"` | 1 byte, or 1 *bit*, instead of the 4 bytes of a float |

They combine. Compare a float32 vector of 1024 numbers (**4,096 bytes**) with the same vector scalar-quantized (**1,024 bytes**,
the docs say 4x less) or binary-quantized (**128 bytes**, 32x less), or a 256-number one (1,024 bytes as floats, **256** as scalar,
**32** as binary).

**The dimensions trap applies to the short index too.** `numDimensions` must be the length of what's stored *and* of what
you query with: a 1024-number question against the 256-number index gets the same
`indexed with 256 dimensions but queried with 1024` error as in Step 4. Shorten the question the same way you shortened the toys.

**Binary** needs `numDimensions` to be a multiple of 8, because it packs eight numbers into a byte (1024 and 256 both are).

**What it costs in accuracy.** Quantizing throws away precision, so scores shift slightly (the night light scored 0.7715
plain and 0.7711 under scalar for the bedtime question) and, on a big collection, the order of near-ties can change.
Whether that matters is something to measure against *your* questions, not assume.

**Shell:**
```js
db.toys.createSearchIndex("toys-compact", "vectorSearch", { fields: [
  { type: "vector", path: "descriptionEmbedding", numDimensions: 1024, similarity: "cosine", quantization: "scalar" } ] })
```
</details>

---

## Still red?

**All of them failing with a connection error.** MongoDB isn't running:
```bash
docker compose up -d
dotnet run --project tools/doctor
```

**Every test says the vectors aren't in the build.** They are generated by `tools/embed` and committed.
Pull the latest. (If you *are* regenerating them, `export VOYAGE_API_KEY=...` first.)

**Step 4: "indexed with 1536 dimensions but queried with 1024".** That is the bug you were asked to find. The
number in the definition must be the length of the vectors: 1024.

**Step 4 passes but Step 5 finds nothing.** Is the index you're querying the one you fixed? Dimensions
can't be edited: drop it (`db.toys.dropSearchIndex("toys-vectors")`) and create it again.

**Step 5 or 6: "numCandidates … limit".** `numCandidates` must be at least `limit`, and at most 10,000.

**Step 6 returns fewer than you asked for.** Is the filter inside the `$vectorSearch` options, rather than a
`Match` after it? That second one is the post-filter.

**Step 6: "needs to be indexed as filter".** The field isn't declared as a `filter` field in the index
definition from Step 4.

**Step 7 fails on the dimensions.** Check you send `output_dimension` and that the length check compares to `_dimensions`.
