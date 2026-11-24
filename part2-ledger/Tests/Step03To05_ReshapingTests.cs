namespace Part2.Tests;

/// <summary>Steps 3–5: <c>$match</c> → <c>$unwind</c> → <c>$group</c> → <c>$project</c>.
/// Badge objective 2.</summary>
[Collection(LedgerCollection.Name)]
public sealed class Step03To05_ReshapingTests : LedgerTestBase
{
    private const string Dinosaur = "Tyrannosaurus Floor Puzzle";

    // ---- Step 3 ----------------------------------------------------------------------------

    [Fact(DisplayName = "Step 3 (obj 2) — ToysToMakeAsync adds up the quantity wanted of each toy")]
    public async Task Step03_ToysToMake_TotalsTheQuantityOfEachToy()
    {
        var expected = OpenThisSeason.SelectMany(o => o.Lines)
                                     .GroupBy(l => l.Toy)
                                     .Select(g => (Toy: g.Key, Quantity: g.Sum(l => l.Quantity)))
                                     .OrderBy(x => x.Toy).ToList();

        var found = await Reports.ToysToMakeAsync();

        Assert.NotEmpty(found);
        Assert.Equal(expected, found.OrderBy(d => d.Toy).Select(d => (d.Toy, d.Quantity)).ToList());
    }

    [Fact(DisplayName = "Step 3 (obj 2) — each toy appears once, not once per order line")]
    public async Task Step03_ToysToMake_ListsEachToyOnce()
    {
        var found = await Reports.ToysToMakeAsync();

        Assert.NotEmpty(found);
        Assert.Equal(found.Count, found.Select(d => d.Toy).Distinct().Count());
    }

    [Fact(DisplayName = "Step 3 (obj 2) — packed orders and last season's forgotten orders aren't demand")]
    public async Task Step03_ToysToMake_LeavesOutPackedAndLastSeasonsOrders()
    {
        var found = await Reports.ToysToMakeAsync();

        var everyOrderEver = AllOrders.SelectMany(o => o.Lines).Where(l => l.Toy == Dinosaur).Sum(l => l.Quantity);
        var stillToMake = OpenThisSeason.SelectMany(o => o.Lines).Where(l => l.Toy == Dinosaur).Sum(l => l.Quantity);
        Assert.True(everyOrderEver > stillToMake, "test data should have more dinosaur puzzles ordered than still to make");

        var reported = found.Single(d => d.Toy == Dinosaur).Quantity;
        Assert.True(reported == stillToMake,
            $"Expected {stillToMake} {Dinosaur}s still to make but got {reported}. " +
            "Check the $match: only this season's orders that are still OPEN.");
    }

    // ---- Step 4 ----------------------------------------------------------------------------

    private static List<(string Region, int Quantity)> ExpectedDemand(string toy) =>
        OpenThisSeason.Where(o => o.Lines.Any(l => l.Toy == toy))
                      .GroupBy(o => o.Region)
                      .Select(g => (g.Key, g.Sum(o => o.Lines.Where(l => l.Toy == toy).Sum(l => l.Quantity))))
                      .OrderBy(x => x.Key).ToList();

    [Fact(DisplayName = "Step 4 (obj 2) — DemandByRegionAsync totals one toy's quantity in each region")]
    public async Task Step04_DemandByRegion_TotalsOneToyInEachRegion()
    {
        var found = await Reports.DemandByRegionAsync(Dinosaur);

        Assert.NotEmpty(found);
        Assert.Equal(ExpectedDemand(Dinosaur), found.OrderBy(r => r.Region).Select(r => (r.Region, r.Quantity)).ToList());
    }

    [Fact(DisplayName = "Step 4 (obj 2) — it counts only that toy, not the other things in the same orders")]
    public async Task Step04_DemandByRegion_CountsOnlyThatToysQuantity()
    {
        var found = await Reports.DemandByRegionAsync(Dinosaur);

        // Ingrid (Europe) ordered ONE dinosaur puzzle and a train set. After $unwind, her order is
        // two documents. If Europe reports 2, the train set got counted: the second $match is missing.
        var europe = found.Single(r => r.Region == "Europe").Quantity;
        Assert.True(europe == 1,
            $"Europe wants 1 {Dinosaur} but you reported {europe}. After $unwind, every order line " +
            "is its own document — including the lines for other toys. Filter them out again.");
    }

    [Fact(DisplayName = "Step 4 (obj 2) — a toy nobody has an open order for gives an empty list")]
    public async Task Step04_DemandByRegion_NoOpenDemandReturnsNothing()
    {
        // Only last season's delivered orders ever asked for these.
        var found = await Reports.DemandByRegionAsync("Lead Soldier Set");

        Assert.Empty(found);
    }

    // ---- Step 5 ----------------------------------------------------------------------------

    private static Dictionary<string, (int Points, string Verdict)> ExpectedVerdicts() =>
        AllLedger.Where(e => e.Season == "current")
                 .GroupBy(e => e.Child)
                 .ToDictionary(g => g.Key, g => { var p = g.Sum(e => e.Points); return (p, p >= 0 ? "nice" : "naughty"); });

    [Fact(DisplayName = "Step 5 (obj 2) — VerdictsAsync says nice or naughty for every child")]
    public async Task Step05_Verdicts_SaysNiceOrNaughtyForEveryChild()
    {
        var found = await Reports.VerdictsAsync();

        var expected = ExpectedVerdicts().OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value.Points, kv.Value.Verdict)).ToList();
        var actual = found.OrderBy(v => v.Child).Select(v => (v.Child, v.Points, v.Verdict)).ToList();
        Assert.NotEmpty(found);
        Assert.Equal(expected, actual);
    }

    [Fact(DisplayName = "Step 5 (obj 2) — exactly zero points is nice")]
    public async Task Step05_Verdicts_AScoreOfZeroIsNice()
    {
        var found = await Reports.VerdictsAsync();

        // Emma was a little grumpy about sprouts, then tidied her room: -2 and +2.
        var emma = found.Single(v => v.Child == "Emma");
        Assert.Equal(0, emma.Points);
        Assert.True(emma.Verdict == "nice", "Santa gives the benefit of the doubt: zero is nice. Is your comparison $gte or $gt?");
    }

    [Fact(DisplayName = "Step 5 (obj 2) — only this season counts, so a child with no deeds this season isn't listed")]
    public async Task Step05_Verdicts_IgnoresLastSeason()
    {
        var found = await Reports.VerdictsAsync();

        // Last season Sofia was dreadful and Hemi was an angel. This season it's the other way round.
        Assert.Equal("nice", found.Single(v => v.Child == "Sofia").Verdict);
        Assert.Equal("naughty", found.Single(v => v.Child == "Hemi").Verdict);
        // Charlotte only has deeds from last season.
        Assert.DoesNotContain(found, v => v.Child == "Charlotte");
    }
}
