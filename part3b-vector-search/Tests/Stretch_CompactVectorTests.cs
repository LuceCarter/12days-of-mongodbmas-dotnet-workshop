using MongoDB.Bson;
using MongoDB.Driver;
using Part3B.Starter;
using SantasWorkshop.Shared.Models;

namespace Part3B.Tests;

/// <summary>Stretch S3–S4: shorter vectors and quantized indexes.
/// Badge stretch objective: effects of dimensionality on quality, latency and cost.</summary>
public sealed class Stretch_CompactVectorTests(VectorFixture fixture) : VectorTestBase(fixture)
{
    // Independent of the code under test: the test's own few lines.
    private static float[] Truncated(float[] v, int n)
    {
        var head = v.Take(n).ToArray();
        var norm = Math.Sqrt(head.Sum(x => (double)x * x));
        return head.Select(x => (float)(x / norm)).ToArray();
    }

    private static double Norm(float[] v) => Math.Sqrt(v.Sum(x => (double)x * x));

    // ---- S3 -------------------------------------------------------------------------------

    [Fact(DisplayName = "Stretch S3 — Shorten keeps the first numbers and scales them back to length 1")]
    public void S3_KeepsThePrefixAndRenormalises()
    {
        // [3, 4, 12] cut to two numbers is [3, 4]: length 5, so it needs dividing by 5.
        var shorter = GiftFinder.Shorten([3f, 4f, 12f], 2);

        Assert.Equal(2, shorter.Length);
        Assert.Equal(0.6f, shorter[0], precision: 5);
        Assert.Equal(0.8f, shorter[1], precision: 5);
    }

    [Fact(DisplayName = "Stretch S3 — a real vector shortened to 256 is 256 long, length 1, and points the way its first 256 numbers do")]
    public void S3_ARealVectorShortens()
    {
        var full = VectorOf("Rocking Horse");

        var shorter = GiftFinder.Shorten(full, 256);

        Assert.Equal(256, shorter.Length);
        Assert.Equal(1.0, Norm(shorter), precision: 5);
        Assert.Equal(1.0, Cosine(shorter, full.Take(256).ToArray()), precision: 5);
    }

    [Fact(DisplayName = "Stretch S3 — keeping all of an already-length-1 vector changes nothing")]
    public void S3_KeepingEverythingChangesNothing()
    {
        var full = VectorOf("Dinosaur Dig Kit");

        var same = GiftFinder.Shorten(full, full.Length);

        Assert.Equal(full.Length, same.Length);
        Assert.Equal(1.0, Cosine(same, full), precision: 5);
    }

