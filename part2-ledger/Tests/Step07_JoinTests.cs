using MongoDB.Driver;

namespace Part2.Tests;

/// <summary>Step 7: <c>$lookup</c> and <c>$set</c>. Badge objective 4.</summary>
[Collection(LedgerCollection.Name)]
public sealed class Step07_JoinTests : LedgerTestBase
{
    private static List<(string Order, string Toy, int Quantity, decimal UnitPrice, decimal LineTotal)> Expected(IReadOnlyDictionary<string, decimal>? priceOverrides = null)
    {
        var prices = Catalogue.ToDictionary(t => t.Name, t => t.Price);
        if (priceOverrides is not null) foreach (var (k, v) in priceOverrides) prices[k] = v;

        return OpenThisSeason.SelectMany(o => o.Lines.Select(l => (Order: o, Line: l)))
                             .Where(x => prices.ContainsKey(x.Line.Toy))        // an inner join: no price, no row
                             .Select(x => (x.Order.OrderNumber, x.Line.Toy, x.Line.Quantity,
                                           prices[x.Line.Toy], prices[x.Line.Toy] * x.Line.Quantity))
                             .OrderBy(x => x.OrderNumber).ThenBy(x => x.Toy).ToList();
    }

    private static List<(string, string, int, decimal, decimal)> Shape(IEnumerable<Part2.Starter.LineCost> rows) =>
        rows.Select(r => (r.OrderNumber, r.Toy, r.Quantity, r.UnitPrice, r.LineTotal))
            .OrderBy(x => x.OrderNumber).ThenBy(x => x.Toy).ToList();

    [Fact(DisplayName = "Step 7 (obj 4) — LineCostsAsync prices every open order line from the catalogue")]
    public async Task Step07_LineCosts_PricesEveryLineAtTheCataloguePrice()
    {
        var found = await Reports.LineCostsAsync();

        Assert.NotEmpty(found);
        Assert.Equal(Expected(), Shape(found));
    }

    [Fact(DisplayName = "Step 7 (obj 4) — a line for a toy we don't make is left out")]
    public async Task Step07_LineCosts_LeavesOutToysWeDoNotMake()
    {
        var found = await Reports.LineCostsAsync();

        // Koa and Elsa each asked for a Unicorn Saddle. It isn't in the catalogue, so $lookup
        // finds nothing for it — an empty array, not an error — and $unwind drops the line.
        Assert.NotEmpty(found);
        Assert.DoesNotContain(found, r => r.Toy == "Unicorn Saddle");
        Assert.Equal(OpenThisSeason.SelectMany(o => o.Lines).Count() - 2, found.Count);
    }

    [Fact(DisplayName = "Step 7 (obj 4) — prices come from the catalogue as it is NOW, not a copy on the order")]
    public async Task Step07_LineCosts_UsesTodaysPrice()
    {
        // Timber costs rise again. The order slips don't change: they never held a price.
        await Toys.UpdateOneAsync(t => t.Name == "Rocking Horse", Builders<SantasWorkshop.Shared.Models.Toy>.Update.Set(t => t.Price, 100m));

        var found = await Reports.LineCostsAsync();

        var overrides = new Dictionary<string, decimal> { ["Rocking Horse"] = 100m };
        Assert.Equal(Expected(overrides), Shape(found));
        Assert.All(found.Where(r => r.Toy == "Rocking Horse"), r => Assert.Equal(100m, r.UnitPrice));
    }
}
