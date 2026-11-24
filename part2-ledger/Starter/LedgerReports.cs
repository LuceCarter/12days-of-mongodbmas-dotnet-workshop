using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using SantasWorkshop.Shared.Models;

namespace Part2.Starter;

/// <summary>
/// The workshop's reports. Every method here is one step in the Part 2 README, and one
/// idea the Fundamentals of Data Transformation badge assesses.
/// </summary>
/// <remarks>
/// Replace each <c>throw new NotImplementedException</c> with a real pipeline (and fix the
/// one that is already written but wrong). Run <c>dotnet test part2-ledger/Tests</c> as you
/// go — the tests tell you when a step is right. Stuck? <c>SOLUTIONS.md</c> has the answers.
///
/// Pipelines here are written as JSON — the stages exactly as the MongoDB shell and Compass
/// show them — so what you learn transfers straight to the badge:
/// <code>
/// var pipeline = PipelineDefinition&lt;Order, ToyDemand&gt;.Create(
/// [
///     """{ $match: { season: "current" } }""",
///     """{ $unwind: "$lines" }""",
/// ]);
/// var rows = await _orders.Aggregate(pipeline).ToListAsync();
/// </code>
/// One rule: <b>a stage with a fixed value can be a JSON string, but a stage that contains a
/// variable must be built with <c>BsonDocument</c></b>. Pasting a variable into a JSON string
/// is the aggregation version of SQL injection — a value containing a quote can break out of
/// the string and change the stage. The README shows both side by side.
/// </remarks>
public sealed class LedgerReports(
    IMongoCollection<Order> orders,
    IMongoCollection<LedgerEntry> ledger)
{
    private readonly IMongoCollection<Order> _orders = orders;
    private readonly IMongoCollection<LedgerEntry> _ledger = ledger;

    // ─────────────────────────────────────────────────────────────────────────
    //  Part A — How a pipeline works
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 1 (badge objective 1) — how many orders are still open for <paramref name="season"/>?
    /// </summary>
    /// <remarks>
    /// Shell equivalent:
    /// <c>db.orders.aggregate([ { $match: { season: "current", status: "open" } }, { $count: "openOrders" } ])</c>
    /// </remarks>
    public Task<long> CountOpenOrdersAsync(string season = "current")
    {
        // TODO (Step 1): Build a two-stage pipeline.
        //   $match — orders from `season` whose status is "open"
        //   $count — name the result field (say "openOrders")
        //
        // The $match contains the variable `season`, so build THAT stage with a BsonDocument:
        //   new BsonDocument("$match", new BsonDocument { { "season", season }, { "status", "open" } })
        // and the $count stage can be a plain JSON string: BsonDocument.Parse("""{ $count: "openOrders" }""")
        //
        // Run it with _orders.Aggregate(pipeline).FirstOrDefaultAsync() and read the number out of
        // the document it gives you. (PipelineDefinition<Order, BsonDocument>.Create(...) takes a
        // list of BsonDocument stages.)
        //
        // Hint: think about what comes back when NOTHING matches. The tests ask.
        throw new NotImplementedException("Step 1: $match then $count.");
    }

    /// <summary>
    /// Step 2 (badge objective 1) — each child's total points <em>this season</em>.
    /// </summary>
    /// <remarks>
    /// This one is already written. An elf wrote it and went home, and it returns an empty list.
    /// The stages are all correct. Something about their <b>order</b> is not.
    /// </remarks>
    public async Task<List<ChildScore>> ScoresThisSeasonAsync()
    {
        // BUG (Step 2): read these stages top to bottom, like documents walking down an
        // assembly line. After the first stage, what does a document look like? Does the
        // second stage still have what it needs to do its job?
        //
        // Fix it by moving ONE line. Don't change any stage.
        var pipeline = PipelineDefinition<LedgerEntry, ChildScore>.Create(
        [
            """{ $group: { _id: "$child", points: { $sum: "$points" } } }""",
            """{ $match: { season: "current" } }""",
            """{ $project: { _id: 0, child: "$_id", points: 1 } }""",
        ]);

        return await _ledger.Aggregate(pipeline).ToListAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Part B — What to make, and where
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 3 (badge objective 2) — how many of each toy do the elves still need to make?
    /// Counts this season's <b>open</b> orders only.
    /// </summary>
    /// <remarks>
    /// Shell equivalent: <c>$match</c> → <c>$unwind: "$lines"</c> → <c>$group</c> by toy,
    /// <c>$sum</c> the quantities → <c>$project</c> to <c>{ toy, quantity }</c>.
    /// </remarks>
    public Task<List<ToyDemand>> ToysToMakeAsync()
    {
        // TODO (Step 3): Four stages, in this order.
        //   $match   — season "current", status "open"
        //   $unwind  — "$lines": one document per order LINE instead of one per order
        //   $group   — _id is the toy ("$lines.toy"); quantity is the $sum of "$lines.quantity"
        //   $project — hide _id, and rename it to "toy"; keep "quantity"
        //
        // Result type: ToyDemand (see the bottom of this file).
        throw new NotImplementedException("Step 3: $match, $unwind, $group, $project.");
    }

    /// <summary>
    /// Step 4 (badge objective 2) — where is <paramref name="toy"/> wanted, and how many?
    /// Counts this season's <b>open</b> orders only.
    /// </summary>
    /// <remarks>
    /// Shell equivalent: <c>$match</c> → <c>$unwind</c> → <c>$match</c> → <c>$group</c> by region → <c>$project</c>.
    /// Yes, two <c>$match</c> stages. The README explains why.
    /// </remarks>
    public Task<List<RegionDemand>> DemandByRegionAsync(string toy)
    {
        // TODO (Step 4): Like Step 3, but for ONE toy, grouped by region.
        //   $match   — open, current-season orders that have this toy on any line
        //              (the same trick as Part 1's array match: "lines.toy": toy)
        //              `toy` is a variable, so build the $match stages as BsonDocument — the
        //              element name is "lines.toy", value `toy`.
        //   $unwind  — "$lines"
        //   $match   — ??? Look at the documents after the unwind. Are they all for this toy?
        //   $group   — by "$region", summing "$lines.quantity"
        //   $project — hide _id, rename it to "region"
        throw new NotImplementedException("Step 4: $match, $unwind, $match, $group, $project.");
    }

    /// <summary>
    /// Step 5 (badge objective 2) — is each child on the nice list or the naughty list?
    /// This season's deeds only. A child on exactly zero points is nice.
    /// </summary>
    /// <remarks>
    /// Shell equivalent: <c>$match</c> → <c>$group</c> (sum the points) → <c>$project</c>
    /// with a <c>$cond</c> that works out the verdict.
    /// </remarks>
    public Task<List<ChildVerdict>> VerdictsAsync()
    {
        // TODO (Step 5): Step 2's pipeline, plus a verdict.
        //   $match   — this season's deeds
        //   $group   — total points per child
        //   $project — child, points, and a computed "verdict":
        //                { $cond: [ <is points at least zero?>, "nice", "naughty" ] }
        //
        // Hint: $gte is "greater than or equal". Inside a $project (an aggregation expression),
        // comparisons are written { $gte: ["$points", 0] } — an array, with field names in quotes
        // and a dollar sign. That's different from a $match filter. Worth noticing why.
        throw new NotImplementedException("Step 5: $match, $group, $project with $cond.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Part C — The top of the list
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 6 (badge objective 3) — the <paramref name="count"/> kindest deeds this season,
    /// best first.
    /// </summary>
    /// <remarks>
    /// Shell equivalent:
    /// <c>db.ledger.aggregate([ { $match: { season: "current", points: { $gt: 0 } } }, { $sort: { points: -1 } }, { $limit: 5 } ])</c>
    /// </remarks>
    public Task<List<LedgerEntry>> KindestDeedsAsync(int count)
    {
        // TODO (Step 6): Three stages — and the order is the whole lesson.
        //   $match — this season, and points greater than zero
        //   $sort  — highest points first
        //   $limit — keep `count`
        //
        // This pipeline returns whole LedgerEntry documents, so use
        // PipelineDefinition<LedgerEntry, LedgerEntry>.
        //
        // Hint: $limit takes the number `count`, which is a variable: new BsonDocument("$limit", count).
        // The other two stages are fixed, so JSON strings are fine — turn each into a stage with
        // BsonDocument.Parse(...).
        throw new NotImplementedException("Step 6: $match, $sort, $limit.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Part D — Joining collections
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 7 (badge objective 4) — what will each open order line cost, at today's prices?
    /// A line for a toy that isn't in the catalogue is left out.
    /// </summary>
    /// <remarks>
    /// Shell equivalent: <c>$match</c> → <c>$unwind</c> → <c>$lookup</c> from <c>toys</c> →
    /// <c>$unwind</c> → <c>$set</c> (unit price and line total) → <c>$project</c>.
    /// </remarks>
    public Task<List<LineCost>> LineCostsAsync()
    {
        // TODO (Step 7): Orders don't store prices — the catalogue does. Join them.
        //   $match   — this season's open orders
        //   $unwind  — "$lines"
        //   $lookup  — from "toys", localField "lines.toy", foreignField "name", as "toy"
        //              (the result is always an ARRAY, even when it finds one toy... or none)
        //   $unwind  — "$toy"   (turn that one-element array into the toy itself)
        //   $set     — add unitPrice ("$toy.price") and lineTotal
        //              (use { $multiply: [ "$lines.quantity", "$toy.price" ] })
        //   $project — orderNumber, toy ("$lines.toy"), quantity ("$lines.quantity"),
        //              unitPrice and lineTotal
        throw new NotImplementedException("Step 7: $lookup and $set.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Part E — Looking under the bonnet
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 8 (badge objective 5) — ask the server how it would run a pipeline, and return its
    /// explain output. Works for <em>any</em> list of stages.
    /// </summary>
    /// <remarks>
    /// Shell equivalent: <c>db.orders.explain("executionStats").aggregate([ ... ])</c>
    /// </remarks>
    public Task<BsonDocument> ExplainAsync(IEnumerable<string> stages)
    {
        // TODO (Step 8): The driver has no explain() method for aggregations, so send the command
        // yourself. The command document looks like this:
        //
        //   {
        //     explain: { aggregate: "orders", pipeline: [ ...stages... ], cursor: {} },
        //     verbosity: "executionStats"
        //   }
        //
        // Parse each stage string with BsonDocument.Parse, put them in a BsonArray, and run the
        // command with _orders.Database.RunCommandAsync<BsonDocument>(...).
        throw new NotImplementedException("Step 8: run the explain command.");
    }

    /// <summary>
    /// Step 9 (badge objective 5) — create the index that the "toys to make" pipeline wants, and
    /// return its name.
    /// </summary>
    /// <remarks>
    /// Shell equivalent: <c>db.orders.createIndex({ season: 1, status: 1 })</c>
    /// </remarks>
    public Task<string> CreateSeasonStatusIndexAsync()
    {
        // TODO (Step 9): An index on season, then status — the two fields Step 3's $match tests.
        //
        // Hint: Builders<Order>.IndexKeys.Ascending(...).Ascending(...), wrapped in a
        // CreateIndexModel<Order>, passed to _orders.Indexes.CreateOneAsync(...).
        throw new NotImplementedException("Step 9: create an index on season and status.");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  The shapes your pipelines return. Leave these alone.
//
//  [BsonElement] says which field in the pipeline's output fills each property.
//  [BsonIgnoreExtraElements] says "don't fall over if the output has something else too".
// ─────────────────────────────────────────────────────────────────────────────

[BsonIgnoreExtraElements]
public sealed record ToyDemand(
    [property: BsonElement("toy")] string Toy,
    [property: BsonElement("quantity")] int Quantity);

[BsonIgnoreExtraElements]
public sealed record RegionDemand(
    [property: BsonElement("region")] string Region,
    [property: BsonElement("quantity")] int Quantity);

[BsonIgnoreExtraElements]
public sealed record ChildScore(
    [property: BsonElement("child")] string Child,
    [property: BsonElement("points")] int Points);

[BsonIgnoreExtraElements]
public sealed record ChildVerdict(
    [property: BsonElement("child")] string Child,
    [property: BsonElement("points")] int Points,
    [property: BsonElement("verdict")] string Verdict);

[BsonIgnoreExtraElements]
public sealed record LineCost(
    [property: BsonElement("orderNumber")] string OrderNumber,
    [property: BsonElement("toy")] string Toy,
    [property: BsonElement("quantity")] int Quantity,
    [property: BsonElement("unitPrice"), BsonRepresentation(BsonType.Decimal128)] decimal UnitPrice,
    [property: BsonElement("lineTotal"), BsonRepresentation(BsonType.Decimal128)] decimal LineTotal);
