# Part 1 — The Workshop Inventory

> **Level:** Beginner. No MongoDB experience needed.
> **Time:** 30–60 minutes for the core path.
> **Badge:** CRUD Overview (C# edition)

---

## The story

It is the third of November, and the workshop is in trouble.

For as long as anyone can remember, Santa's toy catalogue has lived in an enormous
leather ledger, written in fountain pen, kept on a lectern by the window. It worked
beautifully for several centuries. It is now four thousand pages long, three elves
have repetitive strain injury, and last Tuesday somebody spilled cocoa on the entire
letter M.

You have been brought in to move the catalogue into MongoDB.

By the end of this part the elves will be able to add a toy, look one up, change a
price, clear out the discontinued lines, find toys by age, price, category and theme,
print shelf labels, and see at a glance what is running low.

---

## Get the code

Everything in this challenge lives in one public repository:

**[github.com/LuceCarter/12days-of-mongodbmas-dotnet-workshop](https://github.com/LuceCarter/12days-of-mongodbmas-dotnet-workshop)**

Get it either way:

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
part1-inventory/
  README.md                       this page
  Starter/WorkshopInventory.cs    ← your code goes here
  Tests/                          the tests (leave them alone)
  SOLUTIONS.md                    answers, if you get stuck
src/Shared/                       the Toy model and seed data
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

---

## How this works

Everything you write goes in
[**`part1-inventory/Starter/WorkshopInventory.cs`**](Starter/WorkshopInventory.cs).
It has twelve methods, each with a `// TODO` and a `throw new NotImplementedException`.
Replace each one.

Run the tests whenever you like:

```bash
dotnet test part1-inventory/Tests
```

They start **red**. Each step turns some of them green. When all 22 pass, you're done.

Stuck? [`SOLUTIONS.md`](SOLUTIONS.md) has every answer with an explanation. Looking
isn't cheating — reading a worked example is how most of us learn this.

---

## The toy

Every toy in the catalogue looks like this:

```csharp
public sealed class Toy
{
    public ObjectId Id { get; set; }
    public string Name { get; set; }          // "Rocking Horse"
    public decimal Price { get; set; }        // 89.99
    public int MinAge { get; set; }           // 2
    public int MaxAge { get; set; }           // 6
    public string Category { get; set; }      // "wooden"
    public bool InStock { get; set; }
    public int StockCount { get; set; }       // 12
    public bool Discontinued { get; set; }
    public List<string> Tags { get; set; }    // ["classic", "wooden", "horse"]
    public string Description { get; set; }
}
```

In MongoDB, that whole object is stored as **one document**. There are no joins to
reassemble it and no separate `toy_tags` table — the tags live inside the toy, as an
array. If you're coming from SQL, that's the single biggest shift.

<details>
<summary>📜 Santa's legacy SQL (flavour only — skip if you like)</summary>

The old ledger, had it ever been normalised, would have needed three tables:

```sql
CREATE TABLE toys (
    id INT PRIMARY KEY, name VARCHAR(100), price DECIMAL(6,2),
    min_age INT, max_age INT, category_id INT,
    in_stock BIT, stock_count INT, discontinued BIT, description TEXT
);
CREATE TABLE categories (id INT PRIMARY KEY, name VARCHAR(50));
CREATE TABLE toy_tags  (toy_id INT, tag VARCHAR(50));
```

In MongoDB it's one collection, `toys`, and one document per toy. We'll show the SQL
beside each step so you can see what's changed. **None of this is assessed** — it's
here because translating from something you know is often the fastest way in.
</details>

---

## Part A — Stocking the shelves

### Step 1 — Add a toy · *objective 1.1*

The elves have finished a new wooden sled. Put it in the catalogue.

Open `part1-inventory/Starter/WorkshopInventory.cs` and implement `AddToyAsync`.

```csharp
await _toys.InsertOneAsync(toy);
```

That's genuinely it. You don't declare the shape of a document first, and you don't
create the collection — MongoDB makes it on first write.

> **Shell:** `db.toys.insertOne({ name: "Wooden Sled", price: 45.00, ... })`
> **SQL was:** `INSERT INTO toys (name, price, ...) VALUES (...)` — plus a row in
> `toy_tags` for every tag.

---

### Step 2 — Find one toy · *objective 1.2*

Mrs Claus wants to know what the Rocking Horse costs these days.

Implement `FindByNameAsync`. This is **implicit equality** — you're not using an
operator, just saying "this field equals this value".

```csharp
var filter = Builders<Toy>.Filter.Eq(t => t.Name, name);
return await _toys.Find(filter).FirstOrDefaultAsync();
```

`FirstOrDefaultAsync()` gives you `null` when nothing matches. `FirstAsync()` would
throw instead.

> **Shell:** `db.toys.findOne({ name: "Rocking Horse" })`
> **SQL was:** `SELECT TOP 1 * FROM toys WHERE name = 'Rocking Horse'`

<details>
<summary>The same thing in LINQ</summary>

```csharp
return await _toys.Find(t => t.Name == name).FirstOrDefaultAsync();
```

Both send exactly `{ "name": "Rocking Horse" }` to the server. We lead with
`Builders` throughout because it lines up almost word-for-word with the shell syntax
the badge assessment uses — but write whichever you prefer in your own code.
</details>

---

### Step 3 — Change a price · *objective 1.3*

Timber costs have gone up. The Rocking Horse is now £79.99.

Implement `UpdatePriceAsync`. An update needs **two** things: a filter saying *which*
document, and an update saying *what to change*.

```csharp
var filter = Builders<Toy>.Filter.Eq(t => t.Name, name);
var update = Builders<Toy>.Update.Set(t => t.Price, newPrice);

var result = await _toys.UpdateOneAsync(filter, update);
return result.ModifiedCount;
```

`Set` is the `$set` operator. It changes **only** the field you name and leaves the
rest of the document alone. That matters more than it looks: without `$set`, you'd
replace the entire document and lose every other field. One of the tests checks
exactly this.

> **Shell:** `db.toys.updateOne({ name: "Rocking Horse" }, { $set: { price: 79.99 } })`
> **SQL was:** `UPDATE toys SET price = 79.99 WHERE name = 'Rocking Horse'`

---

### Step 4 — Clear out the discontinued toys · *objective 1.4*

The lead soldiers are gone, the original jack-in-the-box was too startling, and the
glow stars have been replaced. Remove everything marked discontinued.

Implement `RemoveDiscontinuedAsync`.

```csharp
var filter = Builders<Toy>.Filter.Eq(t => t.Discontinued, true);

var result = await _toys.DeleteManyAsync(filter);
return result.DeletedCount;
```

> ⚠️ **`DeleteMany` with an empty filter deletes the entire collection.** There is no
> confirmation prompt and no undo. Always check your filter before you run a delete.

> **Shell:** `db.toys.deleteMany({ discontinued: true })`
> **SQL was:** `DELETE FROM toys WHERE discontinued = 1`

---

## Part B — Narrowing things down

Up to now every filter has been "this field equals this value". Now we want ranges,
combinations and arrays.

### Step 5 — Toys under £20 · *objective 2.1*

For the bargain shelf. Implement `FindUnderPriceAsync`.

```csharp
var filter = Builders<Toy>.Filter.Lt(t => t.Price, maxPrice);
return await _toys.Find(filter).ToListAsync();
```

The four comparison operators you need:

| Builders | MongoDB | Means |
|---|---|---|
| `Lt` | `$lt` | less than |
| `Lte` | `$lte` | less than or equal |
| `Gt` | `$gt` | greater than |
| `Gte` | `$gte` | greater than or equal |

`Lt` is **exclusive** — a toy priced at exactly £20 won't appear. There's a test for it.

> **Shell:** `db.toys.find({ price: { $lt: 20 } })`
> **SQL was:** `SELECT * FROM toys WHERE price < 20`

---

### Step 6 — Toys for a 7-year-old · *objective 2.1*

A letter has arrived from a seven-year-old. What can we offer?

A toy suits a child when `MinAge <= 7` **and** `MaxAge >= 7`. Implement `FindForAgeAsync`.

```csharp
var filter = Builders<Toy>.Filter.And(
    Builders<Toy>.Filter.Lte(t => t.MinAge, age),
    Builders<Toy>.Filter.Gte(t => t.MaxAge, age));
```

> 🧠 **Check the direction.** It's easy to write `MinAge >= age`, which reads naturally
> in English and is wrong. The toy's *minimum* age must be at or **below** the child's
> age. A test catches this one specifically, because almost everybody writes it
> backwards at least once.

> **Shell:** `db.toys.find({ minAge: { $lte: 7 }, maxAge: { $gte: 7 } })`

---

### Step 7 — In stock, for ages 5 to 8 · *objective 2.2*

Now an explicit `$and`. Implement `FindInStockForAgeBandAsync`.

```csharp
var filter = Builders<Toy>.Filter.And(
    Builders<Toy>.Filter.Eq(t => t.InStock, true),
    Builders<Toy>.Filter.Gte(t => t.MinAge, minAge),
    Builders<Toy>.Filter.Lte(t => t.MaxAge, maxAge));
```

Compare this with Step 6 and notice the comparisons point the *other way*. Step 6 asked
"does this toy's range cover age 7?". Step 7 asks "does this toy's range sit **inside**
ages 5–8?". Same operators, opposite question.

> **Shell:** `db.toys.find({ $and: [ { inStock: true }, { minAge: { $gte: 5 } }, { maxAge: { $lte: 8 } } ] })`

<details>
<summary>Why you rarely see <code>$and</code> written out</summary>

Listing conditions side by side in one document is an implicit AND:

```js
db.toys.find({ inStock: true, minAge: { $gte: 5 } })   // same thing
```

You need explicit `$and` when you want two conditions on the **same field**, because a
document can't have duplicate keys. The badge may well test this, so it's worth knowing
both forms exist.
</details>

---

### Step 8 — The games shelf · *objective 2.2*

Puzzles **or** board games. Implement `FindPuzzlesOrBoardGamesAsync`.

```csharp
var filter = Builders<Toy>.Filter.Or(
    Builders<Toy>.Filter.Eq(t => t.Category, "puzzle"),
    Builders<Toy>.Filter.Eq(t => t.Category, "board game"));
```

Unlike `$and`, **`$or` always has to be written out** — there's no implicit form.

> **Shell:** `db.toys.find({ $or: [ { category: "puzzle" }, { category: "board game" } ] })`
> **SQL was:** `SELECT * FROM toys t JOIN categories c ON ... WHERE c.name IN (...)`

---

### Step 9 — Everything with dinosaurs · *objective 2.3*

**This is the step most worth slowing down for.**

`Tags` is an array: `["dinosaur", "science", "digging"]`. We want every toy tagged
`"dinosaur"`.

In the shell, you write exactly what you'd write for a normal field:

```js
db.toys.find({ tags: "dinosaur" })
```

No operator. No `$elemMatch`. **MongoDB matches the value against *any element* of the
array.** That's the concept the badge is testing, and it's genuinely one of the nicer
things about the document model.

In C#, there's a wrinkle. This doesn't compile:

```csharp
Builders<Toy>.Filter.Eq(t => t.Tags, "dinosaur")   // ❌ CS1660
```

`Tags` is a `List<string>` and `"dinosaur"` is a `string`, so the typed builder won't
accept it. Use `AnyEq`:

```csharp
var filter = Builders<Toy>.Filter.AnyEq(t => t.Tags, tag);
return await _toys.Find(filter).ToListAsync();
```

> 🧠 **`AnyEq` is a C# typing detail, not a different query.** It sends exactly
> `{ tags: "dinosaur" }` — byte for byte the same as the shell. Don't let the method
> name convince you MongoDB needs something special for arrays here. It doesn't.

> **SQL was:** `SELECT t.* FROM toys t JOIN toy_tags tt ON tt.toy_id = t.id WHERE tt.tag = 'dinosaur'`
> — a join, plus `DISTINCT` if a toy could match twice. Here, there's no join at all.

---

## Part C — Shaping the results

### Step 10 — The five cheapest toys in stock · *objective 3.1*

Implement `FindCheapestInStockAsync`.

```csharp
var filter = Builders<Toy>.Filter.Eq(t => t.InStock, true);

return await _toys.Find(filter)
                  .SortBy(t => t.Price)
                  .Limit(count)
                  .ToListAsync();
```

`SortBy` is ascending (`1` in the shell); `SortByDescending` is `-1`.

The sort and the limit both happen **on the server**. It doesn't fetch 25 toys and throw
20 away — it sorts, takes five, and sends five.

> **Shell:** `db.toys.find({ inStock: true }).sort({ price: 1 }).limit(5)`
> **SQL was:** `SELECT TOP 5 * FROM toys WHERE in_stock = 1 ORDER BY price ASC`

---

### Step 11 — Shelf labels · *objective 3.2*

The label printer needs a name and a price. Nothing else. Implement `GetShelfLabelsAsync`.

```csharp
return await _toys.Find(FilterDefinition<Toy>.Empty)
                  .Project(t => new ShelfLabel(t.Name, t.Price))
                  .ToListAsync();
```

`FilterDefinition<Toy>.Empty` is "match everything" — the equivalent of `{}`.

This is a **projection**, and it's about more than tidiness: the fields you leave out
are never sent over the network. With a `Description` on every toy (and, from Part 3B,
a 1,024-number embedding), that difference gets large quickly.

> **Shell:** `db.toys.find({}, { name: 1, price: 1, _id: 0 })`
> **SQL was:** `SELECT name, price FROM toys`

---

### Step 12 — What's running low? · *objective 3.3*

Anything with fewer than five on the shelf needs making. Implement `CountLowStockAsync`.

```csharp
var filter = Builders<Toy>.Filter.Lt(t => t.StockCount, threshold);
return await _toys.CountDocumentsAsync(filter);
```

`CountDocumentsAsync` counts **on the server** and sends back a number. Fetching every
matching document just to call `.Count` on the list would move far more data for the
same answer.

> **Shell:** `db.toys.countDocuments({ stockCount: { $lt: 5 } })`
> **SQL was:** `SELECT COUNT(*) FROM toys WHERE stock_count < 5`

---

## ✅ You're done

```bash
dotnet test part1-inventory/Tests
```

22 green. The catalogue is in MongoDB and the elves can stop hand-writing it.

---

## Badge checklist — CRUD Overview (C# edition)

Every objective the badge assesses, and the step that teaches it.

| ✓ | Objective | Step | Taught by |
|---|---|---|---|
| ☐ | **1.1** Insert a document using `InsertOne()` | Step 1 | `AddToyAsync` |
| ☐ | **1.2** Find a single document using `Find()` and implicit equality | Step 2 | `FindByNameAsync` |
| ☐ | **1.3** Use `$set` with `UpdateOne()` to replace a field's value | Step 3 | `UpdatePriceAsync` |
| ☐ | **1.4** Write a `DeleteMany()` that deletes all documents matching a query | Step 4 | `RemoveDiscontinuedAsync` |
| ☐ | **2.1** Limit results with comparison operators `$gt`, `$lt`, `$gte`, `$lte` | Steps 5, 6 | `FindUnderPriceAsync`, `FindForAgeAsync` |
| ☐ | **2.2** Limit results with logical operators `$and`, `$or` | Steps 7, 8 | `FindInStockForAgeBandAsync`, `FindPuzzlesOrBoardGamesAsync` |
| ☐ | **2.3** Query an array field using an equality constraint with a scalar value | Step 9 | `FindByTagAsync` |
| ☐ | **3.1** Use sort and limit to refine results | Step 10 | `FindCheapestInStockAsync` |
| ☐ | **3.2** Use projection with `Find()` to return selected fields | Step 11 | `GetShelfLabelsAsync` |
| ☐ | **3.3** Use `CountDocuments()` with a filter | Step 12 | `CountLowStockAsync` |

**Do you have to finish the exercises to take the badge?** No — the badge is open and
you can take it whenever you like. But it assesses these concepts, so doing the steps
first is the easier route.

---

## 📋 From C# to the badge

The assessment is likely to show MongoDB Shell syntax rather than C#. Here's the
translation for everything above. The full set lives in
[docs/csharp-to-badge.md](../docs/csharp-to-badge.md).

| What you want | Builders (C#) | Shell |
|---|---|---|
| Insert one | `InsertOneAsync(toy)` | `db.toys.insertOne({ ... })` |
| Find one by equality | `Filter.Eq(t => t.Name, "...")` | `db.toys.findOne({ name: "..." })` |
| Set a field | `Update.Set(t => t.Price, 79.99m)` | `{ $set: { price: 79.99 } }` |
| Delete many | `DeleteManyAsync(filter)` | `db.toys.deleteMany({ ... })` |
| Less than | `Filter.Lt(t => t.Price, 20m)` | `{ price: { $lt: 20 } }` |
| Greater or equal | `Filter.Gte(t => t.MinAge, 5)` | `{ minAge: { $gte: 5 } }` |
| AND | `Filter.And(a, b)` | `{ $and: [ ... ] }` |
| OR | `Filter.Or(a, b)` | `{ $or: [ ... ] }` |
| **Array contains** | `Filter.AnyEq(t => t.Tags, "dinosaur")` | `{ tags: "dinosaur" }` |
| Sort ascending | `.SortBy(t => t.Price)` | `.sort({ price: 1 })` |
| Sort descending | `.SortByDescending(t => t.Price)` | `.sort({ price: -1 })` |
| Limit | `.Limit(5)` | `.limit(5)` |
| Projection | `.Project(t => new { t.Name, t.Price })` | `.project({ name: 1, price: 1, _id: 0 })` |
| Count with filter | `CountDocumentsAsync(filter)` | `db.toys.countDocuments({ ... })` |
| Match everything | `FilterDefinition<Toy>.Empty` | `{}` |

**Want to see what your C# actually sent?** Render the filter:

```csharp
var filter = Builders<Toy>.Filter.AnyEq(t => t.Tags, "dinosaur");

Console.WriteLine(filter.Render(new RenderArgs<Toy>(
    BsonSerializer.SerializerRegistry.GetSerializer<Toy>(),
    BsonSerializer.SerializerRegistry)));
// => { "tags" : "dinosaur" }
```

That trick is worth remembering for the whole series.

---

## 🚀 Level up

Optional. None of this is assessed by the CRUD badge — do it if you're enjoying yourself.

### More write operations

- **`InsertManyAsync`** — seed the whole catalogue in one round trip. (We use it in
  `SeedLoader`; go and read it.)
- **`UpdateManyAsync`** — put every wooden toy up by 10%.
- **`$inc`** — `Update.Inc(t => t.StockCount, -1)` when a toy is packed. Why is this
  safer than reading the value, subtracting one, and writing it back?
- **Upserts** — `new UpdateOptions { IsUpsert = true }` updates if it exists and inserts
  if it doesn't.

### Stretch badge — Indexing Design Fundamentals

Every query you wrote today scanned all 34 documents. That's fine for 34. It isn't for
34 million.

- Run a query with `.Explain()` and find `COLLSCAN` in the output.
- Add an index: `toys.Indexes.CreateOne(new CreateIndexModel<Toy>(Builders<Toy>.IndexKeys.Ascending(t => t.Price)))`
- Run `.Explain()` again. It should now say `IXSCAN`.
- Look up the **ESR guideline** — Equality, Sort, Range — and work out the best compound
  index for Step 10's query (`inStock` equality, `price` sort).
- Think about cost: every index speeds up reads and slows down writes. Which of today's
  twelve queries would you actually index in production?

### Stretch badge — Relational to Document Model

- `Tags` is embedded in the toy. When would you reference instead of embedding?
- Orders arrive in Part 2. Should an order embed its toys, or reference them? What
  changes your answer?
- Add **schema validation** to the `toys` collection so a toy can't be saved without a
  name. Start with `db.createCollection` and a `$jsonSchema` validator.

---

## What's next

**Part 2 — The Naughty/Nice Ledger** (released Dec 5). The catalogue is in place; now
the orders arrive. You'll use aggregation pipelines to work out how many of each toy to
make, which regions need what, and why the order of your pipeline stages decides whether
it finishes in a second or a minute.
