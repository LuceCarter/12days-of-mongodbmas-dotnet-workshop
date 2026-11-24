using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Search;
using Part3A.Starter;
using SantasWorkshop.Shared.Models;

namespace Part3A.Tests;

/// <summary>Steps 5–6: a static mapping with several data types, and the Decimal128 trap.
/// Badge objective 4.</summary>
public sealed class Step05To06_StaticMappingTests(SearchFixture fixture) : SearchTestBase(fixture)
{
    // ---- Step 5 ----------------------------------------------------------------------------

    private static async Task<BsonDocument> DefinitionOfAsync(IMongoCollection<Toy> collection, string index) =>
        (await (await collection.SearchIndexes.ListAsync(index)).ToListAsync()).Single()["latestDefinition"].AsBsonDocument;

    /// <summary>Every <c>type</c> a field was mapped with. A field can have one type, or a list of them.</summary>
    private static List<string> TypesOf(BsonDocument definition, string field)
    {
        var mapping = definition["mappings"]["fields"].AsBsonDocument.GetValue(field, BsonNull.Value);
        return mapping switch
        {
            BsonArray list => list.Select(m => m["type"].AsString).ToList(),
            BsonDocument one => [one["type"].AsString],
            _ => [],
        };
    }

    [Fact(DisplayName = "Step 5 (obj 4) — CreateStaticIndexAsync creates toys-static with dynamic mapping switched off")]
    public async Task Step05_CreatesAStaticIndex()
    {
        var scratch = await Fixture.FreshScratchCollectionAsync("scratch_step5");

        await new ToySearch(scratch).CreateStaticIndexAsync();

        var definition = await DefinitionOfAsync(scratch, ToySearch.DefaultStaticIndex);
        Assert.False(definition["mappings"]["dynamic"].AsBoolean,
            "This is the *static* index: mappings.dynamic must be false, and the fields listed one by one.");
    }

    [Fact(DisplayName = "Step 5 (obj 4) — every field in the plan is mapped with the planned type")]
    public async Task Step05_MapsEachFieldWithTheRightType()
    {
        var scratch = await Fixture.FreshScratchCollectionAsync("scratch_step5_types");
        await new ToySearch(scratch).CreateStaticIndexAsync();
        var definition = await DefinitionOfAsync(scratch, ToySearch.DefaultStaticIndex);

        // The plan table in the README, field by field.
        Assert.Contains("string", TypesOf(definition, "name"));
        Assert.Contains("autocomplete", TypesOf(definition, "name"));
        Assert.Equal(["string"], TypesOf(definition, "description"));
        Assert.Equal(["string"], TypesOf(definition, "tags"));
        Assert.Equal(["token"], TypesOf(definition, "category"));
        Assert.Equal(["number"], TypesOf(definition, "minAge"));
        Assert.Equal(["number"], TypesOf(definition, "maxAge"));
        Assert.Equal(["number"], TypesOf(definition, "price"));
        Assert.Equal(["boolean"], TypesOf(definition, "inStock"));
        Assert.Equal(["boolean"], TypesOf(definition, "discontinued"));

        Assert.Equal("lucene.english", definition["mappings"]["fields"]["description"]["analyzer"].AsString);
    }

    [Fact(DisplayName = "Step 5 (obj 4) — your index can answer exact, stemmed and as-you-type questions")]
    public async Task Step05_TheIndexDoesWhatTheMappingPromises()
    {
        var scratch = await Fixture.FreshScratchCollectionAsync("scratch_step5_works");
        var yours = new ToySearch(scratch);
        await yours.CreateStaticIndexAsync();

        var search = Builders<Toy>.Search;
        async Task<List<string>> NamesAsync(SearchDefinition<Toy> query) =>
            (await scratch.Aggregate().Search(query, indexName: ToySearch.DefaultStaticIndex).ToListAsync())
                .Select(t => t.Name).ToList();

        // token: an exact value
        Assert.Equal(3, (await NamesAsync(search.Equals(t => t.Category, "puzzle"))).Count);

        // English analyzer: the plural finds the singular's relatives
        Assert.Contains("Brachiosaurus Balance Game", await NamesAsync(search.Text(t => t.Description, "dinosaurs")));

        // autocomplete: half a word
        Assert.Contains("Dinosaur Dig Kit", await NamesAsync(search.Autocomplete(t => t.Name, "dino")));

        // number and boolean
        Assert.NotEmpty(await NamesAsync(search.Range(t => t.MinAge, SearchRangeBuilder.Lte(2))));
        Assert.NotEmpty(await NamesAsync(search.Equals(t => t.InStock, false)));
    }

