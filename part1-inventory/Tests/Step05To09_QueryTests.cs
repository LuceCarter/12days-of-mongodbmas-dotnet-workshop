namespace Part1.Tests;

/// <summary>Steps 5–9: comparison operators, logical operators, array equality.
/// Badge objectives 2.1–2.3.</summary>
[Collection(WorkshopCollection.Name)]
public sealed class Step05To09_QueryTests : InventoryTestBase
{
    [Fact(DisplayName = "Step 5 (2.1) — FindUnderPriceAsync uses a $lt comparison")]
    public async Task Step05_FindUnderPrice_ReturnsCheapToys()
    {
        var expected = Catalogue.Where(t => t.Price < 20m).Select(t => t.Name).ToHashSet();

        var found = await Inventory.FindUnderPriceAsync(20m);

        Assert.NotEmpty(found);
        Assert.Equal(expected, found.Select(t => t.Name).ToHashSet());
        Assert.All(found, t => Assert.True(t.Price < 20m, $"{t.Name} costs {t.Price}"));
    }

    [Fact(DisplayName = "Step 5 (2.1) — the boundary price is excluded ($lt, not $lte)")]
    public async Task Step05_FindUnderPrice_IsExclusive()
    {
        // "Pocket Telescope" is exactly 19.99, so a £19.99 limit must exclude it.
        var found = await Inventory.FindUnderPriceAsync(19.99m);

        Assert.DoesNotContain(found, t => t.Name == "Pocket Telescope");
    }

    [Fact(DisplayName = "Step 6 (2.1) — FindForAgeAsync finds toys suitable for a 7-year-old")]
    public async Task Step06_FindForAge_ReturnsToysSpanningThatAge()
    {
        var expected = Catalogue.Where(t => t.MinAge <= 7 && t.MaxAge >= 7)
                                .Select(t => t.Name).ToHashSet();

        var found = await Inventory.FindForAgeAsync(7);

        Assert.NotEmpty(found);
        Assert.Equal(expected, found.Select(t => t.Name).ToHashSet());
        Assert.All(found, t =>
            Assert.True(t.MinAge <= 7 && t.MaxAge >= 7,
                        $"{t.Name} is for ages {t.MinAge}-{t.MaxAge}, which doesn't include 7"));
    }

    [Fact(DisplayName = "Step 6 (2.1) — the comparisons aren't the wrong way round")]
    public async Task Step06_FindForAge_DoesNotInvertTheComparisons()
    {
        // Toys outside the range must not come back for a 7-year-old. They will if
        // you write minAge >= age / maxAge <= age by mistake.
        var found = await Inventory.FindForAgeAsync(7);

        Assert.DoesNotContain(found, t => t.Name == "Knitted Snowman");      // ages 0-4, too young
        Assert.DoesNotContain(found, t => t.Name == "Pocket Telescope");     // ages 8-14, too old

        // ...while toys whose range spans 7 must.
        Assert.Contains(found, t => t.Name == "Space Explorer Board Game");  // ages 7-12
        Assert.Contains(found, t => t.Name == "Tyrannosaurus Floor Puzzle"); // ages 4-7
    }

    [Fact(DisplayName = "Step 7 (2.2) — FindInStockForAgeBandAsync combines filters with $and")]
    public async Task Step07_FindInStockForAgeBand_CombinesAllThreeConditions()
    {
        var expected = Catalogue
            .Where(t => t.InStock && t.MinAge >= 5 && t.MaxAge <= 8)
            .Select(t => t.Name).ToHashSet();

        var found = await Inventory.FindInStockForAgeBandAsync(5, 8);

        Assert.NotEmpty(found);
        Assert.Equal(expected, found.Select(t => t.Name).ToHashSet());
        Assert.All(found, t =>
        {
            Assert.True(t.InStock, $"{t.Name} is out of stock");
            Assert.True(t.MinAge >= 5 && t.MaxAge <= 8,
                        $"{t.Name} is for ages {t.MinAge}-{t.MaxAge}, outside the 5-8 band");
        });
    }

    [Fact(DisplayName = "Step 8 (2.2) — FindPuzzlesOrBoardGamesAsync combines filters with $or")]
    public async Task Step08_FindPuzzlesOrBoardGames_ReturnsBothCategories()
    {
        var expected = Catalogue
            .Where(t => t.Category is "puzzle" or "board game")
            .Select(t => t.Name).ToHashSet();

        var found = await Inventory.FindPuzzlesOrBoardGamesAsync();

        Assert.Equal(expected, found.Select(t => t.Name).ToHashSet());

        // Both categories must actually be represented — an $and here returns nothing,
        // and returning only one category means the Or isn't doing its job.
        Assert.Contains(found, t => t.Category == "puzzle");
        Assert.Contains(found, t => t.Category == "board game");
        Assert.All(found, t => Assert.True(t.Category is "puzzle" or "board game",
                                           $"{t.Name} is a {t.Category}"));
    }

    [Fact(DisplayName = "Step 9 (2.3) — FindByTagAsync matches a scalar against an array field")]
    public async Task Step09_FindByTag_FindsEveryDinosaur()
    {
        var expected = Catalogue.Where(t => t.Tags.Contains("dinosaur"))
                                .Select(t => t.Name).ToHashSet();

        var found = await Inventory.FindByTagAsync("dinosaur");

        Assert.NotEmpty(found);
        Assert.Equal(expected, found.Select(t => t.Name).ToHashSet());
        Assert.All(found, t => Assert.Contains("dinosaur", t.Tags));
    }

    [Fact(DisplayName = "Step 9 (2.3) — it matches any element, not just the first")]
    public async Task Step09_FindByTag_MatchesAnyPositionInTheArray()
    {
        // "wooden" is the 2nd tag on Rocking Horse and the 3rd on Woodland Animal
        // Dominoes. Both must come back — position in the array is irrelevant.
        var found = await Inventory.FindByTagAsync("wooden");

        Assert.Contains(found, t => t.Name == "Rocking Horse");
        Assert.Contains(found, t => t.Name == "Woodland Animal Dominoes");
    }

    [Fact(DisplayName = "Step 9 (2.3) — an unknown tag returns nothing, not everything")]
    public async Task Step09_FindByTag_UnknownTagReturnsEmpty()
    {
        var found = await Inventory.FindByTagAsync("jetpack");

        Assert.Empty(found);
    }
}
