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
    public static IReadOnlyList<Toy> LoadToys()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames()
                           .Single(n => n.EndsWith("toys.json", StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource '{name}' was not found.");

        return JsonSerializer.Deserialize<List<Toy>>(stream, JsonOptions)
            ?? throw new InvalidOperationException("toys.json deserialised to null.");
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
}
