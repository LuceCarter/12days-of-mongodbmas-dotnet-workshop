using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Part3B.Starter;
using SantasWorkshop.Shared.Models;

namespace Part3B.Tests;

/// <summary>Step 4: a vector index with the right number of dimensions, and the three similarities.
/// Badge objectives 3 and 4.</summary>
public sealed class Step04_VectorIndexTests(VectorFixture fixture) : VectorTestBase(fixture)
{
    private async Task<(IMongoCollection<Toy> Scratch, GiftFinder Yours)> BuildAsync(string name, string? similarity = null)
    {
        var scratch = await Fixture.FreshScratchCollectionAsync($"scratch_{name}");
        var yours = new GiftFinder(scratch, embedder: null, indexName: $"idx-{name}");

        if (similarity is null) await yours.CreateVectorIndexAsync();
        else await yours.CreateVectorIndexAsync(similarity);

        return (scratch, yours);
    }

    private static async Task<BsonDocument> FieldAsync(IMongoCollection<Toy> collection, string index, string type, string? path = null)
    {
        var definition = (await (await collection.SearchIndexes.ListAsync(index)).ToListAsync()).Single()["latestDefinition"];
        return definition["fields"].AsBsonArray.Select(f => f.AsBsonDocument)
            .First(f => f["type"] == type && (path is null || f["path"] == path));
    }

    [Fact(DisplayName = "Step 4 (obj 3) — the index declares as many dimensions as the vectors have")]
    public async Task Step04_DeclaresTheRightNumberOfDimensions()
    {
        var (scratch, _) = await BuildAsync("dims");

        var vector = await FieldAsync(scratch, "idx-dims", "vector");
        var declared = vector["numDimensions"].ToInt32();

        Assert.True(declared == VectorFixture.Dimensions,
            $"The index declares {declared} dimensions, but every toy's vector has {VectorFixture.Dimensions}. " +
            "Atlas accepts that and reports the index ready, and then: query it with a real vector and you get " +
            $"'indexed with {declared} dimensions but queried with {VectorFixture.Dimensions}'; query it with " +
            $"{declared} numbers to quieten that and you get nothing, with no error at all. " +
            "numDimensions has to equal the length of what the embedding model produces (Step 1 told you).");
    }

    [Fact(DisplayName = "Step 4 (obj 3) — the index indexes the right field, and answers a query")]
    public async Task Step04_TheIndexAnswersQueries()
    {
        var (scratch, _) = await BuildAsync("answers");
        Assert.Equal("descriptionEmbedding", (await FieldAsync(scratch, "idx-answers", "vector"))["path"].AsString);

        // The first query after the method returns must already work, so the method must have waited.
        var hits = await scratch.Aggregate()
            .VectorSearch(t => t.DescriptionEmbedding, new QueryVector(VectorOf("Rocking Horse")), 3,
                new VectorSearchOptions<Toy> { IndexName = "idx-answers", NumberOfCandidates = 50 })
            .ToListAsync();

        Assert.NotEmpty(hits);
        Assert.Equal("Rocking Horse", hits[0].Name);
    }

    private static async Task<List<Toy>> QueryAsync(IMongoCollection<Toy> collection, string index, FilterDefinition<Toy>? filter = null) =>
        await collection.Aggregate()
            .VectorSearch(t => t.DescriptionEmbedding, new QueryVector(VectorOf("Rocking Horse")), 5,
                new VectorSearchOptions<Toy> { IndexName = index, NumberOfCandidates = 100, Filter = filter })
            .ToListAsync();

    [Fact(DisplayName = "Step 4 (obj 3) — it declares the fields a query can narrow by, and a filtered query works")]
    public async Task Step04_DeclaresTheFilterFields()
    {
        var (scratch, _) = await BuildAsync("filters");

        foreach (var path in new[] { "minAge", "maxAge", "inStock", "category" })
            await FieldAsync(scratch, "idx-filters", "filter", path);

        // Declaring them is only worth anything if the index then works with them.
        var puzzles = await QueryAsync(scratch, "idx-filters", Builders<Toy>.Filter.Eq(t => t.Category, "puzzle"));
        Assert.NotEmpty(puzzles);
        Assert.All(puzzles, t => Assert.Equal("puzzle", t.Category));
    }

    [Fact(DisplayName = "Step 4 (obj 4) — it uses cosine similarity unless told otherwise")]
    public async Task Step04_DefaultsToCosine()
    {
        var (scratch, _) = await BuildAsync("cosine-default");

        Assert.Equal("cosine", (await FieldAsync(scratch, "idx-cosine-default", "vector"))["similarity"].AsString);
        Assert.NotEmpty(await QueryAsync(scratch, "idx-cosine-default"));
    }

    [Theory(DisplayName = "Step 4 (obj 4) — the index scores with the similarity it was built with")]
    [InlineData("cosine")]
    [InlineData("euclidean")]
    [InlineData("dotProduct")]
    public async Task Step04_ScoresWithTheChosenSimilarity(string similarity)
    {
        var (scratch, _) = await BuildAsync($"sim-{similarity}", similarity);
        Assert.Equal(similarity, (await FieldAsync(scratch, $"idx-sim-{similarity}", "vector"))["similarity"].AsString);

        var query = VectorForQuestion("a gift for a budding palaeontologist");
        var hits = await scratch.Aggregate()
            .VectorSearch(t => t.DescriptionEmbedding, new QueryVector(query), 5,
                new VectorSearchOptions<Toy> { IndexName = $"idx-sim-{similarity}", NumberOfCandidates = 100 })
            .Project<ToyScore>("{ _id: 0, name: 1, score: { $meta: 'vectorSearchScore' } }")
            .ToListAsync();

        // The score is the similarity, rescaled to 0..1. This checks the formula in the README.
        Assert.NotEmpty(hits);
        Assert.All(hits, hit => Assert.Equal(
            AtlasScore(similarity, query, VectorOf(hit.Name)), hit.Score, precision: 3));
    }

    private sealed record ToyScore(
        [property: BsonElement("name")] string Name,
        [property: BsonElement("score")] double Score);
}
