# Part 2 — Solutions

Every answer, with the reasoning. **Using this isn't cheating** — reading a worked example
is how most people learn a new pipeline. Try a step first, then come here.

Each solution below is exactly what's verified by the tests.

<details>
<summary><b>Step 1 — CountOpenOrdersAsync</b> (objective 1)</summary>

```csharp
public async Task<long> CountOpenOrdersAsync(string season = "current")
{
    var pipeline = PipelineDefinition<Order, BsonDocument>.Create(
    [
        new BsonDocument("$match", new BsonDocument { { "season", season }, { "status", "open" } }),
        BsonDocument.Parse("""{ $count: "openOrders" }"""),
    ]);

    var result = await _orders.Aggregate(pipeline).FirstOrDefaultAsync();

    // $count produces NO document at all when nothing reaches it, so "no result" means zero.
    return result is null ? 0 : result["openOrders"].ToInt64();
}
```

The `$match` stage holds the variable `season`, so it's built with a `BsonDocument` rather
than glued into a string. See the note on injection in the README.

The same thing with the typed builders, which handle the variable for you:

```csharp
var result = await _orders.Aggregate()
    .Match(o => o.Season == season && o.Status == "open")
    .Count()
    .FirstOrDefaultAsync();

return result?.Count ?? 0;
```

**Why `?.Count ?? 0` and `is null ? 0`?** `$count` is unusual: it doesn't output `{ openOrders: 0 }`
when there's nothing to count — it outputs *nothing*. The second test calls it with a season
that doesn't exist, and a version that calls `.First()` or `result["openOrders"]` straight away
will throw. `countDocuments()` from Part 1 doesn't have this quirk, which is part of why it's
the better choice when a count is all you want. Here we use `$count` because it's the simplest
stage that shows what a pipeline *is*: a list of stages, each receiving what the one before sent.

If you got 24 instead of 22: you counted two open orders from last season that the elves
forgot to close. The season in the `$match` is what excludes them.

**Shell:** `db.orders.aggregate([ { $match: { season: "current", status: "open" } }, { $count: "openOrders" } ])`
</details>

<details>
<summary><b>Step 2 — ScoresThisSeasonAsync</b> (objective 1)</summary>

```csharp
public async Task<List<ChildScore>> ScoresThisSeasonAsync()
{
    var pipeline = PipelineDefinition<LedgerEntry, ChildScore>.Create(
    [
        """{ $match: { season: "current" } }""",
        """{ $group: { _id: "$child", points: { $sum: "$points" } } }""",
        """{ $project: { _id: 0, child: "$_id", points: 1 } }""",
    ]);

    return await _ledger.Aggregate(pipeline).ToListAsync();
}
```

The fix is moving one line: `$match` goes first.

**Why the original returned nothing.** Documents flow down a pipeline, and each stage only sees
what the previous stage *output*. `$group` outputs brand-new documents containing only the
group key and whatever accumulators you asked for. After this stage:

```js
{ _id: "$child", points: { $sum: "$points" } }
```

each document is just `{ _id: "Isla", points: 14 }`. There is no `season` field any more. So
the next stage, `{ $match: { season: "current" } }`, asks every document for a field none of
them have, and matches nothing. No error — an empty result. That's what makes this bug nasty.

**Order changes the answer, not just the speed.** Here it changed the answer to "nothing".
Two more ways it bites:

- `$limit` before `$sort` keeps the first *N* documents in whatever order they happen to be
  stored, and *then* sorts those. You get a sorted list of the wrong documents. (Step 6.)
- `$unwind` before `$match` makes every order into several documents before you've thrown any
  away. Same answer, much more work.

**The server helps, a bit.** MongoDB's optimiser will move a `$match` that's safe to move
earlier. Write `$sort` then `$match` and it quietly runs the `$match` first — the explain plans
are identical (the Level up lets you watch it happen). But it can only move a stage when doing
so can't change the answer. It can't rescue this pipeline, because the `$match` here depends on a
field that `$group` removed; and it can't rescue a `$limit` that runs too early, which really does
change which documents you get. **Reorderings that can't change the answer, the server fixes.
Reorderings that can, it can't.**

