using MongoDB.Driver;
using SantasWorkshop.Shared.Models;

namespace Part1.Starter;

/// <summary>
/// The workshop's toy catalogue. Every method here is one step in the Part 1 README,
/// and one objective on the CRUD Overview badge.
/// </summary>
/// <remarks>
/// Replace each <c>throw new NotImplementedException</c> with real code. Run
/// <c>dotnet test part1-inventory/Tests</c> as you go — the tests tell you when a
/// step is right. Stuck on one? <c>SOLUTIONS.md</c> has the answers.
/// </remarks>
public sealed class WorkshopInventory(IMongoCollection<Toy> toys)
{
    private readonly IMongoCollection<Toy> _toys = toys;

    // ─────────────────────────────────────────────────────────────────────────
    //  Step 1 — Stocking the shelves
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 1 (badge objective 1.1) — add a single toy to the catalogue.
    /// </summary>
    /// <remarks>
    /// Shell equivalent: <c>db.toys.insertOne({ ... })</c>
    /// </remarks>
    public Task AddToyAsync(Toy toy)
    {
        // TODO (Step 1): Insert `toy` into _toys using InsertOneAsync.
        //
        // Hint: the driver fills in the generated _id on `toy` for you afterwards.
        throw new NotImplementedException("Step 1: insert a toy with InsertOneAsync.");
    }

    /// <summary>
    /// Step 2 (badge objective 1.2) — find one toy by its exact name.
    /// Returns null when nothing matches.
    /// </summary>
    /// <remarks>
    /// Shell equivalent: <c>db.toys.findOne({ name: "Rocking Horse" })</c>
    /// </remarks>
    public Task<Toy?> FindByNameAsync(string name)
    {
        // TODO (Step 2): Find the single toy whose Name equals `name`.
        //
        // Builders: Builders<Toy>.Filter.Eq(t => t.Name, name)
        // LINQ:     _toys.Find(t => t.Name == name)
        //
        // Use FirstOrDefaultAsync() so a miss gives you null rather than an exception.
        throw new NotImplementedException("Step 2: find one toy by name.");
    }

    /// <summary>
    /// Step 3 (badge objective 1.3) — change one toy's price.
    /// Returns how many documents were modified.
    /// </summary>
    /// <remarks>
    /// Shell equivalent:
    /// <c>db.toys.updateOne({ name: "..." }, { $set: { price: 79.99 } })</c>
    /// </remarks>
    public Task<long> UpdatePriceAsync(string name, decimal newPrice)
    {
        // TODO (Step 3): Update the toy called `name`, setting Price to `newPrice`.
        //
        // You need two things: a filter (which document) and an update (what to change).
        //   filter: Builders<Toy>.Filter.Eq(t => t.Name, name)
        //   update: Builders<Toy>.Update.Set(t => t.Price, newPrice)
        //
        // UpdateOneAsync returns an UpdateResult. Return its ModifiedCount.
        throw new NotImplementedException("Step 3: set a field with UpdateOneAsync.");
    }

