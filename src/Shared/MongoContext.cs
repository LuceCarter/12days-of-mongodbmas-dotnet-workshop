using MongoDB.Driver;
using SantasWorkshop.Shared.Models;

namespace SantasWorkshop.Shared;

/// <summary>
/// Works out where MongoDB is and hands back the collections the challenges use.
/// </summary>
/// <remarks>
/// Connection string resolution, first match wins:
/// <list type="number">
///   <item>the <c>MONGODB_URI</c> environment variable (set for you in Codespaces,
///         and what you set yourself if you use the Atlas free tier);</item>
///   <item>the local docker-compose deployment on localhost.</item>
/// </list>
/// </remarks>
public static class MongoContext
{
    /// <summary>What docker-compose.yml gives you.</summary>
    public const string LocalConnectionString =
        "mongodb://workshop:workshop@localhost:27017/?directConnection=true";

    public const string DatabaseName = "santas_workshop";

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("MONGODB_URI") is { Length: > 0 } uri
            ? uri
            : LocalConnectionString;

    /// <summary>True when we fell back to localhost rather than reading MONGODB_URI.</summary>
    public static bool UsingLocalFallback =>
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MONGODB_URI"));

    public static IMongoClient CreateClient()
    {
        var settings = MongoClientSettings.FromConnectionString(ConnectionString);
        // Fail fast with a clear error rather than hanging for 30 seconds when
        // the container isn't running. The doctor relies on this.
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        settings.ApplicationName = "santas-workshop";
        return new MongoClient(settings);
    }

    public static IMongoDatabase GetDatabase(IMongoClient? client = null) =>
        (client ?? CreateClient()).GetDatabase(DatabaseName);

    /// <summary>A database other than the practice one. The Part 2 tests use their own so they
    /// never overwrite the data you are exploring in Compass or the shell.</summary>
    public static IMongoDatabase GetDatabase(IMongoClient client, string databaseName) =>
        client.GetDatabase(databaseName);

    public static IMongoCollection<Toy> GetToys(IMongoClient? client = null) =>
        GetDatabase(client).GetCollection<Toy>("toys");

    public static IMongoCollection<Order> GetOrders(IMongoClient? client = null) =>
        GetDatabase(client).GetCollection<Order>("orders");

    public static IMongoCollection<LedgerEntry> GetLedger(IMongoClient? client = null) =>
        GetDatabase(client).GetCollection<LedgerEntry>("ledger");
}