    // ---- Step 6 ----------------------------------------------------------------------------

    private static List<string> SpaceToysUpTo(decimal max) =>
        Catalogue.Where(t => t.Price <= max).Select(t => t.Name).Intersect(ToysMentioning("space")).ToList();

    [Fact(DisplayName = "Step 6 (obj 4) — ⚠️ Atlas Search can't see Decimal128: a range on price finds nothing, and ToysUnderPriceAsync works around it")]
    public async Task Step06_FindsToysUnderThePriceDespiteTheTrap()
    {
        // The trap is real, and it has two layers. Check both FIRST, so a server that one day learns to
        // index Decimal128 turns this test red and tells us the README is out of date, rather than
        // quietly teaching a falsehood.
        var search = Builders<Toy>.Search;

        // Layer 1 — hand the query a decimal and the server refuses it, out loud.
        var refused = await Assert.ThrowsAsync<MongoCommandException>(() => Toys.Aggregate()
            .Search(search.Range(t => t.Price, SearchRangeBuilder.Lte(100m)), indexName: SearchFixture.ReferenceStatic)
            .ToListAsync());
        Assert.Contains("DECIMAL128", refused.Message);

        // Layer 2 — cast it to a double to make the error go away, and the query runs and finds nothing,
        // although every toy in the catalogue costs under £100.
        var cheapDirectly = await Toys.Aggregate()
            .Search(search.Range("price", SearchRangeBuilder.Lte(100d)), indexName: SearchFixture.ReferenceStatic)
            .ToListAsync();
        Assert.True(cheapDirectly.Count == 0,
            $"A range on `price` found {cheapDirectly.Count} toys. It used to find none, because price is a Decimal128 " +
            "and Atlas Search doesn't index that type. If the server now supports it, the README, SOLUTIONS.md and Step 6 need rewriting.");

        var found = await Search.ToysUnderPriceAsync("space", 15m);

        Assert.True(found.Count > 0,
            "ToysUnderPriceAsync found nothing. ⚠️ Atlas Search's `number` type indexes int32, int64 and double, but NOT " +
            "Decimal128, which is what `price` is stored as. The index accepted the mapping and quietly left every price out, " +
            "so the `range` can never match. Take the price check OUT of the $search, and put it in a $match after it. " +
            "(See the ⚠️ box in the README.)");

        Assert.Equal(2, SpaceToysUpTo(15m).Count);
        AssertSameToys(SpaceToysUpTo(15m), found, "'space' toys costing £15 or less.");
    }

    [Fact(DisplayName = "Step 6 (obj 4) — the price limit is inclusive, and a high limit keeps every match, best first")]
    public async Task Step06_ALargeLimitKeepsEveryMatchBestFirst()
    {
        var found = await Search.ToysUnderPriceAsync("space", 1000m);

        AssertSameToys(ToysMentioning("space"), found);
        AssertBestFirst(found);

        var exactly = Catalogue.Single(t => t.Name == "Solar System Puzzle").Price;
        Assert.Contains("Solar System Puzzle", (await Search.ToysUnderPriceAsync("space", exactly)).Select(h => h.Name));
    }

    [Fact(DisplayName = "Step 6 (obj 4) — the limit really cuts: £7 finds the glow stars, £1 finds nothing")]
    public async Task Step06_TheLimitReallyCuts()
    {
        // The first line matters: an empty list is also what a broken method returns for £1.
        Assert.Equal(["Glow Stars, Pack of 100"], (await Search.ToysUnderPriceAsync("space", 7m)).Select(h => h.Name));
        Assert.Empty(await Search.ToysUnderPriceAsync("space", 1m));
    }
}
