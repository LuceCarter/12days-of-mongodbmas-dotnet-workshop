using System.Net;
using System.Text.Json.Nodes;
using Part3B.Starter;

namespace Part3B.Tests;

/// <summary>Steps 7–8: asking the embedding model, then searching with what it says.
/// Badge objectives 2 and 5. No network and no key: a stand-in plays Voyage.</summary>
public sealed class Step07To08_FromQuestionToGiftTests(VectorFixture fixture) : VectorTestBase(fixture)
{
    private const string Key = "pa-test-key-0123456789";

    private static (VoyageEmbedder Embedder, FakeVoyage Voyage) EmbedderReturning(float[] vector)
    {
        var voyage = new FakeVoyage(_ => FakeVoyage.Ok(vector));
        return (new VoyageEmbedder(new HttpClient(voyage), Key), voyage);
    }

    // ---- Step 7 ----------------------------------------------------------------------------

    [Fact(DisplayName = "Step 7 (obj 2) — EmbedAsync asks Voyage's embeddings endpoint with the key, the model and the dimensions")]
    public async Task Step07_SendsTheRightRequest()
    {
        var (embedder, voyage) = EmbedderReturning(VectorOf("Rocking Horse"));

        await embedder.EmbedAsync("a rainy-day game", "query");

        var (request, body) = Assert.Single(voyage.Calls);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(VoyageEmbedder.Endpoint, request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal(Key, request.Headers.Authorization.Parameter);
        Assert.Equal("voyage-3.5", (string?)body["model"]);
        Assert.Equal(1024, (int?)body["output_dimension"]);
        Assert.Equal("a rainy-day game", (string?)body["input"]![0]);
    }

    [Theory(DisplayName = "Step 7 (obj 2) — a stored document and a question are embedded differently")]
    [InlineData("document")]
    [InlineData("query")]
    public async Task Step07_PassesTheInputTypeOn(string inputType)
    {
        var (embedder, voyage) = EmbedderReturning(VectorOf("Rocking Horse"));

        await embedder.EmbedAsync("anything", inputType);

        Assert.Equal(inputType, (string?)voyage.Calls[0].Body["input_type"]);
    }

    [Fact(DisplayName = "Step 7 (obj 2) — it returns the vector Voyage sent back")]
    public async Task Step07_ReturnsTheVector()
    {
        var expected = VectorOf("Knitted Snowman");
        var (embedder, _) = EmbedderReturning(expected);

        var vector = await embedder.EmbedAsync("a soft snowman", "document");

        Assert.Equal(expected, vector);
    }

    [Fact(DisplayName = "Step 7 (obj 2) — a failed call says what Voyage said, and never prints the key")]
    public async Task Step07_ReportsAFailureWithoutLeakingTheKey()
    {
        var voyage = new FakeVoyage(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"detail":"Provided API key is invalid."}"""),
        });
        var embedder = new VoyageEmbedder(new HttpClient(voyage), Key);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => embedder.EmbedAsync("anything", "query"));

        Assert.Contains("401", error.Message);
        Assert.Contains("invalid", error.Message);
        Assert.DoesNotContain(Key, error.Message);
    }

    [Fact(DisplayName = "Step 7 (obj 2) — a vector of the wrong length is caught here, naming both numbers")]
    public async Task Step07_RejectsAVectorOfTheWrongLength()
    {
        var (embedder, _) = EmbedderReturning(new float[1536]);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => embedder.EmbedAsync("anything", "query"));

        Assert.Contains("1536", error.Message);
        Assert.Contains("1024", error.Message);
        Assert.DoesNotContain(Key, error.Message);
    }

    // ---- Step 8 ----------------------------------------------------------------------------

    private GiftFinder FinderWithEmbedderReturning(float[] vector, out FakeVoyage voyage)
    {
        voyage = new FakeVoyage(_ => FakeVoyage.Ok(vector));
        return new GiftFinder(Toys, new VoyageEmbedder(new HttpClient(voyage), Key), VectorFixture.ReferenceIndex);
    }

    [Fact(DisplayName = "Step 8 (obj 2, 5) — FindGiftsAsync embeds the question as a QUERY, then searches with the vector")]
    public async Task Step08_EmbedsTheQuestionThenSearches()
    {
        var query = VectorForQuestion("a gift for a budding palaeontologist");
        var finder = FinderWithEmbedderReturning(query, out var voyage);

        var found = await finder.FindGiftsAsync("a gift for a budding palaeontologist", age: null, count: 5);

        var (_, body) = Assert.Single(voyage.Calls);
        Assert.Equal("query", (string?)body["input_type"]);
        Assert.Equal("a gift for a budding palaeontologist", (string?)body["input"]![0]);
        Assert.Equal(Nearest(query, 5).Select(x => x.Name), found.Select(h => h.Name));
    }

    [Fact(DisplayName = "Step 8 (obj 2, 5) — give it an age and only suitable toys come back")]
    public async Task Step08_AnAgeNarrowsTheSearch()
    {
        var query = VectorForQuestion("a rainy-day game for the whole family");
        var finder = FinderWithEmbedderReturning(query, out _);
        var byName = Catalogue.ToDictionary(t => t.Name);

        var found = await finder.FindGiftsAsync("a rainy-day game for the whole family", age: 4, count: 5);

        Assert.Equal(5, found.Count);
        Assert.All(found, hit => Assert.True(Suits(byName[hit.Name], 4), $"{hit.Name} doesn't suit a four-year-old."));
        Assert.Equal(Nearest(query, 5, t => Suits(t, 4)).Select(x => x.Name), found.Select(h => h.Name));
    }

    [Fact(DisplayName = "Step 8 (obj 2, 5) — without an embedder it says why, rather than failing obscurely")]
    public async Task Step08_NeedsAnEmbedder()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Finder.FindGiftsAsync("anything", null, 3));

        Assert.Contains("embedder", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
