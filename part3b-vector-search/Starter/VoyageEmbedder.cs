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
    int dimensions = VoyageEmbedder.DefaultDimensions)
{
    public const string Endpoint = "https://api.voyageai.com/v1/embeddings";

    /// <summary>The model that made the vectors the toys ship with.</summary>
    public const string DefaultModel = "voyage-3.5";

    /// <summary>What <see cref="DefaultModel"/> returns unless you ask for something else.</summary>
    public const int DefaultDimensions = 1024;

    private readonly HttpClient _http = http;
    private readonly string _apiKey = apiKey;
    private readonly string _model = model;
    private readonly int _dimensions = dimensions;

    /// <summary>A real embedder, using the key in <c>VOYAGE_API_KEY</c>. Throws if it isn't set.</summary>
    public static VoyageEmbedder FromEnvironment() =>
        Environment.GetEnvironmentVariable("VOYAGE_API_KEY") is { Length: > 0 } key
            ? new VoyageEmbedder(new HttpClient(), key)
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
        // TODO (Step 7): POST to Endpoint with:
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
