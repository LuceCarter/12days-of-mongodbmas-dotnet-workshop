using MongoDB.Bson;
using MongoDB.Driver;
using Part3A.Starter;
using SantasWorkshop.Shared.Models;

namespace Part3A.Tests;

/// <summary>Steps 1–2: create a search index, wait for it, run a first <c>$search</c>.
/// Badge objectives 1 and 5.</summary>
public sealed class Step01To02_FirstSearchTests(SearchFixture fixture) : SearchTestBase(fixture)
{
    // ---- Step 1 ----------------------------------------------------------------------------

    [Fact(DisplayName = "Step 1 (obj 1) — CreateDynamicIndexAsync creates a dynamic search index called toys-dynamic")]
    public async Task Step01_CreatesADynamicIndex()
    {
        var scratch = await Fixture.FreshScratchCollectionAsync("scratch_step1");
        var yours = new ToySearch(scratch);

        await yours.CreateDynamicIndexAsync();

        var found = await (await scratch.SearchIndexes.ListAsync(ToySearch.DefaultDynamicIndex)).ToListAsync();
        Assert.Single(found);
        Assert.True(found[0]["latestDefinition"]["mappings"]["dynamic"].AsBoolean,
            "The index exists, but its mapping isn't dynamic. The definition is { mappings: { dynamic: true } }.");
    }

    [Fact(DisplayName = "Step 1 (obj 1) — CreateDynamicIndexAsync waits until the index can answer queries")]
    public async Task Step01_WaitsUntilTheIndexIsQueryable()
    {
        var scratch = await Fixture.FreshScratchCollectionAsync("scratch_step1_wait");
        var yours = new ToySearch(scratch);

        await yours.CreateDynamicIndexAsync();

        // The very first query after the method returns must already work. If it comes back empty,
        // the index was still building: the method returned too early.
        var hits = await scratch.Aggregate()
            .Search(Builders<Toy>.Search.Text(t => t.Description, "dinosaur"), indexName: ToySearch.DefaultDynamicIndex)
            .ToListAsync();

        Assert.NotEmpty(hits);
    }

    // ---- Step 2 ----------------------------------------------------------------------------

    [Fact(DisplayName = "Step 2 (obj 1, 5) — SearchDescriptionsAsync finds toys whose description has the word")]
    public async Task Step02_FindsTheWordInDescriptions()
    {
        var found = await Search.SearchDescriptionsAsync("dinosaur");

        AssertSameToys(ToysMentioning("dinosaur", name: false, tags: false), found,
            "A dynamic index matches whole words only, so the plural 'dinosaurs' isn't a match (Step 5 fixes that).");
    }

    [Fact(DisplayName = "Step 2 (obj 1, 5) — results come back best match first, with a score")]
    public async Task Step02_ReturnsResultsBestFirstWithAScore()
    {
        var found = await Search.SearchDescriptionsAsync("wooden");

        AssertBestFirst(found);
        Assert.True(found.Count > 1, "'wooden' appears in several descriptions.");
    }

    [Fact(DisplayName = "Step 2 (obj 1, 5) — a word nobody uses returns an empty list, not an error")]
    public async Task Step02_NoMatchIsEmpty()
    {
        Assert.Empty(await Search.SearchDescriptionsAsync("xylophone"));
    }
}
