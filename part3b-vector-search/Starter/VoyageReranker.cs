using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Part3B.Starter;

/// <summary>
/// Puts a short list of candidates into a better order by asking a Voyage <b>reranking</b> model.
/// Stretch step S1 is writing <see cref="RerankAsync"/>.
/// </summary>
/// <remarks>
/// A vector search compares a question with each document <i>separately</i>: the document's vector was made
/// before anyone asked anything. A <b>reranker</b> is a <i>cross-encoder</i>: it reads the question and one
/// candidate <b>together</b>, so it can judge how well that candidate answers <i>this</i> question. It's slower
/// and costs a call, so you don't run it over everything: you run it over the few dozen best candidates
/// that a fast first stage (vector search, or a text search) already found.
///
/// You don't need a key for the exercises: the tests use a stand-in for the network.
/// </remarks>
public sealed class VoyageReranker(
    HttpClient http,
    string apiKey,
    string model = VoyageReranker.DefaultModel,
    string baseUrl = VoyageEmbedder.DefaultBaseUrl)
{
    /// <summary>The highest-accuracy current reranking model. <c>rerank-3-lite</c> trades some accuracy for speed.</summary>
    public const string DefaultModel = "rerank-3";

    private readonly HttpClient _http = http;
    private readonly string _apiKey = apiKey;
    private readonly string _model = model;
    private readonly string _endpoint = baseUrl.TrimEnd('/') + "/v1/rerank";

    /// <summary>A real reranker, using <c>VOYAGE_API_KEY</c> and <c>VOYAGE_BASE_URL</c> like <see cref="VoyageEmbedder.FromEnvironment"/>.</summary>
    public static VoyageReranker FromEnvironment() =>
        Environment.GetEnvironmentVariable("VOYAGE_API_KEY") is { Length: > 0 } key
            ? new VoyageReranker(new HttpClient(), key, baseUrl: VoyageHost.FromEnvironment())
            : throw new InvalidOperationException(
                "VOYAGE_API_KEY isn't set. You don't need it for the exercises; to call Voyage for real, set it " +
                "(and VOYAGE_BASE_URL=https://ai.mongodb.com if your key was made in MongoDB Atlas).");

    /// <summary>
    /// Stretch S1 (badge objectives: reranking, and when to use it; using Voyage reranking models) — ask the
    /// reranking model how well each of <paramref name="documents"/> answers <paramref name="query"/>, and
    /// return the best <paramref name="topK"/>, most relevant first.
    /// </summary>
    public Task<List<Reranked>> RerankAsync(string query, IReadOnlyList<string> documents, int topK)
    {
        // TODO (Stretch S1): POST to _endpoint with:
        //   header   Authorization: Bearer <the key>
        //   body     { "query": query, "documents": [ ... ], "model": _model, "top_k": topK }
        //
        // The reply looks like  { "data": [ { "index": 2, "relevance_score": 0.81 }, ... ], "usage": { ... } }
        // `index` says WHICH of your documents that row is about (its position in the list you sent), and the
        // rows come back most relevant first. Return each as  new Reranked(index, relevance_score).
        //
        // And be careful:
        //   - If the call fails, throw an HttpRequestException that includes the status code and the body Voyage sent back.
        //   - Don't put the key in any message you throw.
        throw new NotImplementedException("Stretch S1: ask the reranking model.");
    }
}

/// <summary>One reranked candidate: which of the documents you sent (by position), and how relevant it was.</summary>
/// <remarks>The relevance score only means something next to the others from the same call.</remarks>
public sealed record Reranked(int Index, double RelevanceScore);
