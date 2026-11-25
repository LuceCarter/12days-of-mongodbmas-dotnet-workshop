# From C# to the badge

The badges assess MongoDB concepts, and their questions tend to use the **MongoDB
Shell**, **Compass** or the **Atlas UI** — not C#. This file translates what you write
in these challenges into what you'll see in an assessment.

Each part's README has the relevant slice of this table inline. This is the full set.

> **Filter style.** We lead with `Builders<Toy>` because it maps almost one-to-one onto
> shell syntax, which is what the assessment uses. The LINQ equivalent is shown
> alongside because it's often what you'd actually write in a C# codebase.

---

## Part 1 — CRUD

| What you want | Builders (C#) | LINQ (C#) | Shell |
|---|---|---|---|
| Insert one | `toys.InsertOneAsync(toy)` | same | `db.toys.insertOne({ ... })` |
| Find one by equality | `toys.Find(Builders<Toy>.Filter.Eq(t => t.Name, "Rocking Horse"))` | `toys.Find(t => t.Name == "Rocking Horse")` | `db.toys.findOne({ name: "Rocking Horse" })` |
| Set a field | `Builders<Toy>.Update.Set(t => t.Price, 79.99m)` | — | `{ $set: { price: 79.99 } }` |
| Delete many | `toys.DeleteManyAsync(Builders<Toy>.Filter.Eq(t => t.Discontinued, true))` | `toys.DeleteManyAsync(t => t.Discontinued)` | `db.toys.deleteMany({ discontinued: true })` |
| Comparison | `Builders<Toy>.Filter.Lt(t => t.Price, 20m)` | `t => t.Price < 20m` | `{ price: { $lt: 20 } }` |
| Logical AND | `Builders<Toy>.Filter.And(a, b)` | `t => a && b` | `{ $and: [ ... ] }` |
| Logical OR | `Builders<Toy>.Filter.Or(a, b)` | `t => a \|\| b` | `{ $or: [ ... ] }` |
| **Array equality** | `Builders<Toy>.Filter.AnyEq(t => t.Tags, "dinosaur")` | `t => t.Tags.Contains("dinosaur")` | `{ tags: "dinosaur" }` |
| Sort and limit | `.SortBy(t => t.Price).Limit(5)` | same | `.sort({ price: 1 }).limit(5)` |
| Projection | `.Project(t => new { t.Name, t.Price })` | same | `.project({ name: 1, price: 1, _id: 0 })` |
| Count with a filter | `toys.CountDocumentsAsync(filter)` | same | `db.toys.countDocuments(filter)` |

### Watch out: array equality

The shell query `{ tags: "dinosaur" }` is a plain equality against an **array** field.
MongoDB matches the scalar against *any element* of the array — no special operator is
involved. That is the concept the badge is testing.

The typed C# builder can't express it as `Eq`, because `Tags` is a `List<string>` while
`"dinosaur"` is a `string`:

```csharp
Builders<Toy>.Filter.Eq(t => t.Tags, "dinosaur")     // ❌ does not compile (CS1660)
Builders<Toy>.Filter.AnyEq(t => t.Tags, "dinosaur")  // ✅
```

So `AnyEq` is a **C# typing detail, not a different MongoDB query**. Both send
`{ tags: "dinosaur" }` over the wire. Don't let `AnyEq` convince you the shell needs
something like `$elemMatch` — for a single scalar value, it doesn't.

### Ascending and descending

`SortBy` / `SortByDescending` map to `1` and `-1`:

```csharp
.SortBy(t => t.Price)            // { price: 1 }
.SortByDescending(t => t.Price)  // { price: -1 }
```

### Seeing the query your C# actually sent

Handy whenever an assessment question looks unfamiliar — render the filter and compare
it with the shell syntax:

```csharp
var filter = Builders<Toy>.Filter.AnyEq(t => t.Tags, "dinosaur");

Console.WriteLine(filter.Render(new RenderArgs<Toy>(
    BsonSerializer.SerializerRegistry.GetSerializer<Toy>(),
    BsonSerializer.SerializerRegistry)));

// => { "tags" : "dinosaur" }
```

---

## Part 2 — Aggregation

The assessment shows **pipelines**, usually in the shell or built in Compass. The good news: in
these challenges you write each stage as JSON, so the stage is **identical** in C# and in the shell.

| What you want | Shell / Compass | C# (JSON stage) | C# (typed builder) |
|---|---|---|---|
| Run a pipeline | `db.orders.aggregate([ … ])` | `_orders.Aggregate(pipeline).ToListAsync()` | `_orders.Aggregate().Match(…)…ToListAsync()` |
| Filter | `{ $match: { season: "current" } }` | same | `.Match(o => o.Season == "current")` |
| One document per array element | `{ $unwind: "$lines" }` | same | `.Unwind(…)` |
| Group and add up | `{ $group: { _id: "$child", points: { $sum: "$points" } } }` | same | `.Group(…)` |
| Reshape or compute | `{ $project: { _id: 0, child: "$_id", points: 1 } }` | same | `.Project(…)` |
| Sort | `{ $sort: { points: -1 } }` | same | `.SortByDescending(e => e.Points)` |
| Limit | `{ $limit: 5 }` | `new BsonDocument("$limit", count)` | `.Limit(5)` |
| Join another collection | `{ $lookup: { from: "toys", localField: "lines.toy", foreignField: "name", as: "toy" } }` | same | `.Lookup(…)` |
| Add fields | `{ $set: { total: … } }` | same | `.Set(…)` |
| Count what arrives | `{ $count: "openOrders" }` | same | `.Count()` |
| Explain a pipeline | `db.orders.explain("executionStats").aggregate([ … ])` | `RunCommandAsync` with an `explain` command | — |
| Explain in Compass | **Aggregations** tab → **Explain** | — | — |

A stage is either a JSON string (when it holds fixed values) or a `BsonDocument` (when it holds a
variable): `"""{ $unwind: "$lines" }"""` versus `new BsonDocument("$limit", count)`. Both end up
as the same stage on the wire.

### Watch out: never glue a variable into a stage string

```csharp
$$"""{ $match: { "lines.toy": "{{toy}}" } }"""            // ❌ a quote in `toy` breaks out of the string
new BsonDocument("$match", new BsonDocument("lines.toy", toy))   // ✅ the value stays data
```

With a toy called `Kite, "Red" Dragon` the first throws a `FormatException`; with `x", "region": "Asia`
it silently adds a second condition to your stage. This is the aggregation version of SQL
injection. Fixed values can be JSON; variables go in a `BsonDocument`.

### Watch out: `$count` outputs nothing when nothing matches

`{ $count: "openOrders" }` doesn't emit `{ openOrders: 0 }` for an empty input — it emits **no
document at all**. In C#, `FirstOrDefaultAsync()` gives you `null`, so `result["openOrders"]` throws.
Treat "no document" as zero. (`countDocuments()`, from Part 1, doesn't have this quirk.)

### Watch out: filters and expressions are different grammars

| Inside | Greater-than-or-equal is written | Field names |
|---|---|---|
| `$match` (a **query filter**) | `{ points: { $gte: 0 } }` | bare: `points` |
| `$project`, `$set`, `$group` (an **aggregation expression**) | `{ $gte: ["$points", 0] }` | a leading dollar: `"$points"` |

### Watch out: `$lookup` returns an array, even for one match

`as: "toy"` gives `toy: [ { …the toy… } ]`. A second `$unwind: "$toy"` turns that into the toy itself.
If nothing matched, `toy` is an **empty array** and the `$unwind` **drops the document**: the line
vanishes silently. (`preserveNullAndEmptyArrays: true` keeps it.)

### Watch out: after `$group`, only the key and the accumulators exist

`$group` doesn't pass documents through — it outputs brand-new ones containing just `_id` and
whatever you accumulated. A later `$match` on a field you didn't carry through finds nothing, with
no error. That is why stage order is a *correctness* matter, not only a speed one.

### Reading an explain plan

| You see | It means |
|---|---|
| `COLLSCAN` | no index helped: the server read every document |
| `IXSCAN` | it walked an index (usually under a `FETCH` that reads the matching documents) |
| `COUNT_SCAN` | a count answered from the index alone, without reading documents |
| `totalDocsExamined` | how many documents it read |
| `nReturned` | how many it handed on |

`totalDocsExamined` far above `nReturned` means it read a lot to find a little — what an index fixes.
The shape of explain output varies with the pipeline (a lone `$match` has a top-level
`queryPlanner`; a longer one nests it in `stages[0].$cursor`), so look for these names anywhere in
the document rather than at a fixed path.

### Seeing what your C# actually sent

```csharp
Console.WriteLine(pipeline);
// => [{ "$match" : { "season" : "current", "status" : "open" } }, { "$unwind" : "$lines" }]
```

Works on a `PipelineDefinition` and on a typed `Aggregate()` chain, and prints the LINQ pipeline
too. Compare it with what you'd type in the shell.

## Part 3A — Search

An Atlas Search *index definition* is JSON, and a `$search` stage is JSON. The driver's typed builders
(`Builders<Toy>.Search`) print exactly the JSON you'd type in the shell, so `Console.WriteLine(query)`
shows you the shell version of any query you wrote in C#.

| What you want | Shell / Atlas | C# |
|---|---|---|
| Create a dynamic index | `db.toys.createSearchIndex("n", { mappings: { dynamic: true } })` | `await toys.SearchIndexes.CreateOneAsync(new CreateSearchIndexModel("n", definition))` |
| Create a static index | `{ mappings: { dynamic: false, fields: { ... } } }` | the same JSON in `BsonDocument.Parse(...)` |
| List indexes, check `queryable` | `db.toys.getSearchIndexes("n")` | `await toys.SearchIndexes.ListAsync("n")` |
| Drop or update an index | `dropSearchIndex("n")`, `updateSearchIndex("n", def)` | `SearchIndexes.DropOneAsync`, `UpdateAsync` |
| Words in a field | `{ $search: { index, text: { query, path } } }` | `.Search(Builders<Toy>.Search.Text(t => t.Description, q), indexName: "n")` |
| Several fields | `path: ["name", "description"]` | `Search.Text(new[] { "name", "description" }, q)` |
| Exact value | `equals: { path, value }` | `Search.Equals(t => t.Category, "puzzle")` |
| Range | `range: { path, gte, lte }` | `Search.Range(t => t.MinAge, SearchRangeBuilder.Lte(6))` |
| Combine clauses | `compound: { must, should, filter, mustNot }` | `Search.Compound().Must(...).Filter(...)` |
| Boost one clause | `score: { boost: { value: 5 } }` | `new SearchTextOptions<Toy> { Score = new SearchScoreDefinitionBuilder<Toy>().Boost(5) }` |
| Tolerate typos | `fuzzy: { maxEdits: 2 }` | `new SearchTextOptions<Toy> { Fuzzy = new SearchFuzzyOptions { MaxEdits = 2 } }` |
| As you type | `autocomplete: { query, path }` | `Search.Autocomplete(t => t.Name, prefix)` |
| Read the score | `{ $meta: "searchScore" }` | the same, in a `Project("{ score: { $meta: 'searchScore' } }")` |
| Counts and facets, no documents | `{ $searchMeta: { count / facet } }` | `.SearchMeta(...)` |

### Watch out: a new search index isn't ready when `CreateOneAsync` returns

Create, then **wait for `queryable`**, then query. Queried too early, `$search` returns an empty result
and no error. Locally it takes a fraction of a second; on an Atlas free cluster about a minute.

### Watch out: no `index` means an index called `default`

`$search` without `index: "name"` looks for one named `default`. Name yours anything else and forget,
and you get an empty result and no error.

### Watch out: `number` does not index Decimal128

Atlas Search's `number` type is `int32`, `int64` and `double`. A **Decimal128** field (our `price`)
can be mapped as `number` without complaint, and then **nothing in it is searchable**. A `range`
query given a `decimal` fails with `Type is not supported: DECIMAL128`; cast it to a `double` and the
query runs and returns **nothing**. Filter that field in a `$match` *after* the `$search`, or store a
searchable copy as an `int` or `double`.

### Which type for which operator

| Operator | Needs the field mapped as |
|---|---|
| `text`, `phrase` | `string` (with an analyzer if you want stemming) |
| `autocomplete` | `autocomplete` |
| `equals` | `token`, `boolean`, or a number |
| `range` | `number` or `date` |
| facets | `token` or `stringFacet` |

A dynamic index can't give you `token`, `autocomplete` or a chosen analyzer: that is when to go static.

## Part 3B — Vector Search

A vector index definition is JSON, and so is a `$vectorSearch` stage. The driver's `VectorSearch(...)`
prints exactly the stage you'd type in the shell, so `Console.WriteLine(query)` shows you the shell
version of anything you wrote in C#.

| What you want | Shell / Atlas | C# |
|---|---|---|
| Create a vector index | `db.toys.createSearchIndex("n", "vectorSearch", { fields: [ ... ] })` | `SearchIndexes.CreateOneAsync(new CreateSearchIndexModel("n", SearchIndexType.VectorSearch, definition))` |
| A vector field | `{ type: "vector", path, numDimensions: 1024, similarity: "cosine" }` | the same, as a `BsonDocument` |
| A filter field | `{ type: "filter", path: "minAge" }` | the same |
| Find the nearest | `{ $vectorSearch: { index, path, queryVector, numCandidates, limit } }` | `.VectorSearch(t => t.Field, new QueryVector(v), limit, new VectorSearchOptions<T> { IndexName, NumberOfCandidates })` |
| Narrow it first (pre-filter) | `filter: { minAge: { $lte: 5 } }` | `VectorSearchOptions<T> { Filter = ... }` |
| Exact (ENN), not approximate | `exact: true`, with no `numCandidates` | `VectorSearchOptions<T> { Exact = true }` |
| Read the score | `{ $meta: "vectorSearchScore" }` | the same, in a `Project` |
| Get a vector for some text | `POST https://api.voyageai.com/v1/embeddings` | `HttpClient` |

### Stretch: reranking, shorter vectors and quantization

| What you want | Shell / Atlas / REST | C# |
|---|---|---|
| Rerank a short list | `POST /v1/rerank` `{ query, documents, model: "rerank-3", top_k }` → `data[] { index, relevance_score }` | `HttpClient` (`VoyageReranker.RerankAsync`) |
| Shorter vectors from Voyage | `output_dimension: 256` (also 512, 1024, 2048) | `VoyageEmbedder` with `dimensions: 256` |
| …or shorten one you have | keep the first *n* numbers, then rescale to length 1 | `GiftFinder.Shorten` |
| Quantize an index | `quantization: "scalar"` or `"binary"` on the vector field | the same, in the `BsonDocument` |
| Contextualized chunk embeddings | `POST /v1/contextualizedembeddings` `{ inputs: [[chunk, chunk]], input_type, model }` | `HttpClient` |
| Let Atlas make the vectors | an `autoEmbed` field in the vector index; query with `query: { text }` | the same, as a `BsonDocument` |

A key created in **MongoDB Atlas** uses `https://ai.mongodb.com` for all of the Voyage endpoints (`VOYAGE_BASE_URL`); one
created on voyageai.com uses `https://api.voyageai.com`. A rerank score only means something next to the others from the
same call, and a reranker can only re-order what the first stage already found.

### Watch out: `numDimensions` is not checked when you create the index

Declare the wrong number and the index builds, reports **READY**, and is `queryable`. Query it with a vector of
the real length and you get `vector field is indexed with N dimensions but queried with M`: read it. "Fix" the
query to match the index and you get **no error and no results**, because every stored vector was the wrong
length for the index and was skipped. The number must equal what the embedding model produces. It can't be
edited: drop the index and create it again.

### Watch out: the same model, the same length

The question must be embedded by **the same model** as the stored vectors, with the same number of
dimensions. Atlas never makes a vector: your code asks the embedding model, then hands the vector to Atlas.
Voyage's `input_type` is `"document"` for text you store and `"query"` for a question.

### Rules the server enforces

| Rule | If you break it |
|---|---|
| `queryVector` length = `numDimensions` | error naming both numbers |
| `numCandidates` ≥ `limit`, and ≤ 10,000 | error |
| `numCandidates` is required unless `exact: true` | error |
| A `filter` field must be declared `filter` in the index | `Path 'x' needs to be indexed as filter` |
| A wrong index name | **no results, no error** |
| `$vectorSearch` must be the first stage | error |

### Similarity: what the score means

Atlas rescales every similarity to a 0 to 1 score, higher is closer: cosine `(1 + cos) / 2`, euclidean
`1 / (1 + distance²)`, dotProduct `(1 + dot) / 2`. A cosine score of 0.5 means *at right angles*.
Cosine ignores a vector's length; dot product and Euclidean don't.

### Pre-filter or post-filter?

A `filter` inside `$vectorSearch` narrows **before** the search, so you still get `limit` results, and it
doesn't change any score. A `$match` after it narrows what's already been chosen, so you can end up with
fewer than `limit`. Pre-filtered queries are typically slower than unfiltered ones (MongoDB's docs say so),
and a filter that is too restrictive is a problem of its own.

## Part 4A — RAG

A RAG pipeline is a few small pieces you already know (a chunker, an embedding call, a vector index, a `$vectorSearch`) plus a prompt.
The badge may show the same ideas in Python, but the MongoDB parts are identical.

| What you want | Shell / Atlas / REST | C# |
|---|---|---|
| Chunk with overlap | a loop over words: window `size`, step `size − overlap` | `Chunker.SplitIntoChunks` |
| Store a chunk with its metadata | `db.handbook_chunks.insertOne({ _id, section, audience, source, text, embedding })` | `ReplaceOneModel<HandbookChunk>` with `IsUpsert = true` |
| Remove chunks that no longer exist | `deleteMany({ section: { $in: names }, _id: { $nin: keep } })` | `DeleteManyAsync(c => names.Contains(c.Section) && !keep.Contains(c.Id))` |
| A vector index with filter fields | `createSearchIndex("n", "vectorSearch", { fields: [ { type: "vector", ... }, { type: "filter", path: "section" } ] })` | `SearchIndexes.CreateOneAsync(new CreateSearchIndexModel("n", SearchIndexType.VectorSearch, definition))` |
| Search, narrowed first | `{ $vectorSearch: { ..., filter: { section: "Toy Safety" } } }` | `VectorSearchOptions<T> { Filter = Builders<T>.Filter.Eq(c => c.Section, "Toy Safety") }` |
| Embed a question (not a stored chunk) | `POST /v1/embeddings` with `"input_type": "query"` | `IEmbedder.EmbedAsync(question, "query")` |
| Call the language model | `POST /chat/completions` with `messages` (system, then user) | `IChatClient.GetResponseAsync(messages)` |
| Give an agent the search as a tool | a function the model may call | `TextSearchProvider` (Agent Framework) over a `Func<string, CancellationToken, Task<IEnumerable<TextSearchResult>>>` |

The same retriever through Semantic Kernel's MongoDB connector (a **preview** package) is shown, with what it does and doesn't do, in Part 4A's README.

## Part 4B — Agents

An agent is a model that can call functions, in a loop, with a memory. The badge may show it in Python or LangGraph, but the ideas and the MongoDB parts are the same.

| What you want | Elsewhere (Python / LangGraph, shell) | C# |
|---|---|---|
| Define a tool | a function with a docstring and typed arguments (a LangChain `@tool`) | a method with `[Description]`s, wrapped by `AIFunctionFactory.Create(method, target, options)` |
| What a model is shown about a tool | name, description and a JSON schema of the parameters | `AIFunction.Name`, `.Description` and `.JsonSchema` |
| A tool call, from the model | a message with `tool_calls: [{ id, name, args }]` | `FunctionCallContent(callId, name, arguments)` |
| A tool result, back to the model | a message with role `tool` and the call's `tool_call_id` | `FunctionResultContent(callId, result)`, in a message with `ChatRole.Tool` |
| The agent loop | a graph: an *agent* node, a *tools* node, a conditional edge, and an edge back | `ChatClientAgent`, or the `for` loop in Part 4B's `ToolLoop` |
| A cap on the loop | `recursion_limit` | a `maxTurns` you enforce yourself (`ToolLoop`) |
| A retrieval tool | a retriever exposed as a tool | `TextSearchProvider` with `SearchTime = OnDemandFunctionCalling` |
| Short-term memory | a checkpointer, or the message history | a `ChatHistoryProvider` (`MongoChatHistoryProvider`) |
| Store a message | `insertOne({ conversationId, sequence, role, message, createdAt })` | `InsertManyAsync` of `StoredMessage` |
| Read a conversation in order | `find({ conversationId }).sort({ sequence: 1 })` | `Find(m => m.ConversationId == id).SortBy(m => m.Sequence)` |
| One message per position | `createIndex({ conversationId: 1, sequence: 1 }, { unique: true })` | `CreateIndexOptions { Unique = true }` |
| Expire old conversations | `createIndex({ createdAt: 1 }, { expireAfterSeconds: 86400 })` | `CreateIndexOptions { ExpireAfter = TimeSpan.FromHours(24) }` |
| Keep a window of the conversation | trim the message list, never between a tool call and its result | `ChatWindow.TrimToRecent` |
