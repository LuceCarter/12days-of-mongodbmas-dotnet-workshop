namespace Part2.Tests;

/// <summary>Step 6: <c>$match</c> → <c>$sort</c> → <c>$limit</c>. Badge objective 3.</summary>
[Collection(LedgerCollection.Name)]
public sealed class Step06_TopOfTheListTests : LedgerTestBase
{
    private static List<(string Child, int Points)> Expected(int count) =>
        AllLedger.Where(e => e.Season == "current" && e.Points > 0)
                 .OrderByDescending(e => e.Points)
                 .Take(count)
                 .Select(e => (e.Child, e.Points)).ToList();

    [Fact(DisplayName = "Step 6 (obj 3) — KindestDeedsAsync returns the five best deeds, best first")]
    public async Task Step06_KindestDeeds_ReturnsTheBestFive()
    {
        var found = await Reports.KindestDeedsAsync(5);

        // Order matters here, so compare as a sequence. The top five have different points on
        // purpose, so there is only one right answer.
        Assert.Equal(Expected(5), found.Select(e => (e.Child, e.Points)).ToList());
    }

    [Fact(DisplayName = "Step 6 (obj 3) — it's sorted before it's limited, and last season doesn't count")]
    public async Task Step06_KindestDeeds_IsBestFirstAndIgnoresLastSeason()
    {
        var found = await Reports.KindestDeedsAsync(5);

        // Last season somebody rescued a kitten for +12. If that tops your list, the $match is
        // missing the season. If your list isn't descending, $limit ran before $sort.
        Assert.Equal(10, found[0].Points);
        var points = found.Select(e => e.Points).ToList();
        Assert.Equal(points.OrderByDescending(p => p).ToList(), points);
        Assert.All(found, e => Assert.Equal("current", e.Season));
    }

    [Fact(DisplayName = "Step 6 (obj 3) — the number you pass is the number you get")]
    public async Task Step06_KindestDeeds_RespectsTheCount()
    {
        var found = await Reports.KindestDeedsAsync(3);

        Assert.Equal(Expected(3), found.Select(e => (e.Child, e.Points)).ToList());
    }
}
