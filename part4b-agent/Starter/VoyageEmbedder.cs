using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;

namespace Part4B.Starter;

/// <summary>
/// Embeds text with Voyage AI. <b>Already written for you</b>: you wrote this one in Part 3B (Step 7).
/// </summary>
/// <remarks>
/// You don't need a key to do the exercises, because the tests use a stand-in. To call Voyage for real, set
/// <c>VOYAGE_API_KEY</c> (and <c>VOYAGE_BASE_URL=https://ai.mongodb.com</c> if your key was made in MongoDB Atlas).
/// </remarks>
public sealed class VoyageEmbedder(
    HttpClient http,
    string apiKey,
    string model = VoyageEmbedder.DefaultModel,
    int dimensions = VoyageEmbedder.DefaultDimensions,
    string baseUrl = VoyageEmbedder.DefaultBaseUrl) : IEmbedder
{
    public const string DefaultBaseUrl = "https://api.voyageai.com";
    public const string DefaultModel = "voyage-4";
    public const int DefaultDimensions = 1024;

    private readonly string _endpoint = baseUrl.TrimEnd('/') + "/v1/embeddings";

    public static VoyageEmbedder FromEnvironment() =>
        Environment.GetEnvironmentVariable("VOYAGE_API_KEY") is { Length: > 0 } key
            ? new VoyageEmbedder(new HttpClient(), key,
                baseUrl: Environment.GetEnvironmentVariable("VOYAGE_BASE_URL") is { Length: > 0 } url ? url : DefaultBaseUrl)
            : throw new InvalidOperationException(
                "VOYAGE_API_KEY isn't set. You don't need it for the exercises; to call Voyage for real, set it " +
                "(and VOYAGE_BASE_URL=https://ai.mongodb.com if your key was made in MongoDB Atlas).");

    public async Task<float[]> EmbedAsync(string text, string inputType, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(new JsonObject
            {
                ["input"] = new JsonArray(text),
                ["model"] = model,
                ["input_type"] = inputType,
                ["output_dimension"] = dimensions,
            }.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Voyage answered {(int)response.StatusCode} {response.ReasonPhrase}: {body}");

        var vector = JsonNode.Parse(body)!["data"]![0]!["embedding"]!.AsArray().Select(v => v!.GetValue<float>()).ToArray();

        if (vector.Length != dimensions)
            throw new InvalidOperationException($"Voyage returned a vector of {vector.Length} numbers, but this embedder was asked for {dimensions}.");

        return vector;
    }
}
