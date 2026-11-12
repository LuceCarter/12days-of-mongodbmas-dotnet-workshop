using MongoDB.Driver;
using SantasWorkshop.Shared.Models;

namespace Part1.Tests;

/// <summary>Steps 10–12: sort and limit, projection, counting.
/// Badge objectives 3.1–3.3.</summary>
[Collection(WorkshopCollection.Name)]
public sealed class Step10To12_ShapingTests : InventoryTestBase
{
    [Fact(DisplayName = "Step 10 (3.1) — FindCheapestInStockAsync sorts ascending and limits")]
    public async Task Step10_FindCheapestInStock_ReturnsTheFiveCheapest()
    {
        var expected = Catalogue.Where(t => t.InStock)
                                .OrderBy(t => t.Price)
                                .Take(5)
                                .Select(t => t.Name)
                                .ToList();

        var found = await Inventory.FindCheapestInStockAsync(5);

        Assert.Equal(5, found.Count);
        // Order matters here, so compare as a sequence rather than a set.
        Assert.Equal(expected, found.Select(t => t.Name).ToList());
        Assert.All(found, t => Assert.True(t.InStock, $"{t.Name} is out of stock"));
    }

    [Fact(DisplayName = "Step 10 (3.1) — the sort is ascending, not descending")]
    public async Task Step10_FindCheapestInStock_IsAscending()
    {
        var found = await Inventory.FindCheapestInStockAsync(5);

        var prices = found.Select(t => t.Price).ToList();
        Assert.Equal(prices.OrderBy(p => p).ToList(), prices);

        // The single cheapest in-stock toy must be first.
        var cheapest = Catalogue.Where(t => t.InStock).MinBy(t => t.Price)!;
        Assert.Equal(cheapest.Name, found[0].Name);
    }

    [Fact(DisplayName = "Step 10 (3.1) — the limit is respected")]
    public async Task Step10_FindCheapestInStock_RespectsTheLimit()
    {
        var found = await Inventory.FindCheapestInStockAsync(3);

        Assert.Equal(3, found.Count);
    }

    [Fact(DisplayName = "Step 11 (3.2) — GetShelfLabelsAsync projects name and price")]
    public async Task Step11_GetShelfLabels_ReturnsNameAndPriceForEveryToy()
    {
        var labels = await Inventory.GetShelfLabelsAsync();

        Assert.Equal(Catalogue.Count, labels.Count);

        var expected = Catalogue.ToDictionary(t => t.Name, t => t.Price);
        foreach (var label in labels)
        {
            Assert.True(expected.ContainsKey(label.Name), $"Unexpected toy: {label.Name}");
            Assert.Equal(expected[label.Name], label.Price);
        }
    }

    [Fact(DisplayName = "Step 11 (3.2) — the Rocking Horse label carries the right price")]
    public async Task Step11_GetShelfLabels_CarriesTheCorrectPrice()
    {
        var labels = await Inventory.GetShelfLabelsAsync();

        var horse = labels.Single(l => l.Name == "Rocking Horse");
        Assert.Equal(89.99m, horse.Price);
    }

    [Fact(DisplayName = "Step 12 (3.3) — CountLowStockAsync counts without fetching")]
    public async Task Step12_CountLowStock_CountsToysBelowTheThreshold()
    {
        var expected = Catalogue.Count(t => t.StockCount < 5);

        var count = await Inventory.CountLowStockAsync(5);

        Assert.Equal(expected, count);
    }

    [Fact(DisplayName = "Step 12 (3.3) — the threshold is exclusive ($lt, not $lte)")]
    public async Task Step12_CountLowStock_IsExclusive()
    {
        var atExactlyFour = Catalogue.Count(t => t.StockCount < 4);
        var atExactlyFive = Catalogue.Count(t => t.StockCount < 5);

        // The catalogue has at least one toy with exactly 4 in stock, so these differ.
        Assert.NotEqual(atExactlyFour, atExactlyFive);

        Assert.Equal(atExactlyFour, await Inventory.CountLowStockAsync(4));
        Assert.Equal(atExactlyFive, await Inventory.CountLowStockAsync(5));
    }

    [Fact(DisplayName = "Step 12 (3.3) — counting reflects deletions")]
    public async Task Step12_CountLowStock_ReflectsTheCurrentCollection()
    {
        var before = await Inventory.CountLowStockAsync(5);

        await Toys.DeleteOneAsync(
            Builders<Toy>.Filter.Lt(t => t.StockCount, 5));

        var after = await Inventory.CountLowStockAsync(5);

        Assert.Equal(before - 1, after);
    }
}
