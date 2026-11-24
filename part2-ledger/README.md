# Part 2 — The Naughty/Nice Ledger

> **Level:** Beginner. Part 1 helps — we reuse its array trick once — but you don't need to have finished it.
> **Time:** 30–60 minutes for the core path.
> **Badge:** Fundamentals of Data Transformation

---

## The story

The catalogue is in MongoDB. The elves' wrists are recovering, Mrs Claus has stopped sighing
at the lectern, and for almost an hour everything is calm.

Then the letters arrive.

Dozens of them, in dozens of different handwritings, each one copied by an elf onto an order
slip. And beside them, the other book: **the Naughty/Nice Ledger**, in which one small and
tireless elf has been writing down every good and bad deed since — well, since the ledger.

Santa, who is thinking, asks his questions in the loud voice he uses for thinking:

- *How many of each toy do we still have to make?*
- *Where in the world does the dinosaur puzzle need to go?*
- *Who is on the nice list, and who is on the naughty?*
- *What was the single kindest thing anyone did?*
- *And what is all of this going to cost?*

You could answer each one with a `Find()` and a loop, and be at it until Boxing Day. Instead
you're going to ask MongoDB to do the sums. These questions are a different animal from Part 1:
they don't *find* documents, they **reshape** them — add them up, flatten them, join them. That's
what an aggregation pipeline is for.

