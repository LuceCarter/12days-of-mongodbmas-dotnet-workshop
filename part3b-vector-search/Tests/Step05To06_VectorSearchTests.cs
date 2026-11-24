using Part3B.Starter;

namespace Part3B.Tests;

/// <summary>Steps 5–6: <c>$vectorSearch</c>, and narrowing it with a pre-filter. Badge objectives 5 and 6.</summary>
public sealed class Step05To06_VectorSearchTests(VectorFixture fixture) : VectorTestBase(fixture)
{
    // ---- Step 5 ----------------------------------------------------------------------------

    [Fact(DisplayName = "Step 5 (obj 5) — FindSimilarAsync finds the same toys as comparing with every one")]
    public async Task Step05_MatchesTheBruteForceAnswer()
    {
        foreach (var query in new[]
        {
            VectorOf("Pocket Telescope"),
            VectorOf("Knitted Snowman"),
            VectorForQuestion("a rainy-day game for the whole family"),
        })
        {
            var found = await Finder.FindSimilarAsync(query, 5);

            Assert.Equal(Nearest(query, 5).Select(x => x.Name), found.Select(h => h.Name));
            AssertBestFirst(found);
        }
    }

    [Fact(DisplayName = "Step 5 (obj 5) — a toy's own vector finds that toy first, with a score of 1")]
    public async Task Step05_AToyIsItsOwnNearestNeighbour()
    {
        var found = await Finder.FindSimilarAsync(VectorOf("Rocking Horse"), 3);

        Assert.Equal("Rocking Horse", found[0].Name);
        Assert.Equal(1.0, found[0].Score, precision: 3);
    }

    [Fact(DisplayName = "Step 5 (obj 5) — the score is the cosine, rescaled to 0–1")]
    public async Task Step05_TheScoreIsTheRescaledCosine()
    {
        var query = VectorForQuestion("look at the night sky");

        var found = await Finder.FindSimilarAsync(query, 5);

        Assert.NotEmpty(found);
        Assert.All(found, hit => Assert.Equal((1 + Cosine(query, VectorOf(hit.Name))) / 2, hit.Score, precision: 3));
    }

    [Fact(DisplayName = "Step 5 (obj 5) — it honours the count, and asking for more than exist is fine")]
    public async Task Step05_HonoursTheCount()
    {
        var query = VectorOf("Marble Run Starter Set");

        Assert.Single(await Finder.FindSimilarAsync(query, 1));
        Assert.Equal(7, (await Finder.FindSimilarAsync(query, 7)).Count);
        Assert.Equal(Catalogue.Count, (await Finder.FindSimilarAsync(query, 500)).Count);
    }

    // ---- Step 6 ----------------------------------------------------------------------------

    [Fact(DisplayName = "Step 6 (obj 6) — GiftsForAgeAsync only returns toys that suit the age and are in stock")]
    public async Task Step06_OnlyReturnsSuitableToys()
    {
        var found = await Finder.GiftsForAgeAsync(VectorOf("Pocket Telescope"), 3, 5);
        var byName = Catalogue.ToDictionary(t => t.Name);

        Assert.NotEmpty(found);
        Assert.All(found, hit => Assert.True(Suits(byName[hit.Name], 3), $"{hit.Name} doesn't suit a three-year-old, or isn't in stock."));
        AssertBestFirst(found);
    }

    [Fact(DisplayName = "Step 6 (obj 6) — a pre-filter still returns the full count; filtering afterwards doesn't")]
    public async Task Step06_APreFilterKeepsLookingUntilItHasEnough()
    {
        // A telescope is for ages 8 to 14. Ask for three-year-olds' toys that resemble it: the telescope's
        // nearest neighbours are mostly too old, so a search-then-filter loses most of them.
        var query = VectorOf("Pocket Telescope");
        Assert.True(Catalogue.Count(t => Suits(t, 3)) >= 5, "Test data should have at least five toys for a three-year-old.");

        var pre = await Finder.GiftsForAgeAsync(query, 3, 5);
        var post = await Finder.GiftsForAgePostFilteredAsync(query, 3, 5);

        Assert.Equal(5, pre.Count);
        Assert.True(post.Count < 5, $"The post-filter kept {post.Count} of 5: expected the filter to lose some.");
    }

    [Fact(DisplayName = "Step 6 (obj 6) — filtering decides who competes, not how they score")]
    public async Task Step06_FilteringDoesNotChangeAScore()
    {
        var query = VectorForQuestion("something calming for a restless child at bedtime");
        var unfiltered = (await Finder.FindSimilarAsync(query, 500)).ToDictionary(h => h.Name, h => h.Score);

        var filtered = await Finder.GiftsForAgeAsync(query, 5, 8);

        Assert.NotEmpty(filtered);
        Assert.All(filtered, hit => Assert.Equal(unfiltered[hit.Name], hit.Score, precision: 4));
    }

    [Fact(DisplayName = "Step 6 (obj 6) — nothing suits a forty-year-old, and that's an empty list, not an error")]
    public async Task Step06_NothingMatchesTheFilter()
    {
        Assert.Empty(await Finder.GiftsForAgeAsync(VectorOf("Rocking Horse"), 40, 5));
        Assert.NotEmpty(await Finder.GiftsForAgeAsync(VectorOf("Rocking Horse"), 4, 5));
    }
}
