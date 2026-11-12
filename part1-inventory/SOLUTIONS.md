# Part 1 — Solutions

Every answer, with the reasoning. **Using this isn't cheating** — reading a worked
example is how most people learn a new API. Try a step first, then come here.

Each solution below is exactly what's verified by the tests.

<details>
<summary><b>Step 1 — AddToyAsync</b> (objective 1.1)</summary>

```csharp
public async Task AddToyAsync(Toy toy)
{
    await _toys.InsertOneAsync(toy);
}
```

There's no schema to declare first and no `CREATE TABLE`. MongoDB creates the
collection on the first write.

After this runs, `toy.Id` is populated — the driver generates the `ObjectId` client-side
and sets it on your object.
</details>

<details>
<summary><b>Step 2 — FindByNameAsync</b> (objective 1.2)</summary>

```csharp
public async Task<Toy?> FindByNameAsync(string name)
{
    var filter = Builders<Toy>.Filter.Eq(t => t.Name, name);
    return await _toys.Find(filter).FirstOrDefaultAsync();
}
```

Or in LINQ:

```csharp
return await _toys.Find(t => t.Name == name).FirstOrDefaultAsync();
```

Both send `{ "name": "Rocking Horse" }`.

`FirstOrDefaultAsync()` returns `null` on no match. `FirstAsync()` throws
`InvalidOperationException`, which is why the test for an unknown toy would fail if you
used it.
</details>

<details>
<summary><b>Step 3 — UpdatePriceAsync</b> (objective 1.3)</summary>

```csharp
public async Task<long> UpdatePriceAsync(string name, decimal newPrice)
{
    var filter = Builders<Toy>.Filter.Eq(t => t.Name, name);
    var update = Builders<Toy>.Update.Set(t => t.Price, newPrice);

    var result = await _toys.UpdateOneAsync(filter, update);
    return result.ModifiedCount;
}
```

**Why `$set` matters.** Without it you'd be replacing the whole document:

```csharp
await _toys.ReplaceOneAsync(filter, new Toy { Name = name, Price = newPrice, ... });
```

That wipes every field you didn't supply. The test asserts `Category`, `MinAge`, `Tags`
and `Description` all survive, so a replace will fail it.

`ModifiedCount` vs `MatchedCount`: if you set the price to the value it already had,
`MatchedCount` is 1 but `ModifiedCount` is 0 — MongoDB noticed nothing changed.
</details>

<details>
<summary><b>Step 4 — RemoveDiscontinuedAsync</b> (objective 1.4)</summary>

```csharp
public async Task<long> RemoveDiscontinuedAsync()
{
    var filter = Builders<Toy>.Filter.Eq(t => t.Discontinued, true);

    var result = await _toys.DeleteManyAsync(filter);
    return result.DeletedCount;
}
```

The test checks two things: that the right number went, **and** that everything else
stayed. `FilterDefinition<Toy>.Empty` here would empty the catalogue and fail the
second check — which is the gentlest possible way to learn that lesson.
</details>

<details>
<summary><b>Step 5 — FindUnderPriceAsync</b> (objective 2.1)</summary>

```csharp
public async Task<List<Toy>> FindUnderPriceAsync(decimal maxPrice)
{
    var filter = Builders<Toy>.Filter.Lt(t => t.Price, maxPrice);
    return await _toys.Find(filter).ToListAsync();
}
```

`Lt` is `$lt`, strictly less than. The test uses £19.99 as a limit and checks the
Pocket Telescope (priced at exactly £19.99) is excluded — `Lte` would let it through.
</details>

<details>
<summary><b>Step 6 — FindForAgeAsync</b> (objective 2.1)</summary>

```csharp
public async Task<List<Toy>> FindForAgeAsync(int age)
{
    var filter = Builders<Toy>.Filter.And(
        Builders<Toy>.Filter.Lte(t => t.MinAge, age),
        Builders<Toy>.Filter.Gte(t => t.MaxAge, age));

    return await _toys.Find(filter).ToListAsync();
}
```

**The direction is the whole point.** A toy suits a 7-year-old when the toy's range
*contains* 7:

- `MinAge <= 7` — it isn't aimed at older children
- `MaxAge >= 7` — it isn't aimed at younger children

Writing `Gte(MinAge, age)` reads fine in English ("minimum age at least 7") and gives
you toys for 7-and-up, which is a different question. The test checks that the Knitted
Snowman (0–4) and Pocket Telescope (8–14) are both absent, and that the Space Explorer
Board Game (7–12) and Tyrannosaurus Floor Puzzle (4–7) are both present.
</details>

<details>
<summary><b>Step 7 — FindInStockForAgeBandAsync</b> (objective 2.2)</summary>

```csharp
public async Task<List<Toy>> FindInStockForAgeBandAsync(int minAge, int maxAge)
{
    var filter = Builders<Toy>.Filter.And(
        Builders<Toy>.Filter.Eq(t => t.InStock, true),
        Builders<Toy>.Filter.Gte(t => t.MinAge, minAge),
        Builders<Toy>.Filter.Lte(t => t.MaxAge, maxAge));

    return await _toys.Find(filter).ToListAsync();
}
```

