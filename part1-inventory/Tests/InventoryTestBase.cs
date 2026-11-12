using MongoDB.Driver;
using Part1.Starter;
using SantasWorkshop.Shared;
using SantasWorkshop.Shared.Models;

namespace Part1.Tests;

/// <summary>
/// Gives every test a freshly seeded catalogue and a <see cref="WorkshopInventory"/>
/// to call.
/// </summary>
/// <remarks>
/// The catalogue is reset before <em>each</em> test, so Step 4 deleting the
/// discontinued toys can't upset a test that runs after it. Delete as much as you like.
/// </remarks>
public abstract class InventoryTestBase : IAsyncLifetime
{
    protected IMongoClient Client { get; private set; } = null!;
    protected IMongoCollection<Toy> Toys { get; private set; } = null!;
    protected WorkshopInventory Inventory { get; private set; } = null!;

    /// <summary>The catalogue as it is on disk, for working out expected answers.</summary>
    protected static IReadOnlyList<Toy> Catalogue { get; } = SeedLoader.LoadToys();

    public async ValueTask InitializeAsync()
    {
        Client = MongoContext.CreateClient();

        try
        {
            await SeedLoader.ResetToysAsync(Client);
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

        Toys = MongoContext.GetToys(Client);
        Inventory = new WorkshopInventory(Toys);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
