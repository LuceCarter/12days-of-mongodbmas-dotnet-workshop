using MongoDB.Driver;
using SantasWorkshop.Shared.Models;

namespace Part1.Tests;

/// <summary>Steps 1–4: insert, find, update, delete. Badge objectives 1.1–1.4.</summary>
[Collection(WorkshopCollection.Name)]
public sealed class Step01To04_CrudTests : InventoryTestBase
{
    [Fact(DisplayName = "Step 1 (1.1) — AddToyAsync inserts a toy")]
    public async Task Step01_AddToy_InsertsIt()
    {
        var before = await Toys.CountDocumentsAsync(FilterDefinition<Toy>.Empty);

        var sled = new Toy
        {
            Name = "Wooden Sled",
            Price = 45.00m,
            MinAge = 5,
            MaxAge = 12,
            Category = "outdoor",
            InStock = true,
            StockCount = 7,
            Tags = ["winter", "outdoor", "wooden"],
            Description = "A steerable ash sled with steel runners, waxed and ready."
        };

        await Inventory.AddToyAsync(sled);

        var after = await Toys.CountDocumentsAsync(FilterDefinition<Toy>.Empty);
        Assert.Equal(before + 1, after);

        var stored = await Toys.Find(t => t.Name == "Wooden Sled").FirstOrDefaultAsync();
        Assert.NotNull(stored);
        Assert.Equal(45.00m, stored.Price);
        Assert.Equal("outdoor", stored.Category);
        Assert.Contains("winter", stored.Tags);
    }

    [Fact(DisplayName = "Step 2 (1.2) — FindByNameAsync finds the Rocking Horse")]
    public async Task Step02_FindByName_FindsIt()
    {
        var toy = await Inventory.FindByNameAsync("Rocking Horse");

        Assert.NotNull(toy);
        Assert.Equal("Rocking Horse", toy.Name);
        Assert.Equal("wooden", toy.Category);
    }

    [Fact(DisplayName = "Step 2 (1.2) — FindByNameAsync returns null when nothing matches")]
    public async Task Step02_FindByName_ReturnsNullForUnknown()
    {
        var toy = await Inventory.FindByNameAsync("Hoverboard, Actual Flying");

        Assert.Null(toy);
    }

    [Fact(DisplayName = "Step 3 (1.3) — UpdatePriceAsync sets the new price")]
    public async Task Step03_UpdatePrice_ChangesOnlyThatField()
    {
        var before = await Toys.Find(t => t.Name == "Rocking Horse").FirstAsync();

        var modified = await Inventory.UpdatePriceAsync("Rocking Horse", 79.99m);

        Assert.Equal(1, modified);

        var after = await Toys.Find(t => t.Name == "Rocking Horse").FirstAsync();
        Assert.Equal(79.99m, after.Price);

        // $set changes the one field and leaves everything else alone. If you
        // replaced the whole document instead, these will have been wiped.
        Assert.Equal(before.Category, after.Category);
        Assert.Equal(before.MinAge, after.MinAge);
        Assert.Equal(before.Tags, after.Tags);
        Assert.Equal(before.Description, after.Description);
    }

    [Fact(DisplayName = "Step 4 (1.4) — RemoveDiscontinuedAsync deletes exactly the discontinued toys")]
    public async Task Step04_RemoveDiscontinued_DeletesOnlyThose()
    {
        var expected = Catalogue.Count(t => t.Discontinued);
        var survivors = Catalogue.Count - expected;

        var deleted = await Inventory.RemoveDiscontinuedAsync();

        Assert.Equal(expected, deleted);

        // Nothing discontinued left...
        Assert.Equal(0, await Toys.CountDocumentsAsync(t => t.Discontinued));
        // ...and everything else untouched. An empty filter would have emptied the shelf.
        Assert.Equal(survivors, await Toys.CountDocumentsAsync(FilterDefinition<Toy>.Empty));
    }
}