**Shell:** `db.ledger.aggregate([ { $match: { season: "current" } }, { $group: { _id: "$child", points: { $sum: "$points" } } }, { $project: { _id: 0, child: "$_id", points: 1 } } ])`
</details>

<details>
<summary><b>Step 3 — ToysToMakeAsync</b> (objective 2)</summary>

```csharp
public async Task<List<ToyDemand>> ToysToMakeAsync()
{
    var pipeline = PipelineDefinition<Order, ToyDemand>.Create(
    [
        """{ $match: { season: "current", status: "open" } }""",
        """{ $unwind: "$lines" }""",
        """{ $group: { _id: "$lines.toy", quantity: { $sum: "$lines.quantity" } } }""",
        """{ $project: { _id: 0, toy: "$_id", quantity: 1 } }""",
    ]);

    return await _orders.Aggregate(pipeline).ToListAsync();
}
```

Walk one order through it. Sofia's order starts as one document:

```js
{ orderNumber: "ORD-1009", child: "Sofia", region: "North America", status: "open",
  lines: [ { toy: "Tyrannosaurus Floor Puzzle", quantity: 3 }, { toy: "Triceratops Night Light", quantity: 1 } ] }
```

- **`$match`** lets it through, because it's this season and still open.
- **`$unwind: "$lines"`** turns it into *two* documents, one per line. Everything else is
  copied onto both. Now each document has `lines` as a single sub-document, not an array.
- **`$group`** collects every document with the same `lines.toy` into one, adding up the quantities.
- **`$project`** tidies the result: the group key arrives as `_id`, so we rename it to `toy` and
  hide `_id`.

A toy that doesn't exist in the catalogue ("Unicorn Saddle") still appears. `$group` never looks at
the catalogue — that's what `$lookup` is for, in Step 7.

**Check the filter.** If the dinosaur puzzle says 14 rather than 11, you're counting the forgotten
open orders from last season. If it's higher still, you're counting orders that are already packed.

The same pipeline with LINQ:

```csharp
return await _orders.AsQueryable()
    .Where(o => o.Season == "current" && o.Status == "open")
    .SelectMany(o => o.Lines)
    .GroupBy(l => l.Toy)
    .Select(g => new ToyDemand(g.Key, g.Sum(l => l.Quantity)))
    .ToListAsync();
```

Add `using MongoDB.Driver.Linq;`. Without it, .NET 10 gives you a baffling error —
`CS0411: The type arguments for method 'AsyncEnumerable.ToListAsync<TSource>' cannot be inferred` —
because the compiler can't find the MongoDB `ToListAsync` for a LINQ query, so it falls back to a
built-in .NET 10 method that doesn't fit. The `using` is the whole fix.

Both versions send the same four stages. LINQ is lovely in application code. We lead with the JSON
because it's what you'll see in Compass and the badge — and because LINQ can only express what the
driver knows how to translate.

**Shell:** `db.orders.aggregate([ { $match: { season: "current", status: "open" } }, { $unwind: "$lines" }, { $group: { _id: "$lines.toy", quantity: { $sum: "$lines.quantity" } } }, { $project: { _id: 0, toy: "$_id", quantity: 1 } } ])`
</details>

<details>
<summary><b>Step 4 — DemandByRegionAsync</b> (objective 2)</summary>

```csharp
public async Task<List<RegionDemand>> DemandByRegionAsync(string toy)
{
    var wanted = new BsonDocument("lines.toy", toy);

    var pipeline = PipelineDefinition<Order, RegionDemand>.Create(
    [
        new BsonDocument("$match", new BsonDocument { { "season", "current" }, { "status", "open" } }
                                       .AddRange(wanted)),
        BsonDocument.Parse("""{ $unwind: "$lines" }"""),
        new BsonDocument("$match", wanted),
        BsonDocument.Parse("""{ $group: { _id: "$region", quantity: { $sum: "$lines.quantity" } } }"""),
        BsonDocument.Parse("""{ $project: { _id: 0, region: "$_id", quantity: 1 } }"""),
    ]);

    return await _orders.Aggregate(pipeline).ToListAsync();
}
```

