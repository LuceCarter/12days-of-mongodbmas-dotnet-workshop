using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Part3B.Starter;

/// <summary>
/// Turns text into a vector by asking Voyage AI's embedding model. Step 7 is writing
/// <see cref="EmbedAsync"/>.
/// </summary>
/// <remarks>
/// Atlas stores and searches vectors, but it doesn't make them. A <b>separate embedding model</b> does that,
/// and the vector for your <i>question</i> must come from the <b>same model</b>, with the same number of
/// dimensions, as the vectors you stored. Otherwise the numbers mean different things and the search
/// is nonsense.
///
/// You don't need a key to do the exercises: the tests use a stand-in for the network.
/// If you have a key (<c>VOYAGE_API_KEY</c>), <see cref="FromEnvironment"/> makes a real one.
/// </remarks>
public sealed class VoyageEmbedder(
    HttpClient http,
    string apiKey,
    string model = VoyageEmbedder.DefaultModel,
    int dimensions = VoyageEmbedder.DefaultDimensions,
    string baseUrl = VoyageEmbedder.DefaultBaseUrl)
{
    /// <summary>The host a key created on voyageai.com talks to. A key created in MongoDB Atlas talks to
    /// <c>https://ai.mongodb.com</c> instead: set <c>VOYAGE_BASE_URL</c> and <see cref="FromEnvironment"/> does the rest.</summary>
    public const string DefaultBaseUrl = "https://api.voyageai.com";

    /// <summary>The embeddings endpoint on <see cref="DefaultBaseUrl"/>.</summary>
    public const string Endpoint = DefaultBaseUrl + "/v1/embeddings";

    /// <summary>The model that made the vectors the toys ship with.</summary>
    public const string DefaultModel = "voyage-4";

    /// <summary>What <see cref="DefaultModel"/> returns unless you ask for something else.</summary>
    public const int DefaultDimensions = 1024;

    private readonly HttpClient _http = http;
    private readonly string _apiKey = apiKey;
    private readonly string _model = model;
    private readonly int _dimensions = dimensions;
    private readonly string _endpoint = baseUrl.TrimEnd('/') + "/v1/embeddings";

    /// <summary>
    /// A real embedder, using the key in <c>VOYAGE_API_KEY</c> and the host in <c>VOYAGE_BASE_URL</c>
    /// (<c>https://api.voyageai.com</c> if that isn't set). Throws if there is no key.
    /// </summary>
    public static VoyageEmbedder FromEnvironment() =>
        Environment.GetEnvironmentVariable("VOYAGE_API_KEY") is { Length: > 0 } key
            ? new VoyageEmbedder(new HttpClient(), key, baseUrl: VoyageHost.FromEnvironment())
            : throw new InvalidOperationException(
                "VOYAGE_API_KEY isn't set. You don't need it for the exercises; if you want to call Voyage " +
                "for real, create a free key at https://www.mongodb.com/docs/voyageai/ and run: export VOYAGE_API_KEY=\"...\"");

    /// <summary>
    /// Step 7 (badge objective 2) — embed <paramref name="text"/>.
    /// <paramref name="inputType"/> is <c>"document"</c> for text you are storing and <c>"query"</c> for
    /// a question you are searching with.
    /// </summary>
    public Task<float[]> EmbedAsync(string text, string inputType)
    {
        // TODO (Step 7): POST to _endpoint with:
        //   header   Authorization: Bearer <the key>
        //   body     { "input": [ text ], "model": _model, "input_type": inputType, "output_dimension": _dimensions }
        //            (JsonObject / JsonArray make this easy; send it as application/json)
        //
        // The reply looks like  { "data": [ { "embedding": [ 0.01, -0.02, ... ], "index": 0 } ], ... }
        // Return data[0].embedding as a float[].
        //
        // Then be careful, because this is where it goes wrong quietly:
        //   - If the call fails, throw an HttpRequestException that includes the status code and the body Voyage sent back.
        //   - If the vector isn't _dimensions long, throw an InvalidOperationException that says what you got and what you wanted.
        //   - Don't put the key in any message you throw.
        throw new NotImplementedException("Step 7: ask the embedding model.");
    }
}

/// <summary>Which Voyage host to talk to. Keys from voyageai.com and keys from MongoDB Atlas use different ones.</summary>
public static class VoyageHost
{
    /// <summary><c>VOYAGE_BASE_URL</c> if it's set, otherwise <see cref="VoyageEmbedder.DefaultBaseUrl"/>.</summary>
    public static string FromEnvironment() =>
        Environment.GetEnvironmentVariable("VOYAGE_BASE_URL") is { Length: > 0 } url ? url : VoyageEmbedder.DefaultBaseUrl;
}
