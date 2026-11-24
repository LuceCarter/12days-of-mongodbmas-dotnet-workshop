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
    using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
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
**separate embedding model**, here Voyage's `voyage-3.5`, turns text into a vector, and that is a network
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
  -d '{ "input": ["a rainy-day game"], "model": "voyage-3.5", "input_type": "query", "output_dimension": 1024 }'
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