**Why two `$match` stages?** They do different jobs.

The **first** `$match` works on *orders*. `"lines.toy": toy` is Part 1's array trick: "does any
element of `lines` have this toy?" It's there to throw away the orders that have nothing to do
with this toy, early, before the expensive stages.

But it keeps the *whole* order. Ingrid's order has a dinosaur puzzle **and** a train set. After
`$unwind` it's two documents — and the train-set document is still there. If you group straight
away, you add the train set's quantity to Europe's total.

The **second** `$match` works on *order lines*, after the unwind, and keeps only the lines for
this toy. Without it, Europe wants 2 (1 puzzle + 1 train), and South America reports 7 instead
of 4. The test checks for exactly this.

> **An unwind turns "documents that contain X" into "documents that are X".** That's the moment
> a second filter becomes necessary.

**The variable.** `toy` comes from the caller, so it goes into a `BsonDocument`, never into a
string. Compare:

```csharp
// ❌ Don't. A toy name containing a quote breaks out of the string and changes the stage.
$$"""{ $match: { "lines.toy": "{{toy}}" } }"""

// ✅ A BsonDocument keeps the value as data, whatever it contains.
new BsonDocument("$match", new BsonDocument("lines.toy", toy))
```

That's the aggregation version of SQL injection. Here is what actually happens with three toy
names (this was run, not imagined):

| `toy` is… | Glued into a JSON string | Built with `BsonDocument` |
|---|---|---|
| `Rocking Horse` | `{ "lines.toy": "Rocking Horse" }` ✅ | same ✅ |
| `Kite, "Red" Dragon` | **throws** `FormatException` — a perfectly innocent name broke it | `{ "lines.toy": "Kite, \"Red\" Dragon" }` ✅ |
| `x", "region": "Asia` | quietly becomes `{ "lines.toy": "x", "region": "Asia" }` — **extra text became part of your stage** | stays one literal value ✅ |

So even with no attacker in sight, a quote in a value breaks the glued version; and with one, someone
else's text becomes part of your pipeline. It's the reason the README's rule is "fixed values can be
JSON, variables go in a `BsonDocument`".

A toy with no open orders (the `Lead Soldier Set` test) simply produces no documents out of the
first `$match`, so the rest of the pipeline has nothing to do, and you get an empty list.

**Shell:** `db.orders.aggregate([ { $match: { season: "current", status: "open", "lines.toy": "Tyrannosaurus Floor Puzzle" } }, { $unwind: "$lines" }, { $match: { "lines.toy": "Tyrannosaurus Floor Puzzle" } }, { $group: { _id: "$region", quantity: { $sum: "$lines.quantity" } } }, { $project: { _id: 0, region: "$_id", quantity: 1 } } ])`
</details>

<details>
<summary><b>Step 5 — VerdictsAsync</b> (objective 2)</summary>

```csharp
public async Task<List<ChildVerdict>> VerdictsAsync()
{
    var pipeline = PipelineDefinition<LedgerEntry, ChildVerdict>.Create(
    [
        """{ $match: { season: "current" } }""",
        """{ $group: { _id: "$child", points: { $sum: "$points" } } }""",
        """
        { $project: {
            _id: 0,
            child: "$_id",
            points: 1,
            verdict: { $cond: [ { $gte: ["$points", 0] }, "nice", "naughty" ] }
        } }
        """,
    ]);

    return await _ledger.Aggregate(pipeline).ToListAsync();
}
```

This is Step 2's pipeline with one more idea added to the `$project`: a **computed field**.
`$project` isn't only for hiding and renaming — it can calculate.

`$cond` is "if / then / else": `[ condition, valueIfTrue, valueIfFalse ]`.

**Two syntaxes for "greater than or equal".** This is worth noticing, because it trips people up
in the badge:

