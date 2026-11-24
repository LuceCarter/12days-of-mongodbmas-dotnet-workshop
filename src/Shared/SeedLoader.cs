using System.Reflection;
using System.Text.Json;
using MongoDB.Driver;
using SantasWorkshop.Shared.Models;

namespace SantasWorkshop.Shared;

/// <summary>
/// Loads the workshop catalogue from the bundled JSON and puts it into MongoDB.
/// </summary>
/// <remarks>
/// Tests call <see cref="ResetToysAsync"/> before each group of steps, so you can
/// delete and update as much as you like without breaking the next test.
/// </remarks>
public static class SeedLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Reads the catalogue out of the embedded resource. No database involved.</summary>
    public static IReadOnlyList<Toy> LoadToys() => Load<Toy>("toys.json");

    /// <summary>Reads the order book (Part 2) out of the embedded resource.</summary>
    public static IReadOnlyList<Order> LoadOrders() => Load<Order>("orders.json");

    /// <summary>Reads the Naughty/Nice Ledger (Part 2) out of the embedded resource.</summary>
    public static IReadOnlyList<LedgerEntry> LoadLedger() => Load<LedgerEntry>("ledger.json");

    private static IReadOnlyList<T> Load<T>(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames()
                           .Single(n => n.EndsWith(fileName, StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource '{name}' was not found.");

        return JsonSerializer.Deserialize<List<T>>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"{fileName} deserialised to null.");
    }

    /// <summary>
    /// Empties the toys collection and refills it from the bundled catalogue.
    /// Returns how many documents were inserted.
    /// </summary>
    public static async Task<int> ResetToysAsync(
        IMongoClient client,
        CancellationToken cancellationToken = default)
    {
        var collection = MongoContext.GetToys(client);
        var toys = LoadToys().ToList();

        // Drop rather than DeleteMany: this also clears the _id values from a
        // previous run, so every test starts from exactly the same catalogue.
        await MongoContext.GetDatabase(client)
            .DropCollectionAsync("toys", cancellationToken);

        await collection.InsertManyAsync(toys, cancellationToken: cancellationToken);
        return toys.Count;
    }

    /// <summary>How many documents <see cref="ResetWorkshopAsync"/> put in each collection.</summary>
    public sealed record SeedCounts(int Toys, int Orders, int Ledger);

    /// <summary>
    /// Empties <c>toys</c>, <c>orders</c> and <c>ledger</c> in <paramref name="database"/> and
    /// refills all three from the bundled data.
    /// </summary>
    /// <remarks>
    /// Collections are dropped rather than emptied, which also removes any indexes. That is what
    /// lets a test that adds an index start the next test from a clean slate.
    /// </remarks>
    public static async Task<SeedCounts> ResetWorkshopAsync(
        IMongoDatabase database,
        CancellationToken cancellationToken = default)
    {
        var toys = LoadToys().ToList();
        var orders = LoadOrders().ToList();
        var ledger = LoadLedger().ToList();

        await database.DropCollectionAsync("toys", cancellationToken);
        await database.DropCollectionAsync("orders", cancellationToken);
        await database.DropCollectionAsync("ledger", cancellationToken);

        await database.GetCollection<Toy>("toys").InsertManyAsync(toys, cancellationToken: cancellationToken);
        await database.GetCollection<Order>("orders").InsertManyAsync(orders, cancellationToken: cancellationToken);
        await database.GetCollection<LedgerEntry>("ledger").InsertManyAsync(ledger, cancellationToken: cancellationToken);

        return new SeedCounts(toys.Count, orders.Count, ledger.Count);
    }
}
