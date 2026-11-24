using MongoDB.Bson;
using MongoDB.Driver;
using Part2.Starter;
using SantasWorkshop.Shared;
using SantasWorkshop.Shared.Models;

namespace Part2.Tests;

/// <summary>
/// Gives every test freshly seeded orders, ledger and catalogue, and a
/// <see cref="LedgerReports"/> to call.
/// </summary>
/// <remarks>
/// <para>The data is reset before <em>each</em> test, and the collections are dropped rather than
/// emptied, so an index created by Step 9 can't leak into the next test.</para>
/// <para>The tests use their <b>own database</b>, <see cref="DatabaseName"/>, rather than the
/// <c>santas_workshop</c> one you explore in Compass. Running the tests never overwrites what
/// you were looking at, and never collides with Part 1's tests.</para>
/// </remarks>
public abstract class LedgerTestBase : IAsyncLifetime
{
    public const string DatabaseName = "santas_workshop_part2_tests";

    protected IMongoClient Client { get; private set; } = null!;
    protected IMongoDatabase Database { get; private set; } = null!;
    protected IMongoCollection<Order> Orders { get; private set; } = null!;
    protected IMongoCollection<LedgerEntry> Ledger { get; private set; } = null!;
    protected IMongoCollection<Toy> Toys { get; private set; } = null!;
    protected LedgerReports Reports { get; private set; } = null!;

    // The data as it is on disk, for working out what the right answer is.
    protected static IReadOnlyList<Order> AllOrders { get; } = SeedLoader.LoadOrders();
    protected static IReadOnlyList<LedgerEntry> AllLedger { get; } = SeedLoader.LoadLedger();
    protected static IReadOnlyList<Toy> Catalogue { get; } = SeedLoader.LoadToys();

    /// <summary>What the elves still have to make: this season, still open.</summary>
    protected static IEnumerable<Order> OpenThisSeason =>
        AllOrders.Where(o => o.Season == "current" && o.Status == "open");

    public async ValueTask InitializeAsync()
    {
        Client = MongoContext.CreateClient();
        Database = MongoContext.GetDatabase(Client, DatabaseName);

        try
        {
            await SeedLoader.ResetWorkshopAsync(Database);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"""

                 Couldn't reach MongoDB, so these tests can't run.

                 Start it from the repo root:
                     docker compose up -d

                 Then check everything is healthy:
                     dotnet run --project tools/doctor

                 (Underlying error: {ex.GetType().Name} — {ex.Message.Split('\n')[0]})

                 """, ex);
        }

        Orders = Database.GetCollection<Order>("orders");
        Ledger = Database.GetCollection<LedgerEntry>("ledger");
        Toys = Database.GetCollection<Toy>("toys");
        Reports = new LedgerReports(Orders, Ledger);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// The "toys to make" pipeline (Step 3) as plain stages. Steps 8 and 9 explain this one: it
    /// filters on season and status, which is exactly what an index on those fields helps with.
    /// </summary>
    protected static readonly string[] ToysToMakeStages =
    [
        """{ $match: { season: "current", status: "open" } }""",
        """{ $unwind: "$lines" }""",
        """{ $group: { _id: "$lines.toy", quantity: { $sum: "$lines.quantity" } } }""",
        """{ $project: { _id: 0, toy: "$_id", quantity: 1 } }""",
    ];

    /// <summary>
    /// The tests' own explain, so Step 9's test doesn't depend on whether you've finished Step 8.
    /// </summary>
    protected Task<BsonDocument> ExplainDirectlyAsync(IEnumerable<string> stages) =>
        Database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "explain", new BsonDocument
                {
                    { "aggregate", "orders" },
                    { "pipeline", new BsonArray(stages.Select(s => (BsonValue)BsonDocument.Parse(s))) },
                    { "cursor", new BsonDocument() },
                } },
            { "verbosity", "executionStats" },
        });
}

/// <summary>Reads things out of an explain document, wherever the server happened to put them.</summary>
/// <remarks>
/// The shape of explain output depends on the pipeline: a lone <c>$match</c> has a top-level
/// <c>queryPlanner</c>, while a longer pipeline nests it inside <c>stages[0].$cursor</c>. Looking
/// for a stage name anywhere in the document is the same trick you'd use by eye in Compass.
/// </remarks>
internal static class Plan
{
    public static bool HasStage(BsonValue value, string stage) => value switch
    {
        BsonDocument d => (d.TryGetValue("stage", out var s) && s.IsString && s.AsString == stage)
                          || d.Values.Any(v => HasStage(v, stage)),
        BsonArray a => a.Any(v => HasStage(v, stage)),
        _ => false,
    };

    /// <summary>Every numeric field called <paramref name="name"/>, in document order.</summary>
    public static IEnumerable<long> Numbers(BsonValue value, string name)
    {
        switch (value)
        {
            case BsonDocument d:
                foreach (var element in d)
                {
                    if (element.Name == name && element.Value.IsNumeric) yield return element.Value.ToInt64();
                    foreach (var n in Numbers(element.Value, name)) yield return n;
                }
                break;
            case BsonArray a:
                foreach (var item in a)
                    foreach (var n in Numbers(item, name)) yield return n;
                break;
        }
    }
}