| Where | Syntax |
|---|---|
| In a `$match` (a **query filter**) | `{ points: { $gte: 0 } }` — field name first |
| In a `$project` / `$set` (an **aggregation expression**) | `{ $gte: ["$points", 0] }` — operator first, field names with a `$` |

They look similar and they aren't interchangeable. A `$match` filter names a field and puts the
condition inside it; an aggregation expression is a function call with arguments in an array.

**Zero is nice.** Emma was grumpy about sprouts (−2) then tidied her room (+2): exactly 0. `$gte`
says nice; `$gt` would say naughty. The test names her.

**Shell:** `db.ledger.aggregate([ { $match: { season: "current" } }, { $group: { _id: "$child", points: { $sum: "$points" } } }, { $project: { _id: 0, child: "$_id", points: 1, verdict: { $cond: [ { $gte: ["$points", 0] }, "nice", "naughty" ] } } } ])`
</details>

<details>
<summary><b>Step 6 — KindestDeedsAsync</b> (objective 3)</summary>

```csharp
public async Task<List<LedgerEntry>> KindestDeedsAsync(int count)
{
    var pipeline = PipelineDefinition<LedgerEntry, LedgerEntry>.Create(
    [
        BsonDocument.Parse("""{ $match: { season: "current", points: { $gt: 0 } } }"""),
        BsonDocument.Parse("""{ $sort: { points: -1 } }"""),
        new BsonDocument("$limit", count),
    ]);

    return await _ledger.Aggregate(pipeline).ToListAsync();
}
```

The same with the typed builders:

```csharp
return await _ledger.Aggregate()
    .Match(e => e.Season == "current" && e.Points > 0)
    .SortByDescending(e => e.Points)
    .Limit(count)
    .ToListAsync();
```

**This order is the whole lesson.**

| Order | What happens |
|---|---|
| `$match` → `$sort` → `$limit` ✅ | Filter to this season's kind deeds, sort *those*, keep the best five. |
| `$limit` → `$sort` ❌ | Take the first five documents *as stored*, then sort them. A tidy list of the wrong deeds. |
| `$sort` → `$match` → `$limit` | Same answer as ✅, but sorts all 79 entries to throw most of them away. |

The ledger file is deliberately in no particular order, so a `$limit` that runs first returns
five arbitrary deeds and the test fails with a list that *looks* plausible.

**Why `$match` first rather than last?** Correctness is the same, but `$match` first means `$sort`
only sorts what's left. And a `$sort` straight after a `$match` can use an index.

**Why `points > 0`?** "Kindest" means a kindness. Without it, the list would still be right
while there are more than five good deeds — but it'd quietly be wrong the day there aren't.

**Last season's +12.** Someone rescued a kitten last season. Forget `season: "current"` and
they top the list.

**Sort ties.** The top five here have different points (10, 9, 8, 7, 6) so there's one right
answer. If two deeds tied for fifth place, which one you got would be down to chance — in real
data, add a second sort key (`{ points: -1, _id: 1 }`) so the order is the same every time.

**Shell:** `db.ledger.aggregate([ { $match: { season: "current", points: { $gt: 0 } } }, { $sort: { points: -1 } }, { $limit: 5 } ])`
</details>

<details>
<summary><b>Step 7 — LineCostsAsync</b> (objective 4)</summary>

```csharp
public async Task<List<LineCost>> LineCostsAsync()
{
    var pipeline = PipelineDefinition<Order, LineCost>.Create(
    [
        """{ $match: { season: "current", status: "open" } }""",
        """{ $unwind: "$lines" }""",
        """{ $lookup: { from: "toys", localField: "lines.toy", foreignField: "name", as: "toy" } }""",
        """{ $unwind: "$toy" }""",
        """
        { $set: {
            unitPrice: "$toy.price",
            lineTotal: { $multiply: ["$lines.quantity", "$toy.price"] }
        } }
        """,
        """{ $project: { _id: 0, orderNumber: 1, toy: "$lines.toy", quantity: "$lines.quantity", unitPrice: 1, lineTotal: 1 } }""",
    ]);

    return await _orders.Aggregate(pipeline).ToListAsync();
}
```

