namespace Part2.Tests;

/// <summary>Steps 1–2: what a pipeline is, and why the order of its stages matters.
/// Badge objective 1.</summary>
[Collection(LedgerCollection.Name)]
public sealed class Step01To02_PipelineBasicsTests : LedgerTestBase
{
    [Fact(DisplayName = "Step 1 (obj 1) — CountOpenOrdersAsync counts this season's open orders")]
    public async Task Step01_CountOpenOrders_CountsThisSeasonsOpenOrders()
    {
        // The data also holds two OPEN orders from last season that the elves forgot to close.
        // They must not be counted: that is what the season in the $match is for.
        long expected = OpenThisSeason.Count();

        var found = await Reports.CountOpenOrdersAsync();

        Assert.Equal(expected, found);
    }

    [Fact(DisplayName = "Step 1 (obj 1) — when nothing matches it returns 0, not null or an error")]
    public async Task Step01_CountOpenOrders_ReturnsZeroWhenNothingMatches()
    {
        // $count outputs NO document at all when nothing reaches it — not a document saying 0.
        // Code that assumes a document always comes back will throw here.
        var found = await Reports.CountOpenOrdersAsync("no-such-season");

        Assert.Equal(0L, found);
    }

    private static Dictionary<string, int> ExpectedScores() =>
        AllLedger.Where(e => e.Season == "current")
                 .GroupBy(e => e.Child)
                 .ToDictionary(g => g.Key, g => g.Sum(e => e.Points));

    [Fact(DisplayName = "Step 2 (obj 1) — ScoresThisSeasonAsync returns a score for every child with deeds")]
    public async Task Step02_Scores_ReturnsEveryChildWithDeedsThisSeason()
    {
        var found = await Reports.ScoresThisSeasonAsync();

        Assert.True(found.Count > 0,
            "The pipeline returned nothing. Every stage is individually fine, so look at what each " +
            "stage hands to the next. After $group, which fields does a document still have? " +
            "Does the next stage still have the field it needs?");
        Assert.Equal(ExpectedScores().Count, found.Count);
    }

    [Fact(DisplayName = "Step 2 (obj 1) — each child's score is their points added up")]
    public async Task Step02_Scores_TotalsEachChildsPoints()
    {
        var found = await Reports.ScoresThisSeasonAsync();

        var expected = ExpectedScores().OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value)).ToList();
        var actual = found.OrderBy(s => s.Child).Select(s => (s.Child, s.Points)).ToList();
        Assert.Equal(expected, actual);
    }

    [Fact(DisplayName = "Step 2 (obj 1) — last season's deeds don't count towards this season's score")]
    public async Task Step02_Scores_IgnoresLastSeasonsDeeds()
    {
        var found = await Reports.ScoresThisSeasonAsync();

        // Sofia did something dreadful last season (-20). This season she has been lovely (+14).
        Assert.Equal(14, found.Single(s => s.Child == "Sofia").Points);
        // Hemi was an angel last season. This season: the cocoa.
        Assert.Equal(-11, found.Single(s => s.Child == "Hemi").Points);
    }
}