(There is one more thing. The ledger knows who spilled the cocoa on the letter M. You'll find out.)

---

## Get the code

Part 2 lives in the same repository as Part 1:

**[github.com/LuceCarter/12days-of-mongodbmas-dotnet-workshop](https://github.com/LuceCarter/12days-of-mongodbmas-dotnet-workshop)**

If you already have it from Part 1, skip to the next section. If not, get it either way:

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
part2-ledger/
  README.md                   this page
  Starter/LedgerReports.cs    ← your code goes here
  Tests/                      the tests (leave them alone)
  SOLUTIONS.md                answers, if you get stuck
src/Shared/                   the models and the seed data
tools/seed/                   loads practice data for Compass and the shell
```

---

## Before you start

Make sure your environment is ready. From the repository folder:

```bash
docker compose up -d
dotnet run --project tools/doctor
```

If anything is red, the doctor tells you how to fix it. Full instructions are in
[docs/setup.md](../docs/setup.md).

You'll also want something to look at in Compass and the shell (you'll use them in Step 8).
Load the practice data now — it's safe to run again whenever you like:

```bash
dotnet run --project tools/seed
```

The **tests don't need this**. They use their own database, `santas_workshop_part2_tests`, so
running them never overwrites the data you're exploring.

---

## How this works

Everything you write goes in
[**`part2-ledger/Starter/LedgerReports.cs`**](Starter/LedgerReports.cs).
It has nine methods. Eight are stubs — a `// TODO` and a `throw new NotImplementedException` —
and **one is already written but wrong**. Finding out why is Step 2.

Run the tests whenever you like:

```bash
dotnet test part2-ledger/Tests
```

They start **red**. Each step turns some of them green. When all 24 pass, you're done.

Stuck? [`SOLUTIONS.md`](SOLUTIONS.md) has every answer with an explanation. Looking isn't
cheating — reading a worked example is how most of us learn this.

---

## What is an aggregation pipeline?

Part 1's `Find()` answers one question: *which documents match?* An **aggregation pipeline**
answers bigger ones: *what do these documents add up to? what shape do I want them in? what do
they look like joined to that other collection?*

The **aggregation framework** is MongoDB's way of processing documents through a **pipeline**:
an ordered list of **stages**. Documents go into the first stage, whatever it outputs goes into
the second, and so on. Each stage does one small job:

| Stage | What it does | In SQL you'd write… |
|---|---|---|
| `$match` | keeps only the documents that match | `WHERE` |
| `$unwind` | turns an array into one document per element | (a join to a child table) |
| `$group` | collapses many documents into one per key, adding things up | `GROUP BY` + `SUM` |
| `$project` | keeps, hides, renames or **computes** fields | `SELECT` |
| `$sort` | orders them | `ORDER BY` |
| `$limit` | keeps the first *n* | `TOP` / `LIMIT` |
| `$lookup` | pulls in matching documents from another collection | `JOIN` |
| `$set` | adds fields, keeps everything else | a computed column |
| `$count` | counts what arrives | `COUNT(*)` |

Think of an assembly line:

```
orders ──► $match ──► $unwind ──► $group ──► $project ──► your results
          (keep the   (one doc    (one doc    (tidy the
           open ones)  per line)   per toy)    shape)
```

> 🧠 **Each stage only sees what the stage before it produced.** That one sentence explains
> most pipeline bugs — and it's why the **order of the stages matters**. Part A is about that.

**Find or aggregate?** Use `Find()` when you want documents as they're stored. Reach for a
pipeline when you want them added up, reshaped or joined. A pipeline can do everything `Find()`
can (`$match`, `$sort`, `$limit` and `$project` are all there), so you'll often see Part 1's
queries written either way.

### Writing a pipeline in C#

You write the stages as **JSON — exactly as the MongoDB shell and Compass show them**, so what
you learn here transfers straight to the badge:

```csharp
var pipeline = PipelineDefinition<Order, ToyDemand>.Create(
[
    """{ $match: { season: "current", status: "open" } }""",
    """{ $unwind: "$lines" }""",
]);

var rows = await _orders.Aggregate(pipeline).ToListAsync();
```

`<Order, ToyDemand>` means "documents come in as `Order`s and go out as `ToyDemand`s". (The
output types are at the bottom of the Starter file; leave them alone.)

> ⚠️ **One rule: a stage with a fixed value can be a JSON string. A stage that contains a
> *variable* must be built with `BsonDocument`.** Pasting a variable into a JSON string is the
> aggregation version of SQL injection: a value containing a quote can break out of the string
> and change your stage. You'll see it in Step 1 and Step 4.

**Want to see what your C# actually sent?** Print the pipeline:

```csharp
Console.WriteLine(pipeline);
// => [{ "$match" : { "season" : "current", "status" : "open" } }, { "$unwind" : "$lines" }]
```

---

## The data

Two new collections join Part 1's `toys`. Every document is a plain object you'll recognise:

```csharp
public sealed class Order
{
    public string OrderNumber { get; set; }    // "ORD-1009"
    public string Child { get; set; }          // "Sofia"
    public string Region { get; set; }         // "North America"
    public string Season { get; set; }         // "current" or "previous"
    public string Status { get; set; }         // "open", "packed" or "delivered"
    public List<OrderLine> Lines { get; set; } // what they asked for
}

public sealed class OrderLine
{
    public string Toy { get; set; }            // "Tyrannosaurus Floor Puzzle"
    public int Quantity { get; set; }          // 3
}

public sealed class LedgerEntry
{
    public string Child { get; set; }          // "Isla"
    public string Season { get; set; }         // "current" or "previous"
    public string Deed { get; set; }           // "Raised money for the animal shelter"
    public int Points { get; set; }            // +10. Mischief is negative.
}
```

A stored order looks like this — notice `lines` is an **array of sub-documents**:

```js
{ orderNumber: "ORD-1009", child: "Sofia", region: "North America",
  season: "current", status: "open",
  lines: [ { toy: "Tyrannosaurus Floor Puzzle", quantity: 3 },
           { toy: "Triceratops Night Light",    quantity: 1 } ] }
```

Three things about this data are deliberate:

- **`season` is `"current"` or `"previous"`, never a date.** These exercises read the same
  whenever you do them. The data holds a little of last season so you can see what forgetting to
  filter on it looks like.
- **A line holds the toy's *name* and a quantity — no price.** The price lives in the catalogue,
  and prices change (Part 1, Step 3 changed one). If every order carried its own copy, you'd have
  to find and fix them all. So an order stores *what was asked for*, and you join to the catalogue
  when you need the price — that's Step 7. (Part 1 asked whether an order should embed its toys
  or reference them. This is the answer: embed the lines, which belong to exactly one order and
  are always read with it; reference the toy, which lives on independently.)
- **A deed's value is stored, but a child's verdict isn't.** Nobody writes "Isla: nice" in the
  ledger. You *work it out* by adding up their deeds — which is exactly the job of `$group`.

<details>
<summary>📜 Santa's legacy SQL (flavour only — skip if you like)</summary>

Normalised, the order book would have needed three tables, and every question would have
needed a join:

```sql
CREATE TABLE orders (id INT PRIMARY KEY, order_number VARCHAR(10), child VARCHAR(50),
                     region VARCHAR(30), season VARCHAR(10), status VARCHAR(10));
CREATE TABLE order_lines (order_id INT, toy_name VARCHAR(100), quantity INT);
CREATE TABLE ledger (id INT PRIMARY KEY, child VARCHAR(50), season VARCHAR(10),
                     deed VARCHAR(200), points INT);
```

In MongoDB the lines live inside the order, so there's no `order_lines` table and no join to
read an order back. As in Part 1, we show the SQL beside each step so you can see what's
changed. **None of it is assessed** — translating from something you know is often the fastest
way in.
</details>

---

## Part A — How a pipeline works

### Step 1 — Count the open orders · *objective 1*

Santa's first question: how many orders are still open? Your first pipeline is two stages
long. Implement `CountOpenOrdersAsync`.

```csharp
var pipeline = PipelineDefinition<Order, BsonDocument>.Create(
[
    new BsonDocument("$match", new BsonDocument { { "season", season }, { "status", "open" } }),
    BsonDocument.Parse("""{ $count: "openOrders" }"""),
]);

var result = await _orders.Aggregate(pipeline).FirstOrDefaultAsync();
return result is null ? 0 : result["openOrders"].ToInt64();
```

`$match` keeps the orders you want; `$count` counts what reaches it and puts the number in a
field you name. Two stages, one number out.

Notice the `$match` is built with a `BsonDocument` — it contains the variable `season`. The
`$count` stage is fixed, so a JSON string is fine.

> 🧠 **`$count` is odd: when nothing reaches it, it outputs *nothing at all*** — not
> `{ openOrders: 0 }`, no document whatsoever. That's why the code checks for `null`. One of the
> tests asks for a season that doesn't exist, and a version that assumes a document always comes
> back will throw.

> **Shell:** `db.orders.aggregate([ { $match: { season: "current", status: "open" } }, { $count: "openOrders" } ])`
> **SQL was:** `SELECT COUNT(*) FROM orders WHERE season = 'current' AND status = 'open'`

---

### Step 2 — Fix the elf's pipeline · *objective 1*

An elf wrote `ScoresThisSeasonAsync` — each child's total points this season — and went home.
It's already in the Starter file. It compiles, runs without errors, and returns an **empty
list**.

Here are its three stages:

```js
{ $group:   { _id: "$child", points: { $sum: "$points" } } }
{ $match:   { season: "current" } }
{ $project: { _id: 0, child: "$_id", points: 1 } }
```

Every stage is correct. Something about their **order** is not. Read them top to bottom as if
documents were walking down an assembly line, and ask of each stage: *what does a document look
like when it arrives here, and does this stage still have what it needs?*

Fix it by moving **one** line. Don't change any stage.

> 🧠 **This is the heart of objective 1: a stage only sees what the previous stage produced.**
> `$group` doesn't pass documents along — it outputs brand-new ones. Order doesn't only change
> *speed*; it can change the *answer*. You'll meet two more ways in this part: a `$limit` that
> runs too early (Step 6), and a filter that has to be applied twice (Step 4).

> **Shell:** `db.ledger.aggregate([ ... ])` — same three stages, in the right order.
> **SQL was:** `WHERE` always ran before `GROUP BY`, so you never had the choice. A pipeline lets
> you put stages in any order, which is more powerful and means you can get it wrong.

---

## Part B — What to make, and where

### Step 3 — How many of each toy? · *objective 2*

Now the real job: how many of each toy do the elves still have to make? Count only this season's
**open** orders. Implement `ToysToMakeAsync` — four stages, in this order:

```csharp
var pipeline = PipelineDefinition<Order, ToyDemand>.Create(
[
    """{ $match: { season: "current", status: "open" } }""",
    """{ $unwind: "$lines" }""",
    """{ $group: { _id: "$lines.toy", quantity: { $sum: "$lines.quantity" } } }""",
    """{ $project: { _id: 0, toy: "$_id", quantity: 1 } }""",
]);

return await _orders.Aggregate(pipeline).ToListAsync();
```

Here's what each stage does to Sofia's order, `ORD-1009`:

| After… | What there is |
|---|---|
| `$match` | her order, one document (it's current and open, so it gets through) |
| `$unwind: "$lines"` | **two** documents — one per line — each a copy of the order with a single line in place of the array |
| `$group` by toy | a document per *toy*: `{ _id: "Tyrannosaurus Floor Puzzle", quantity: 11 }`, adding up every order's lines |
| `$project` | the same, tidied: `_id` renamed to `toy` |

> 🧠 **`$unwind` is how you aggregate over an array.** `$group` works on documents, not on the
> elements inside them, so you first turn each array element into its own document.

> **Shell:** `db.orders.aggregate([ { $match: { season: "current", status: "open" } }, { $unwind: "$lines" }, { $group: { _id: "$lines.toy", quantity: { $sum: "$lines.quantity" } } }, { $project: { _id: 0, toy: "$_id", quantity: 1 } } ])`
> **SQL was:** `SELECT l.toy, SUM(l.quantity) FROM orders o JOIN order_lines l ON l.order_id = o.id WHERE o.season = 'current' AND o.status = 'open' GROUP BY l.toy`

<details>
<summary>The same thing in LINQ</summary>

```csharp
return await _orders.AsQueryable()
    .Where(o => o.Season == "current" && o.Status == "open")
    .SelectMany(o => o.Lines)
    .GroupBy(l => l.Toy)
    .Select(g => new ToyDemand(g.Key, g.Sum(l => l.Quantity)))
    .ToListAsync();
```

This needs `using MongoDB.Driver.Linq;` — without it, .NET 10 gives a baffling `CS0411` about
`AsyncEnumerable.ToListAsync`. We lead with the JSON because it's what you'll see in Compass and
the badge.
</details>

---

### Step 4 — Where's the dinosaur puzzle needed? · *objective 2*

Santa wants to know which regions need a particular toy. Implement `DemandByRegionAsync(toy)`.
It's Step 3's shape for one toy, grouped by region — **with a second `$match`**:

```csharp
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
```

The two `$match` stages do different jobs. The **first** works on *orders*: `"lines.toy": toy` is
Part 1's array trick — *does any line have this toy?* — and throws away the irrelevant orders
early. But it keeps each order **whole**.

Ingrid ordered a dinosaur puzzle **and** a train set. After `$unwind` her order is two documents,
and the train-set document is still there. Group straight away and you'll add the train set to
Europe's total. The **second** `$match`, working on *lines*, removes it.

> 🧠 **An unwind turns "documents that contain X" into "documents that are X".** That's the moment
> a second filter becomes necessary. The test for this step catches it by name.

The `toy` parameter is a variable, so these stages are `BsonDocument`s — the rule from above.

> **Shell:** `db.orders.aggregate([ { $match: { season: "current", status: "open", "lines.toy": "Tyrannosaurus Floor Puzzle" } }, { $unwind: "$lines" }, { $match: { "lines.toy": "Tyrannosaurus Floor Puzzle" } }, { $group: { _id: "$region", quantity: { $sum: "$lines.quantity" } } }, { $project: { _id: 0, region: "$_id", quantity: 1 } } ])`

---

### Step 5 — Naughty or nice? · *objective 2*

Time for the list. Each child's verdict is **nice** if their points this season are zero or more,
and **naughty** if below. Implement `VerdictsAsync` — Step 2's pipeline, with a computed field
added to the `$project`:

```csharp
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
```

`$project` isn't only for hiding and renaming. It can **compute**. `$cond` is "if, then, else":
`[ condition, valueIfTrue, valueIfFalse ]`.

> 🧠 **Two syntaxes for "greater than or equal".** In a `$match` (a *filter*) you name the field
> first: `{ points: { $gte: 0 } }`. Inside `$project` or `$set` (an *aggregation expression*) the
> operator comes first and field names get a dollar sign: `{ $gte: ["$points", 0] }`. They look
> alike and aren't interchangeable — it's a classic way to lose a mark.

Emma was grumpy about sprouts (−2) and then tidied her room (+2): exactly **zero**. The tests
check she's nice.

> **Shell:** `db.ledger.aggregate([ { $match: { season: "current" } }, { $group: { _id: "$child", points: { $sum: "$points" } } }, { $project: { _id: 0, child: "$_id", points: 1, verdict: { $cond: [ { $gte: ["$points", 0] }, "nice", "naughty" ] } } } ])`
> **SQL was:** `SELECT child, SUM(points) AS points, CASE WHEN SUM(points) >= 0 THEN 'nice' ELSE 'naughty' END AS verdict FROM ledger WHERE season = 'current' GROUP BY child`

---

## Part C — The top of the list

### Step 6 — The five kindest deeds · *objective 3*

Santa wants to read out the best deeds of the season, best first. Implement
`KindestDeedsAsync(count)`:

```csharp
var pipeline = PipelineDefinition<LedgerEntry, LedgerEntry>.Create(
[
    BsonDocument.Parse("""{ $match: { season: "current", points: { $gt: 0 } } }"""),
    BsonDocument.Parse("""{ $sort: { points: -1 } }"""),
    new BsonDocument("$limit", count),
]);
```

Three stages, and **the order is the whole lesson**:

| Order | What happens |
|---|---|
| `$match` → `$sort` → `$limit` ✅ | Filter to this season's kind deeds, sort *those*, keep the best five. |
| `$limit` → `$sort` ❌ | Take the first five documents *as they're stored*, then sort them. A tidy list of the **wrong** deeds. |

The ledger file isn't in any particular order, so a `$limit` that runs first gives you five
arbitrary deeds that *look* plausible. The test knows what the real five are.

Two more traps are baked into the data. **Last season** someone rescued a kitten (+12), which
would top the list if your `$match` forgot the season. And `points > 0` matters because "kindest"
means a kindness.

> **Shell:** `db.ledger.aggregate([ { $match: { season: "current", points: { $gt: 0 } } }, { $sort: { points: -1 } }, { $limit: 5 } ])`
> **SQL was:** `SELECT TOP 5 * FROM ledger WHERE season = 'current' AND points > 0 ORDER BY points DESC`

---

## Part D — Joining collections

### Step 7 — What will it cost? · *objective 4*

Orders hold toy *names*; prices live in the catalogue. To cost an order line you have to **join**.
Implement `LineCostsAsync`:

```csharp
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
```

**`$lookup`** is MongoDB's join. For each document it looks in another collection (`from`) for
documents whose `foreignField` equals this document's `localField`, and puts whatever it finds in
a new **array** called `as`.

**`$set`** adds fields while keeping everything already there — `$project`'s gentler sibling. (You
may know it as `$addFields`; it's the same stage under a shorter name.)

> 🧠 **`$lookup` always produces an array**, even when it finds exactly one match. That's why
> there's a second `$unwind`: it turns the one-element array into the toy itself, so `$toy.price`
> works.

> 🧠 **What about a toy we don't make?** Koa and Elsa each asked for a "Unicorn Saddle". The
> `$lookup` searches the catalogue, finds nothing, and sets `toy` to an **empty array** — no
> error. Then `$unwind` has nothing to unwind and **drops the document**. The line quietly
> disappears: 50 open lines go in, 48 come out. Often that's what you want. Sometimes it's very
> much not — the Level up shows how to keep them.

And there's a payoff for not storing prices on orders: one of the tests changes the Rocking
Horse's price *after* the orders exist, and the line totals follow. One place to change a price.

> **Shell:** `db.orders.aggregate([ { $match: { season: "current", status: "open" } }, { $unwind: "$lines" }, { $lookup: { from: "toys", localField: "lines.toy", foreignField: "name", as: "toy" } }, { $unwind: "$toy" }, { $set: { unitPrice: "$toy.price", lineTotal: { $multiply: ["$lines.quantity", "$toy.price"] } } }, { $project: { _id: 0, orderNumber: 1, toy: "$lines.toy", quantity: "$lines.quantity", unitPrice: 1, lineTotal: 1 } } ])`
> **SQL was:** `SELECT o.order_number, l.toy_name, l.quantity, t.price, l.quantity * t.price FROM orders o JOIN order_lines l ON ... JOIN toys t ON t.name = l.toy_name WHERE ...` — an inner join, which is why the unknown toy vanished there too.

---

## Part E — Looking under the bonnet

You've written pipelines that work. Now find out *how* MongoDB runs them. `explain()` shows the
**query plan**: what the server did, in what order, and how much it had to read to do it.

### Step 8 — Read an explain plan · *objective 5*

Implement `ExplainAsync(stages)`. The driver has no `.Explain()` for aggregations, so you send
the **`explain` command** yourself — a normal `aggregate` command wrapped in `explain`:

```csharp
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
```

`verbosity: "executionStats"` makes the server **run** the pipeline and report what happened.
(`"queryPlanner"` only plans it; `"allPlansExecution"` also reports the plans it rejected.)

Run the tests, then print what comes back for Step 3's pipeline, with no index yet. The two
things to find:

```
PROJECTION_SIMPLE
  └─ COLLSCAN            ← read every document in the collection
totalDocsExamined: 46    ← it read all of them…
nReturned:         22    ← …to find the ones it wanted
```

`COLLSCAN` is a **collection scan**: no index could help, so the server read every order. For 46
documents that's nothing; for 46 million it's the difference between a blink and a coffee break.

> **Shell:** `db.orders.explain("executionStats").aggregate([ { $match: { season: "current", status: "open" } }, { $unwind: "$lines" }, { $group: { _id: "$lines.toy", quantity: { $sum: "$lines.quantity" } } }, { $project: { _id: 0, toy: "$_id", quantity: 1 } } ])`

#### 🖐️ Do it in the shell and in Compass

The badge asks you to **access an explain plan in the MongoDB Shell and in Compass**. A test can't
click through Compass for you, so this part is on the checklist below. You'll need the practice
data (`dotnet run --project tools/seed`).

**In the shell.** Open one against your local database:

```bash
docker compose exec mongodb mongosh -u workshop -p workshop --authenticationDatabase admin santas_workshop
```

then paste the **Shell** line above. Scroll the output and find `COLLSCAN`, `totalDocsExamined`
and `nReturned`.

**In Compass.**

1. Connect to `mongodb://workshop:workshop@localhost:27017/?directConnection=true`.
2. Open the `santas_workshop` database and click the **`orders`** collection.
3. Click the **Aggregations** tab.
4. Build Step 3's pipeline. Either click **+ Add Stage**, pick `$match` from the **Select**
   menu and type the stage's contents (`{ season: "current", status: "open" }`) — and so on for
   `$unwind`, `$group`, `$project` — or switch to **Text** view, which accepts the raw pipeline.
   Click **Run** to see the results.
5. Click **Explain** at the top right of the pipeline builder. The **Explain Plan** modal opens.
6. Read the **Query Performance Summary**: execution time, documents returned, documents
   **examined**, and index keys examined. Do the numbers match what you saw from C#?
7. In the **Visual Tree**, each stage of the pipeline is a node — click one for detail. Find the
   `COLLSCAN`. Then switch to **Raw Output** to see the same JSON your code printed.

Compass can also turn a finished pipeline into C#: see
[Export Pipeline to Specific Language](https://www.mongodb.com/docs/compass/agg-pipeline-builder/export-pipeline-to-language/)
in the Compass docs.

---

### Step 9 — Make the plan better · *objective 5*

An **index** lets the server jump straight to the documents it wants instead of reading them all.
Implement `CreateSeasonStatusIndexAsync` — an index on `season`, then `status`: the two fields
Step 3's `$match` tests.

```csharp
var keys = Builders<Order>.IndexKeys.Ascending(o => o.Season).Ascending(o => o.Status);
return await _orders.Indexes.CreateOneAsync(new CreateIndexModel<Order>(keys));
```

Now explain Step 3's pipeline again. The plan changes:

```
PROJECTION_SIMPLE
  └─ FETCH
       └─ IXSCAN          ← walked the index instead of the whole collection
totalDocsExamined: 22     ← only the orders that matched
nReturned:         22
```

`COLLSCAN` has become `IXSCAN`, and documents examined dropped from 46 to 22 — one for every
order returned, none wasted. That is what an index is for. It costs something, though: every
index has to be updated on every write, so index the queries you actually run.

To see it in the shell or Compass, create the index there against the practice data and explain
again:

- **Shell:** `db.orders.createIndex({ season: 1, status: 1 })`
- **Compass:** the **Indexes** tab → **Create Index** → choose `season`, then add `status`
  (the **+** next to the index type makes it a compound index) → **Create Index**. The trash can
  on an index drops it, so you can compare before and after.

> 🧠 **Why explain Step 3's pipeline and not Step 1's?** Try it. With this index, Step 1's `$match`
> + `$count` doesn't show an `IXSCAN` at all. The server realises it only needs a *number* and
> counts entries inside the index (`COUNT_SCAN`) without reading a single order — even faster, and
> a nice thing to find, but a poor way to see what an index does for a query that has to return
> documents.

> **Shell:** `db.orders.createIndex({ season: 1, status: 1 })`

---

## ✅ You're done

```bash
dotnet test part2-ledger/Tests
```

24 green. Santa has his answers, and the elves know what to make and where to send it.

And the cocoa? Open the ledger and look for the worst deed of the season. It was Hemi: −10 for
the cocoa itself, and another −4 for blaming the elves.

---

## Badge checklist — Fundamentals of Data Transformation

Every objective the badge assesses, and the step that teaches it.

| ✓ | Objective | Step | Taught by |
|---|---|---|---|
| ☐ | **1** Define the aggregation framework and explain why stage order matters | Steps 1, 2 | `CountOpenOrdersAsync`, `ScoresThisSeasonAsync` |
| ☐ | **2** Explain and build `$match` → `$unwind` → `$group` → `$project` | Steps 3, 4, 5 | `ToysToMakeAsync`, `DemandByRegionAsync`, `VerdictsAsync` |
| ☐ | **3** Explain and build `$match` → `$sort` → `$limit` | Step 6 | `KindestDeedsAsync` |
| ☐ | **4** Use `$lookup` and `$set` | Step 7 | `LineCostsAsync` |
| ☐ | **5** Access `explain()` plans in the MongoDB Shell and Compass | Steps 8, 9 | `ExplainAsync`, `CreateSeasonStatusIndexAsync` |
| ☐ 🖐️ | **5** …in the shell and in **Compass** — a test can't click, so tick this one yourself | Step 8 | the shell and Compass walk-through |

**Do you have to finish the exercises to take the badge?** No — the badge is open and you can
take it whenever you like. But it assesses these concepts, so doing the steps first is the
easier route.

---

## 📋 From C# to the badge

The assessment is likely to show MongoDB Shell syntax rather than C#. The good news: you wrote
your stages as JSON, so the stage itself is the same in both. The full set lives in
[docs/csharp-to-badge.md](../docs/csharp-to-badge.md).

| What you want | Shell | C# |
|---|---|---|
| Run a pipeline | `db.orders.aggregate([ ... ])` | `_orders.Aggregate(pipeline).ToListAsync()` |
| Filter | `{ $match: { season: "current" } }` | the same JSON; or `.Match(o => o.Season == "current")` |
| One document per array element | `{ $unwind: "$lines" }` | the same JSON |
| Group and add up | `{ $group: { _id: "$child", points: { $sum: "$points" } } }` | the same JSON |
| Reshape or compute | `{ $project: { _id: 0, child: "$_id", points: 1 } }` | the same JSON |
| Sort, then limit | `{ $sort: { points: -1 } }`, `{ $limit: 5 }` | the same JSON; or `.SortByDescending(...).Limit(5)` |
| Join another collection | `{ $lookup: { from, localField, foreignField, as } }` | the same JSON |
| Add fields | `{ $set: { total: ... } }` | the same JSON |
| Count what arrives | `{ $count: "openOrders" }` | the same JSON |
| Explain | `db.orders.explain("executionStats").aggregate([ ... ])` | `RunCommandAsync` with an `explain` command |
| Explain in Compass | **Aggregations** tab → **Explain** | — |

**Aggregation expressions vs query filters.** `$match` takes a *filter* (`{ points: { $gte: 0 } }`).
Inside `$project`, `$set` and `$group`, you write *expressions* (`{ $gte: ["$points", 0] }`), where
field names start with `$`. Same idea, different grammar — remember which one you're in.

---

## 🚀 Level up

Optional. None of this is assessed by the Fundamentals of Data Transformation badge — do it if
you're enjoying yourself. (There's no stretch badge for Part 2, but Step 9 pairs nicely with the
**Indexing Design Fundamentals** one in [Part 1's Level up](../part1-inventory/README.md#-level-up).)

### Badly ordered, well ordered: compare them

Run `explain("executionStats")` (in the shell, or the **Explain** button in Compass) on three
pipelines over the `ledger`, and compare what the server says and what comes back:

```js
// A — well ordered
[ { $match: { season: "current", points: { $gt: 0 } } }, { $sort: { points: -1 } }, { $limit: 5 } ]

// B — $sort and $match swapped
[ { $sort: { points: -1 } }, { $match: { season: "current", points: { $gt: 0 } } }, { $limit: 5 } ]

// C — $limit first
[ { $limit: 5 }, { $sort: { points: -1 } }, { $match: { season: "current", points: { $gt: 0 } } } ]
```

A and B have **identical plans** and return the same five deeds — MongoDB noticed it could run
the `$match` first and did it for you. C returns something quite different (`[4, 3, 2]`), and no
optimiser can help. Why can the server fix B but not C? What does that tell you about when stage
order is a *performance* question, and when it's a *correctness* one?

### Keep the lines `$lookup` dropped

Step 7 lost the unicorn lines because `$unwind` discards a document with nothing to unwind.
Tell it not to:

```js
{ $unwind: { path: "$toy", preserveNullAndEmptyArrays: true } }
```

Now all 50 lines come out, the unicorn ones with no `unitPrice`. What happens to `lineTotal` for
them? Is that better or worse than leaving the line out? (Your `LineCost` type won't cope with a
missing price — what would you change?)

### What are the most-requested toys?

`$group` followed by `$sort` is so common it has its own stage. After an unwind:

```js
{ $unwind: "$lines" }
{ $sortByCount: "$lines.toy" }
```

Try it in Compass. Which toy tops the list? Is it the same one Step 3 put first? `$sortByCount`
counts order **lines**, not quantities — so work out why the two can disagree.

### Both lists in one go

`$facet` runs several pipelines over the *same* input in one round trip. After the verdict
pipeline, split nice from naughty:

```js
{ $facet: {
    nice:    [ { $match: { verdict: "nice" } },    { $sort: { points: -1 } } ],
    naughty: [ { $match: { verdict: "naughty" } }, { $sort: { points:  1 } } ]
} }
```

### Break the tie

If two deeds tied for fifth place in Step 6, which came back would be down to chance. Add a
second sort key — `{ $sort: { points: -1, _id: 1 } }` — so the answer is the same every time.

### What does each *order* cost?

Step 7 gives a row per *line*. Put a `$group` after the `$set` to add the lines back up:

```js
{ $group: { _id: "$orderNumber", total: { $sum: "$lineTotal" } } }
```

Sums of decimals stay exact decimals. Which order is the most expensive?

### Do it with LINQ

Steps 1, 3 and 6 each have a typed alternative in [SOLUTIONS.md](SOLUTIONS.md). Rewrite Step 5
with `AsQueryable()`: group by child, then pick the verdict with an ordinary C# conditional
(`g.Sum(e => e.Points) >= 0 ? "nice" : "naughty"`). The driver turns it into a `$cond` for you.
Print the query (`Console.WriteLine(query)`) and compare it with the JSON you wrote in Step 5 —
what did it call the sum it had to carry between stages? And when an expression *can't* be
translated, you get an exception; that's the day you'll be glad you can write the stage by hand.

---

## What's next

**Part 3A — Finding the Perfect Gift: Search.** The orders are sorted out — but they're written in
the children's own spelling, and a child who asks for a *"dinosor"* has just told you something
`$match` can't hear. Next you'll teach MongoDB to search.