**`$lookup` is a left outer join.** For each incoming document it looks in another collection
(`from`) for documents whose `foreignField` equals this document's `localField`, and puts what it
finds in a new array field (`as`).

| `$lookup` option | Here | Means |
|---|---|---|
| `from` | `"toys"` | the collection to look in |
| `localField` | `"lines.toy"` | the field on *our* document (after the unwind, the toy's name) |
| `foreignField` | `"name"` | the field on the *other* collection to compare it to |
| `as` | `"toy"` | what to call the array of matches |

**The result is always an array.** Even when it finds exactly one toy, `toy` is `[ { ...the toy... } ]`.
Hence the second `$unwind`: it turns that one-element array into the toy itself, so `$toy.price` works.

**`$set` adds fields.** It's `$project`'s gentler sibling: it keeps every field already there and
adds (or overwrites) the ones you name. (You may have seen `$addFields`; `$set` is the same stage
under a shorter name.) `$multiply` here works on a whole number and a decimal, and the result is
still an exact decimal — money stays money.

**What happens to a toy we don't make?** Koa and Elsa each asked for a "Unicorn Saddle". The
`$lookup` searches the catalogue, finds nothing, and sets `toy` to an **empty array** — no error.
Then `$unwind: "$toy"` has nothing to unwind in an empty array and **drops the document**. So the
line disappears from the results, silently. Count the rows: 50 open lines in, 48 out.

That's often exactly what you want, and sometimes it very much isn't (see Level up for keeping them).

**Why look the price up, instead of storing it on the order?** Because prices change. The test
changes the Rocking Horse's price *after* the orders exist, and the line totals follow. If an
order held a copy of the price, you'd have to find and update every one. Storing the toy's *name*
and joining at query time means there's one place to change a price. (The other side of that
trade: if you wanted an order to remember what the customer actually paid, you *would* copy the
price in — a design decision, not a rule.)

**Shell:** `db.orders.aggregate([ { $match: { season: "current", status: "open" } }, { $unwind: "$lines" }, { $lookup: { from: "toys", localField: "lines.toy", foreignField: "name", as: "toy" } }, { $unwind: "$toy" }, { $set: { unitPrice: "$toy.price", lineTotal: { $multiply: ["$lines.quantity", "$toy.price"] } } }, { $project: { _id: 0, orderNumber: 1, toy: "$lines.toy", quantity: "$lines.quantity", unitPrice: 1, lineTotal: 1 } } ])`
</details>

<details>
<summary><b>Step 8 — ExplainAsync</b> (objective 5)</summary>

```csharp
public async Task<BsonDocument> ExplainAsync(IEnumerable<string> stages)
{
    var command = new BsonDocument
    {
        { "explain", new BsonDocument
            {
                { "aggregate", _orders.CollectionNamespace.CollectionName },
                { "pipeline", new BsonArray(stages.Select(s => (BsonValue)BsonDocument.Parse(s))) },
                { "cursor", new BsonDocument() },
            } },
        { "verbosity", "executionStats" },
    };

    return await _orders.Database.RunCommandAsync<BsonDocument>(command);
}
```

The driver has no `.Explain()` for an aggregation, so this sends the **`explain` command** directly.
It wraps a normal `aggregate` command — which is exactly what the shell's
`db.orders.explain("executionStats").aggregate([...])` does for you.

**`verbosity`** controls how much you get:

| Value | What the server does |
|---|---|
| `queryPlanner` | Works out the plan but **doesn't run** the query. |
| `executionStats` | **Runs** it and reports what happened: documents examined, documents returned, time taken. |
| `allPlansExecution` | Like `executionStats`, but also reports the plans it rejected. |

`executionStats` is the useful default, because "how many documents did this read to return that
many?" is the question that matters.

**Reading what comes back.** For a no-index run of Step 3's pipeline you'll find:

```
PROJECTION_SIMPLE
  └─ COLLSCAN          ← read every document in the collection
totalDocsExamined: 46   ← all of them
nReturned:         22   ← to find the ones we wanted
```