    [Theory(DisplayName = "Stretch S3 — it won't make a vector longer, or empty")]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(2000)]
    public void S3_RejectsImpossibleLengths(int dimensions)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GiftFinder.Shorten(VectorOf("Rocking Horse"), dimensions));
    }

    private static readonly string[] AllQuestions =
    [
        "something calming for a restless child at bedtime",
        "a gift for a budding palaeontologist",
        "a rainy-day game for the whole family",
        "a toy that makes a little mess and lots of art",
        "look at the night sky",
        "a toy for a very small baby",
        "something to build with my hands",
        "a present for someone who loves the outdoors",
    ];

    /// <summary>How many of the example questions still find the same best toy once every vector is cut to <paramref name="dimensions"/>.</summary>
    private static int QuestionsKeepingTheirWinner(int dimensions)
    {
        // Through the learner's own Shorten: this measurement is only as good as their code.
        var shortened = Catalogue.Select(t => (t.Name, Vector: GiftFinder.Shorten(t.DescriptionEmbedding!, dimensions))).ToList();

        return AllQuestions.Count(question =>
        {
            var winnerWhole = Nearest(VectorForQuestion(question), 1)[0].Name;
            var query = GiftFinder.Shorten(VectorForQuestion(question), dimensions);
            var winnerShort = shortened.OrderByDescending(x => Cosine(query, x.Vector)).First().Name;
            return winnerWhole == winnerShort;
        });
    }

    [Fact(DisplayName = "Stretch S3 — cutting to a quarter of the numbers keeps most winners, but not all: it is a trade, not a free lunch")]
    public void S3_ShorterVectorsAreATrade()
    {
        var at512 = QuestionsKeepingTheirWinner(512);
        var at256 = QuestionsKeepingTheirWinner(256);
        var at64 = QuestionsKeepingTheirWinner(64);

        // Most of the example questions still find the same best toy at a quarter of the numbers ...
        Assert.True(at256 >= 5, $"At 256 dimensions only {at256} of {AllQuestions.Length} questions kept their winner.");

        // ... but not necessarily all of them, and the less you keep the less survives.
        Assert.True(at512 >= at256, $"512 kept {at512} winners and 256 kept {at256}: keeping fewer numbers shouldn't help.");
        Assert.True(at256 > at64, $"256 kept {at256} winners and 64 kept {at64}: expected a clear drop by 64 numbers.");
        Assert.True(at64 < AllQuestions.Length, "Cutting to 64 numbers lost no winner at all, so this example no longer shows the trade.");
    }

    // ---- S4 -------------------------------------------------------------------------------

    private async Task<(IMongoCollection<Toy> Scratch, GiftFinder Yours)> BuildAsync(string name)
    {
        var scratch = await Fixture.FreshScratchCollectionAsync($"scratch_{name}");

        // The short vectors, made by the test's own code, so Step S4 doesn't depend on S3 being finished.
        await scratch.BulkWriteAsync(Catalogue.Select(t => new UpdateOneModel<Toy>(
            Builders<Toy>.Filter.Eq(x => x.Name, t.Name),
            Builders<Toy>.Update.Set(x => x.DescriptionEmbeddingShort, Truncated(t.DescriptionEmbedding!, 256)))));

        return (scratch, new GiftFinder(scratch, embedder: null, indexName: $"unused-{name}"));
    }

    private static async Task<BsonDocument> VectorFieldAsync(IMongoCollection<Toy> collection, string index) =>
        (await (await collection.SearchIndexes.ListAsync(index)).ToListAsync()).Single()["latestDefinition"]["fields"]
            .AsBsonArray.Select(f => f.AsBsonDocument).Single(f => f["type"] == "vector");

    private static async Task<List<Toy>> SearchAsync(IMongoCollection<Toy> collection, string index, System.Linq.Expressions.Expression<Func<Toy, object?>> field, float[] query) =>
        await collection.Aggregate()
            .VectorSearch(field, new QueryVector(query), 3, new VectorSearchOptions<Toy> { IndexName = index, NumberOfCandidates = 100 })
            .ToListAsync();

    [Fact(DisplayName = "Stretch S4 — a 256-dimension index over the shortened vectors answers queries with shortened vectors")]
    public async Task S4_ABuiltIndexAtTwoFiftySixDimensions()
    {
        var (scratch, yours) = await BuildAsync("s4-256");

        await yours.CreateCompactIndexAsync("idx-256", "descriptionEmbeddingShort", 256);

        var field = await VectorFieldAsync(scratch, "idx-256");
        Assert.Equal("descriptionEmbeddingShort", field["path"].AsString);
        Assert.Equal(256, field["numDimensions"].ToInt32());
        Assert.Equal("cosine", field["similarity"].AsString);

        var hits = await SearchAsync(scratch, "idx-256", t => t.DescriptionEmbeddingShort, Truncated(VectorOf("Rocking Horse"), 256));
        Assert.Equal("Rocking Horse", hits[0].Name);
    }

    [Fact(DisplayName = "Stretch S4 — a 1024-dimension vector can't be searched against the 256-dimension index: the dimensions trap again")]
    public async Task S4_TheTrapAppliesHereToo()
    {
        var (scratch, yours) = await BuildAsync("s4-trap");
        await yours.CreateCompactIndexAsync("idx-trap", "descriptionEmbeddingShort", 256);

        var error = await Assert.ThrowsAsync<MongoCommandException>(() =>
            SearchAsync(scratch, "idx-trap", t => t.DescriptionEmbeddingShort, VectorOf("Rocking Horse")));

        Assert.Contains("256", error.Message);
        Assert.Contains("1024", error.Message);
    }

    [Theory(DisplayName = "Stretch S4 — scalar and binary quantization build, keep the winner, and are recorded in the definition")]
    [InlineData("scalar")]
    [InlineData("binary")]
    public async Task S4_QuantizedIndexesKeepTheWinner(string quantization)
    {
        var (scratch, yours) = await BuildAsync($"s4-{quantization}");

        await yours.CreateCompactIndexAsync($"idx-{quantization}", "descriptionEmbedding", 1024, quantization);

        var field = await VectorFieldAsync(scratch, $"idx-{quantization}");
        Assert.Equal(quantization, field["quantization"].AsString);
        Assert.Equal(1024, field["numDimensions"].ToInt32());

        // The best answer to each question is the same with the numbers squeezed.
        foreach (var question in new[] { "something calming for a restless child at bedtime", "a gift for a budding palaeontologist" })
        {
            var squeezed = await SearchAsync(scratch, $"idx-{quantization}", t => t.DescriptionEmbedding, VectorForQuestion(question));
            Assert.Equal(Nearest(VectorForQuestion(question), 1)[0].Name, squeezed[0].Name);
        }
    }

    [Fact(DisplayName = "Stretch S4 — quantization is off unless you ask, and the index still works")]
    public async Task S4_DefaultsToNoQuantization()
    {
        var (scratch, yours) = await BuildAsync("s4-none");

        await yours.CreateCompactIndexAsync("idx-none", "descriptionEmbedding", 1024);

        // The server doesn't echo "none" back (it's the default), so an absent setting means "none".
        Assert.Equal("none", (await VectorFieldAsync(scratch, "idx-none")).GetValue("quantization", "none").AsString);
        Assert.Equal("Rocking Horse", (await SearchAsync(scratch, "idx-none", t => t.DescriptionEmbedding, VectorOf("Rocking Horse")))[0].Name);
    }
}