`Filter.And` takes any number of filters.

Note the comparisons are the **opposite way round** to Step 6. Here we want the toy's
range to sit *inside* 5–8, so the minimum must be at least 5 and the maximum at most 8.
Same operators, different question — worth sitting with for a moment.
</details>

<details>
<summary><b>Step 8 — FindPuzzlesOrBoardGamesAsync</b> (objective 2.2)</summary>

```csharp
public async Task<List<Toy>> FindPuzzlesOrBoardGamesAsync()
{
    var filter = Builders<Toy>.Filter.Or(
        Builders<Toy>.Filter.Eq(t => t.Category, "puzzle"),
        Builders<Toy>.Filter.Eq(t => t.Category, "board game"));

    return await _toys.Find(filter).ToListAsync();
}
```

`Filter.In` does the same job with `$in`:

```csharp
var filter = Builders<Toy>.Filter.In(t => t.Category, ["puzzle", "board game"]);
```

Both pass the test. `$in` is neater for many values on one field; `$or` is what you need
when the conditions are on *different* fields.

Using `And` here returns nothing — a category can't be two things at once.
</details>

<details>
<summary><b>Step 9 — FindByTagAsync</b> (objective 2.3)</summary>

```csharp
public async Task<List<Toy>> FindByTagAsync(string tag)
{
    var filter = Builders<Toy>.Filter.AnyEq(t => t.Tags, tag);
    return await _toys.Find(filter).ToListAsync();
}
```

LINQ works too:

```csharp
return await _toys.Find(t => t.Tags.Contains(tag)).ToListAsync();
```

As does raw BSON, which is exactly what the shell sends:

```csharp
return await _toys.Find(new BsonDocument("tags", tag)).ToListAsync();
```

**All three produce `{ tags: "dinosaur" }`.** This is the key idea: querying an array
by equality is the same syntax as querying a normal field, and MongoDB matches against
any element. Position doesn't matter — the test checks `"wooden"` is found whether it's
the 2nd tag (Rocking Horse) or the 3rd (Woodland Animal Dominoes).

`Filter.Eq(t => t.Tags, tag)` doesn't compile because `Tags` is `List<string>`. `AnyEq`
exists to satisfy C#'s type system, **not** because MongoDB needs a different query.
You do **not** want `$elemMatch` here — that's for matching multiple conditions against
a single array element, which is a different problem.
</details>

<details>
<summary><b>Step 10 — FindCheapestInStockAsync</b> (objective 3.1)</summary>

```csharp
public async Task<List<Toy>> FindCheapestInStockAsync(int count)
{
    var filter = Builders<Toy>.Filter.Eq(t => t.InStock, true);

    return await _toys.Find(filter)
                      .SortBy(t => t.Price)
                      .Limit(count)
                      .ToListAsync();
}
```

`SortBy` ascending (`1`), `SortByDescending` descending (`-1`). Need a second sort key?
Chain `.ThenBy(...)`.

Filter, sort and limit all run **on the server**, in that order. Sorting in C# after
`ToListAsync()` would give the same answer here but would fetch all 25 in-stock toys
first — and wouldn't be able to use an index.
</details>

<details>
<summary><b>Step 11 — GetShelfLabelsAsync</b> (objective 3.2)</summary>

```csharp
public async Task<List<ShelfLabel>> GetShelfLabelsAsync()
{
    return await _toys.Find(FilterDefinition<Toy>.Empty)
                      .Project(t => new ShelfLabel(t.Name, t.Price))
                      .ToListAsync();
}
```

`FilterDefinition<Toy>.Empty` is `{}` — match everything.

The driver reads your projection expression and asks the server for only those fields.
Add `.Project(t => new { t.Name })` and `description` never crosses the network. Once
Part 3B adds a 1,024-float embedding to every toy, that stops being a micro-optimisation.
</details>

<details>
<summary><b>Step 12 — CountLowStockAsync</b> (objective 3.3)</summary>

```csharp
public async Task<long> CountLowStockAsync(int threshold)
{
    var filter = Builders<Toy>.Filter.Lt(t => t.StockCount, threshold);
    return await _toys.CountDocumentsAsync(filter);
}
```

`Lt` again, so the threshold is exclusive: `CountLowStockAsync(5)` counts 4 and below.

`CountDocumentsAsync` returns a `long` from the server. The alternative —
`(await _toys.Find(filter).ToListAsync()).Count` — gives the same number while dragging
every matching document across the network.

There's also `EstimatedDocumentCount()`, which is much faster but reads collection
metadata and **can't take a filter**. Use it for "roughly how many rows", never for an
exact filtered count.
</details>

---

## Still red?

**All 22 failing with a connection error.** MongoDB isn't running:
```bash
docker compose up -d
dotnet run --project tools/doctor
```

**One test failing on an exact-match assertion.** Read the failure message — the tests
print which toy was wrong. Most commonly it's Step 6's comparison direction.

**Step 3 failing on `Category` or `Tags`.** You replaced the document instead of using
`$set`.

**Step 4 deleting everything.** Your filter is empty. The catalogue is reseeded before
every test, so nothing is permanently lost.