`COLLSCAN` is a collection scan: no index could help, so the server read every order. For 46
documents that's nothing. For 46 million it's the difference between a blink and a coffee break.

**The shape of the output depends on the pipeline.** A pipeline that is only a `$match` gives you
`queryPlanner` and `executionStats` at the top level. A longer one like this nests them inside
`stages[0].$cursor`, because the first stage (the `$match`) is run by the query engine and the
rest by the aggregation engine. And when the server can run *more* of the pipeline itself (a
`$match` followed by `$count`, say) it describes the plan in yet another layout. So the tests —
and you, in Compass — find the interesting parts by searching for them wherever they are, rather
than trusting a fixed path.

**Shell:** `db.orders.explain("executionStats").aggregate([ { $match: { season: "current", status: "open" } }, { $unwind: "$lines" }, ... ])`

**In Compass:** open the **Aggregations** tab for the `orders` collection, build the same four
stages, and click **Explain** (top right of the pipeline builder). The README has the full
click-through.
</details>

<details>
<summary><b>Step 9 — CreateSeasonStatusIndexAsync</b> (objective 5)</summary>

```csharp
public async Task<string> CreateSeasonStatusIndexAsync()
{
    var keys = Builders<Order>.IndexKeys.Ascending(o => o.Season).Ascending(o => o.Status);
    return await _orders.Indexes.CreateOneAsync(new CreateIndexModel<Order>(keys));
}
```

It returns the index's name — MongoDB builds it from the keys, so this one is `season_1_status_1`.

Run Step 8's explain again and the plan changes:

```
PROJECTION_SIMPLE
  └─ FETCH
       └─ IXSCAN        ← walked the index instead of the whole collection
            season_1_status_1
totalDocsExamined: 22   ← only the orders that matched
nReturned:         22
```

`COLLSCAN` has become `IXSCAN`, and the number of documents read dropped from 46 to 22 — one for
every order returned, none wasted. That is what an index is *for*.

**Why `season` then `status`?** Both are equality tests in the `$match`, so either order serves this
query. It's worth getting into the habit of thinking about it anyway: for an index serving a query
with equality, sort and range fields, the usual guideline is **Equality, Sort, Range** (the ESR
guideline, covered in Part 1's Level up).

**Why does the explain use `Step 3`'s pipeline rather than Step 1's?** Try it: with this index,
Step 1's `$match` + `$count` doesn't use `IXSCAN` at all. The server notices it only needs a
*number*, and answers it by counting entries inside the index (`COUNT_SCAN`) without reading a
single order. Even faster, and a nice thing to find — but a poor way to show what an index does
for a query that has to return documents.

**What an index costs.** Every index must be updated on every write. Orders are written all
season, so don't index everything — index the queries you run.

**Shell:** `db.orders.createIndex({ season: 1, status: 1 })`
</details>

---

## Still red?

**All 24 failing with a connection error.** MongoDB isn't running:
```bash
docker compose up -d
dotnet run --project tools/doctor
```

**Step 2 returns an empty list.** That's the bug you were asked to find. Move the `$match` so it
runs before the `$group`.

**Step 3 or 4 off by a lot.** Check the first `$match` has *both* `season: "current"` and
`status: "open"`. The data includes last season's forgotten open orders and this season's packed
ones, on purpose.

**Step 4 reports too much for a region.** You're missing the second `$match`, after the `$unwind`.

**Step 6 returns the wrong five.** Is `$sort` before `$limit`? Does the `$match` have the season?

**Step 7 returns 50 rows, or throws.** Check you `$unwind` the `toy` array after the `$lookup`.
The two lines for a toy we don't make should be gone.

**Step 8 or 9 can't find `COLLSCAN` or `IXSCAN`.** The tests search the whole explain document for
a stage called exactly that. Print the explain output (`Console.WriteLine(explain.ToJson())`) and
look at what the server actually said.

**A test says it can't connect, but only some of them.** The tests use their own database
(`santas_workshop_part2_tests`), so they never touch the one you explore in Compass — but they do
need MongoDB running.
