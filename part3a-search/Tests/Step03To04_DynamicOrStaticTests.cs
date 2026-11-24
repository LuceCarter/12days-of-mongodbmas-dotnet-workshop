using Part3A.Starter;

namespace Part3A.Tests;

/// <summary>Steps 3–4: what a dynamic index maps (nothing in particular), and what it can't answer.
/// Badge objective 3.</summary>
public sealed class Step03To04_DynamicOrStaticTests(SearchFixture fixture) : SearchTestBase(fixture)
{
    private static readonly string[] PlannedFields =
        ["name", "description", "tags", "category", "price", "minAge", "maxAge", "inStock", "discontinued"];

    // ---- Step 3 ----------------------------------------------------------------------------

    [Fact(DisplayName = "Step 3 (obj 3) — DescribeIndexAsync says a dynamic index is dynamic and lists no fields")]
    public async Task Step03_ADynamicIndexHasNoListedFields()
    {
        var summary = await Search.DescribeIndexAsync(SearchFixture.ReferenceDynamic);

        Assert.Equal(SearchFixture.ReferenceDynamic, summary.Name);
        Assert.True(summary.Queryable);
        Assert.True(summary.Dynamic, "A dynamic index has mappings.dynamic = true.");
        Assert.Empty(summary.MappedFields);
    }

    [Fact(DisplayName = "Step 3 (obj 3) — DescribeIndexAsync says a static index isn't dynamic and names every mapped field")]
    public async Task Step03_AStaticIndexListsItsFields()
    {
        var summary = await Search.DescribeIndexAsync(SearchFixture.ReferenceStatic);

        Assert.True(summary.Queryable);
        Assert.False(summary.Dynamic, "A static index has mappings.dynamic = false.");
        Assert.Equal(PlannedFields.OrderBy(f => f), summary.MappedFields.OrderBy(f => f));
    }

    // ---- Step 4 ----------------------------------------------------------------------------

    private static List<string> Puzzles => Catalogue.Where(t => t.Category == "puzzle").Select(t => t.Name).ToList();

    [Fact(DisplayName = "Step 4 (obj 3) — FindInCategoryAsync finds every puzzle on an index that maps category as a token")]
    public async Task Step04_EqualsWorksOnAStaticIndex()
    {
        var found = await Search.FindInCategoryAsync("puzzle", SearchFixture.ReferenceStatic);

        Assert.Equal(3, Puzzles.Count);
        AssertSameToys(Puzzles, found);
    }

    [Fact(DisplayName = "Step 4 (obj 3) — the same query finds nothing on a dynamic index. That's the lesson.")]
    public async Task Step04_TheSameQueryFindsNothingOnADynamicIndex()
    {
        // Not a failing query: a correct one, asked of an index that can't answer it.
        var found = await Search.FindInCategoryAsync("puzzle", SearchFixture.ReferenceDynamic);

        Assert.Empty(found);

        // …but the static index, asked the same question, can. Without this the test would also pass
        // for a method that always returns an empty list.
        Assert.NotEmpty(await Search.FindInCategoryAsync("puzzle", SearchFixture.ReferenceStatic));
    }

    [Fact(DisplayName = "Step 4 (obj 3) — a category nobody has returns an empty list")]
    public async Task Step04_UnknownCategoryIsEmpty()
    {
        Assert.Empty(await Search.FindInCategoryAsync("submarine", SearchFixture.ReferenceStatic));
        Assert.NotEmpty(await Search.FindInCategoryAsync("craft", SearchFixture.ReferenceStatic));
    }
}