    /// <summary>
    /// Step 4 (badge objective 1.4) — clear out every discontinued toy.
    /// Returns how many were deleted.
    /// </summary>
    /// <remarks>
    /// Shell equivalent: <c>db.toys.deleteMany({ discontinued: true })</c>
    /// </remarks>
    public Task<long> RemoveDiscontinuedAsync()
    {
        // TODO (Step 4): Delete every toy where Discontinued is true.
        //
        // DeleteManyAsync returns a DeleteResult. Return its DeletedCount.
        //
        // Careful: an empty filter would delete the entire catalogue. Elves have
        // done this before. Be specific.
        throw new NotImplementedException("Step 4: delete all discontinued toys.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Step 5–7 — Narrowing things down
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 5 (badge objective 2.1) — every toy under a given price,
    /// for the bargain shelf.
    /// </summary>
    /// <remarks>
    /// Shell equivalent: <c>db.toys.find({ price: { $lt: 20 } })</c>
    /// </remarks>
    public Task<List<Toy>> FindUnderPriceAsync(decimal maxPrice)
    {
        // TODO (Step 5): Find every toy whose Price is strictly less than `maxPrice`.
        //
        // Builders: Builders<Toy>.Filter.Lt(t => t.Price, maxPrice)   -> $lt
        // LINQ:     t => t.Price < maxPrice
        //
        // The family: Lt ($lt), Lte ($lte), Gt ($gt), Gte ($gte).
        throw new NotImplementedException("Step 5: filter with a comparison operator.");
    }

    /// <summary>
    /// Step 6 (badge objective 2.1) — every toy suitable for a child of this age.
    /// A toy fits when MinAge &lt;= age &lt;= MaxAge.
    /// </summary>
    /// <remarks>
    /// Shell equivalent:
    /// <c>db.toys.find({ minAge: { $lte: 7 }, maxAge: { $gte: 7 } })</c>
    /// </remarks>
    public Task<List<Toy>> FindForAgeAsync(int age)
    {
        // TODO (Step 6): Find toys where MinAge <= age AND MaxAge >= age.
        //
        // Two conditions on two different fields. Combine them with
        // Builders<Toy>.Filter.And(...), or just use && in a LINQ expression.
        //
        // Note which way round the operators go — this catches people out.
        // The toy's MINIMUM age must be at or below the child's age.
        throw new NotImplementedException("Step 6: combine two comparisons.");
    }

    /// <summary>
    /// Step 7 (badge objective 2.2) — in-stock toys for a given age band,
    /// using a logical AND.
    /// </summary>
    /// <remarks>
    /// Shell equivalent:
    /// <c>db.toys.find({ $and: [ { inStock: true }, { minAge: { $gte: 5 } }, { maxAge: { $lte: 8 } } ] })</c>
    /// </remarks>
    public Task<List<Toy>> FindInStockForAgeBandAsync(int minAge, int maxAge)
    {
        // TODO (Step 7): Find toys that are in stock AND have
        //                MinAge >= minAge AND MaxAge <= maxAge.
        //
        // Builders<Toy>.Filter.And(filterA, filterB, filterC)
        //
        // This is the age BAND the toy sits in, not whether it suits one child —
        // so both comparisons point inwards. Compare with Step 6.
        throw new NotImplementedException("Step 7: combine filters with And.");
    }

    /// <summary>
    /// Step 8 (badge objective 2.2) — everything from the games shelf:
    /// puzzles or board games, using a logical OR.
    /// </summary>
    /// <remarks>
    /// Shell equivalent:
    /// <c>db.toys.find({ $or: [ { category: "puzzle" }, { category: "board game" } ] })</c>
    /// </remarks>
    public Task<List<Toy>> FindPuzzlesOrBoardGamesAsync()
    {
        // TODO (Step 8): Find toys whose Category is either "puzzle" or "board game".
        //
        // Builders<Toy>.Filter.Or(filterA, filterB)
        //
        // Bonus thought: Filter.In(t => t.Category, ["puzzle", "board game"]) does the
        // same job with $in. Try both and see what each sends.
        throw new NotImplementedException("Step 8: combine filters with Or.");
    }

    /// <summary>
    /// Step 9 (badge objective 2.3) — every toy carrying a given tag.
    /// <c>Tags</c> is an array, and this is the objective most worth understanding.
    /// </summary>
    /// <remarks>
    /// Shell equivalent: <c>db.toys.find({ tags: "dinosaur" })</c> — a plain equality.
    /// </remarks>
    public Task<List<Toy>> FindByTagAsync(string tag)
    {
        // TODO (Step 9): Find every toy whose Tags array contains `tag`.
        //
        // In the shell this is just { tags: "dinosaur" } — MongoDB matches a scalar
        // against ANY element of an array. No special operator needed. That is the
        // concept the badge tests.
        //
        // In C#, Filter.Eq(t => t.Tags, tag) will NOT compile: Tags is List<string>
        // and tag is a string. Use one of these instead:
        //
        //   Builders<Toy>.Filter.AnyEq(t => t.Tags, tag)
        //   _toys.Find(t => t.Tags.Contains(tag))
        //
        // Both send exactly { tags: "dinosaur" }. AnyEq is a C# typing detail,
        // not a different query — don't reach for $elemMatch here.
        throw new NotImplementedException("Step 9: query an array field by equality.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Step 10–12 — Shaping the results
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 10 (badge objective 3.1) — the cheapest in-stock toys, cheapest first.
    /// </summary>
    /// <remarks>
    /// Shell equivalent:
    /// <c>db.toys.find({ inStock: true }).sort({ price: 1 }).limit(5)</c>
    /// </remarks>
    public Task<List<Toy>> FindCheapestInStockAsync(int count)
    {
        // TODO (Step 10): Find in-stock toys, sorted by Price ascending,
        //                 returning at most `count` of them.
        //
        // Chain onto Find(...):  .SortBy(t => t.Price).Limit(count)
        //
        // SortBy is ascending (1 in the shell). SortByDescending is -1.
        throw new NotImplementedException("Step 10: sort and limit.");
    }

    /// <summary>
    /// Step 11 (badge objective 3.2) — name and price only, for printing shelf labels.
    /// The elves don't need the other fields.
    /// </summary>
    /// <remarks>
    /// Shell equivalent:
    /// <c>db.toys.find({}, { name: 1, price: 1, _id: 0 })</c>
    /// </remarks>
    public Task<List<ShelfLabel>> GetShelfLabelsAsync()
    {
        // TODO (Step 11): Return a ShelfLabel (Name + Price) for every toy.
        //
        // .Project(t => new ShelfLabel(t.Name, t.Price))
        //
        // Projection matters for more than tidiness: the fields you leave out are
        // never sent over the network at all.
        throw new NotImplementedException("Step 11: project selected fields.");
    }

    /// <summary>
    /// Step 12 (badge objective 3.3) — how many toys are running low,
    /// so the elves know what to make next.
    /// </summary>
    /// <remarks>
    /// Shell equivalent: <c>db.toys.countDocuments({ stockCount: { $lt: 5 } })</c>
    /// </remarks>
    public Task<long> CountLowStockAsync(int threshold)
    {
        // TODO (Step 12): Count the toys whose StockCount is strictly below `threshold`.
        //
        // CountDocumentsAsync(filter) — note it counts, it doesn't fetch. Much
        // cheaper than pulling every document back just to call .Count on them.
        throw new NotImplementedException("Step 12: count documents matching a filter.");
    }
}

/// <summary>Name and price, for a printed shelf label. Used by Step 11.</summary>
public sealed record ShelfLabel(string Name, decimal Price);
