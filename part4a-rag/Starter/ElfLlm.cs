using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Part4A.Starter;

/// <summary>
/// The <b>generator</b>: the language model that writes the answer. <b>Already written for you.</b>
/// </summary>
/// <remarks>
/// <para>It is a different model, from a different company, to the one that makes the vectors (Voyage AI). That is on
/// purpose, and it is a thing the RAG badge assesses: <b>the model that finds the text and the model that writes
/// the answer are separate</b>. Change one without touching the other.</para>
/// <para>Everything in this part is written against <see cref="IChatClient"/>, so swapping providers is a change here
/// and nowhere else. Two routes to Gemini are supported, both through an OpenAI-compatible endpoint:</para>
/// <list type="bullet">
///   <item><b>Google AI Studio</b> (the default): a free API key in <c>GEMINI_API_KEY</c>.</item>
///   <item><b>Google Cloud (Vertex AI)</b>: for when you can't create an AI Studio key, such as when your organisation blocks them.
///         Set <c>GOOGLE_CLOUD_PROJECT</c> (and optionally <c>GOOGLE_CLOUD_LOCATION</c>), and sign in with the <c>gcloud</c> CLI.
///         The project needs billing enabled.</item>
/// </list>
/// <para>You don't need either to do the exercises: the tests use a stand-in. Never commit a key or a token.</para>
/// </remarks>
public static class ElfLlm
{
    /// <summary>Google AI Studio's OpenAI-compatible endpoint for Gemini.</summary>
    public const string DefaultBaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/";

    /// <summary>The model to ask. Override it with <c>GEMINI_MODEL</c>: Google changes which models are on the free tier.</summary>
    public const string DefaultModel = "gemini-3.8-flash";

    /// <summary>Where Vertex AI is asked for by default, if <c>GOOGLE_CLOUD_LOCATION</c> isn't set. <c>gemini-3.8-flash</c> answered from <c>global</c> and was "not found" in <c>us-central1</c>.</summary>
    public const string DefaultCloudLocation = "global";

    /// <summary>
    /// A real chat client. With <c>GEMINI_API_KEY</c> set it uses Google AI Studio; otherwise, with
    /// <c>GOOGLE_CLOUD_PROJECT</c> set, it uses Vertex AI on Google Cloud. <c>GEMINI_MODEL</c> and <c>GEMINI_BASE_URL</c> override either.
    /// </summary>
    public static IChatClient FromEnvironment()
    {
        var (baseUrl, credential, model) = ResolveFromEnvironment();

        return new OpenAIClient(new ApiKeyCredential(credential), new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
            .GetChatClient(model)
            .AsIChatClient();
    }

    /// <summary>Which endpoint, which key or token, and which model, as the environment says. Split out so it can be tested.</summary>
    public static (string BaseUrl, string Credential, string Model) ResolveFromEnvironment(Func<string, string?>? env = null, Func<string>? accessToken = null)
    {
        env ??= Environment.GetEnvironmentVariable;
        string? Get(string name) => env(name) is { Length: > 0 } value ? value : null;

        var modelOverride = Get("GEMINI_MODEL");
        var baseOverride = Get("GEMINI_BASE_URL");

        if (Get("GEMINI_API_KEY") is { } key)
            return (baseOverride ?? DefaultBaseUrl, key, modelOverride ?? DefaultModel);

        if (Get("GOOGLE_CLOUD_PROJECT") is { } project)
        {
            var location = Get("GOOGLE_CLOUD_LOCATION") ?? DefaultCloudLocation;
            // The "global" location has no region in its hostname: https://aiplatform.googleapis.com, not https://global-aiplatform...
            var host = location == "global" ? "aiplatform.googleapis.com" : $"{location}-aiplatform.googleapis.com";
            var url = baseOverride ?? $"https://{host}/v1/projects/{project}/locations/{location}/endpoints/openapi/";

            // Google Cloud authenticates with a short-lived access token, not a key. Use one you've supplied, or ask gcloud for one.
            var token = Get("GOOGLE_CLOUD_ACCESS_TOKEN") ?? (accessToken ?? GcloudAccessToken)();

            // Vertex names its models with a "google/" prefix.
            return (url, token, modelOverride ?? $"google/{DefaultModel}");
        }

        throw new InvalidOperationException(
            "No language model is configured. You don't need one for the exercises. To ask a real one, either:\n" +
            "  - get a free key at https://aistudio.google.com/apikey and run: export GEMINI_API_KEY=\"...\"\n" +
            "  - or, if you can't create one, use Google Cloud: run `gcloud auth login`, then: export GOOGLE_CLOUD_PROJECT=\"your-project-id\"");
    }

    private static string GcloudAccessToken()
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("gcloud", "auth print-access-token")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            }) ?? throw new InvalidOperationException("gcloud didn't start.");

            var token = process.StandardOutput.ReadToEnd().Trim();
            var error = process.StandardError.ReadToEnd().Trim();
            process.WaitForExit(30_000);

            if (process.ExitCode != 0 || token.Length == 0)
                throw new InvalidOperationException($"`gcloud auth print-access-token` failed: {(error.Length > 0 ? error : "no output")}");

            return token;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException(
                "GOOGLE_CLOUD_PROJECT is set but the gcloud CLI isn't installed or isn't on your PATH. Install it (https://cloud.google.com/sdk/docs/install), " +
                "run `gcloud auth login`, or set GOOGLE_CLOUD_ACCESS_TOKEN yourself.");
        }
    }
}
