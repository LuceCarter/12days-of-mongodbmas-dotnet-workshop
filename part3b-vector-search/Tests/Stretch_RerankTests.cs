using System.Net;
using Part3B.Starter;

namespace Part3B.Tests;

/// <summary>Stretch S1–S2: rerank a short list with a cross-encoder, then do it as a two-stage search.
/// Badge stretch objectives: reranking, and when to use it; using Voyage reranking models.
/// No network and no key: a stand-in plays Voyage.</summary>
public sealed class Stretch_RerankTests(VectorFixture fixture) : VectorTestBase(fixture)
{
    private const string Key = "pa-test-key-0123456789";
    private static readonly string[] Documents = ["a night light", "a dig kit", "a skipping rope"];

    // ---- S1 -------------------------------------------------------------------------------

    [Fact(DisplayName = "Stretch S1 — RerankAsync sends the query, the documents in order, the model and top_k, with the key")]
    public async Task S1_SendsTheRightRequest()
    {
        var voyage = new FakeVoyage(_ => FakeVoyage.Reranked((1, 0.9), (0, 0.4)));
        var reranker = new VoyageReranker(new HttpClient(voyage), Key);

        await reranker.RerankAsync("something for a digger", Documents, topK: 2);

        var (request, body) = Assert.Single(voyage.Calls);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.voyageai.com/v1/rerank", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal(Key, request.Headers.Authorization.Parameter);
        Assert.Equal("something for a digger", (string?)body["query"]);
        Assert.Equal(Documents, body["documents"]!.AsArray().Select(d => (string?)d).ToArray());
        Assert.Equal(VoyageReranker.DefaultModel, (string?)body["model"]);
        Assert.Equal(2, (int?)body["top_k"]);
    }

    [Fact(DisplayName = "Stretch S1 — it returns the rows in the order Voyage ranked them, each pointing at the document it's about")]
    public async Task S1_ReturnsTheRankedRows()
    {
        // Voyage says document 2 is the best match, then document 0: not the order they were sent in.
        var voyage = new FakeVoyage(_ => FakeVoyage.Reranked((2, 0.91), (0, 0.55)));
        var reranker = new VoyageReranker(new HttpClient(voyage), Key);

        var ranked = await reranker.RerankAsync("anything", Documents, topK: 2);

        Assert.Equal([new Reranked(2, 0.91), new Reranked(0, 0.55)], ranked);
    }

    [Fact(DisplayName = "Stretch S1 — it talks to whichever host it's given")]
    public async Task S1_UsesTheHostItIsGiven()
    {
        var voyage = new FakeVoyage(_ => FakeVoyage.Reranked((0, 0.5)));
        var reranker = new VoyageReranker(new HttpClient(voyage), Key, baseUrl: "https://ai.mongodb.com");

        await reranker.RerankAsync("anything", Documents, topK: 1);

        Assert.Equal("https://ai.mongodb.com/v1/rerank", voyage.Calls[0].Request.RequestUri!.ToString());
    }

    [Fact(DisplayName = "Stretch S1 — a failed call says what Voyage said, and never prints the key")]
    public async Task S1_ReportsAFailureWithoutLeakingTheKey()
    {
        var voyage = new FakeVoyage(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("""{"detail":"You have exceeded the rate limit."}"""),
        });
        var reranker = new VoyageReranker(new HttpClient(voyage), Key);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => reranker.RerankAsync("anything", Documents, 2));

        Assert.Contains("429", error.Message);
        Assert.Contains("rate limit", error.Message);
        Assert.DoesNotContain(Key, error.Message);
    }

    // ---- S2 -------------------------------------------------------------------------------

    private const string Question = "something calming for a restless child at bedtime";

    private GiftFinder TwoStageFinder(FakeVoyage embedVoyage, FakeVoyage rerankVoyage) =>
        new(Toys, new VoyageEmbedder(new HttpClient(embedVoyage), Key), VectorFixture.ReferenceIndex,
            new VoyageReranker(new HttpClient(rerankVoyage), Key));

    private static FakeVoyage EmbeddingFake() => new(_ => FakeVoyage.Ok(VectorForQuestion(Question)));

    [Fact(DisplayName = "Stretch S2 — the reranker is shown the vector search's candidates, in order, and asked for just the ones to keep")]
    public async Task S2_TheRerankerSeesTheCandidatesInOrder()
    {
        var rerank = new FakeVoyage(_ => FakeVoyage.Reranked((0, 0.9), (1, 0.8), (2, 0.7)));
        var finder = TwoStageFinder(EmbeddingFake(), rerank);

        await finder.FindThenRerankAsync(Question, candidates: 10, keep: 3);

        var (_, body) = Assert.Single(rerank.Calls);
        var candidatesInOrder = Nearest(VectorForQuestion(Question), 10).Select(x => x.Name).ToList();
        var descriptions = candidatesInOrder.Select(n => Catalogue.Single(t => t.Name == n).Description).ToList();

        Assert.Equal(Question, (string?)body["query"]);
        Assert.Equal(descriptions, body["documents"]!.AsArray().Select(d => (string)d!).ToList());
        Assert.Equal(3, (int?)body["top_k"]);
    }

    [Fact(DisplayName = "Stretch S2 — the result follows the reranker, not the vector search, and the score is its relevance")]
    public async Task S2_TheResultFollowsTheReranker()
    {
        // The reranker disagrees with the vector search: it puts the candidate that was 4th (index 3) first,
        // then the 1st (index 0), then the 6th (index 5).
        var rerank = new FakeVoyage(_ => FakeVoyage.Reranked((3, 0.93), (0, 0.71), (5, 0.20)));
        var finder = TwoStageFinder(EmbeddingFake(), rerank);

        var found = await finder.FindThenRerankAsync(Question, candidates: 10, keep: 3);

        var candidates = Nearest(VectorForQuestion(Question), 10).Select(x => x.Name).ToList();
        Assert.Equal([candidates[3], candidates[0], candidates[5]], found.Select(h => h.Name));
        Assert.Equal([0.93, 0.71, 0.20], found.Select(h => h.Score));
    }

    [Fact(DisplayName = "Stretch S2 — without a reranker it says so, rather than failing obscurely")]
    public async Task S2_NeedsAReranker()
    {
        var finder = new GiftFinder(Toys, new VoyageEmbedder(new HttpClient(EmbeddingFake()), Key), VectorFixture.ReferenceIndex);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => finder.FindThenRerankAsync(Question, 10, 3));

        Assert.Contains("rerank", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
