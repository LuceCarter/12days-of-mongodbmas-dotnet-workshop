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

*Added when Part 2 ships.*

## Part 3A — Search

*Added when Part 3A ships.*

## Part 3B — Vector Search

*Added when Part 3B ships.*

## Part 4 — RAG and agents

*Added when Part 4 ships.*
