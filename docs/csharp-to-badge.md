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

*Added when Part 3A ships.*

## Part 3B — Vector Search

*Added when Part 3B ships.*

## Part 4 — RAG and agents

*Added when Part 4 ships.*
