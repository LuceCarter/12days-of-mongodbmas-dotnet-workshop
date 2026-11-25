// Checks the Gemini claims Part 4 makes, against the real API, so the README says what was seen and not what was
// assumed. A maintainer's tool: learners never need it.
//
//   export GEMINI_API_KEY="..."                       # Google AI Studio. Never commit it.
//   export GEMINI_MODEL="gemini-3.8-flash"            # optional: the model to check (defaults to what Part 4A uses)
//   dotnet run --project tools/llm-check
//
// If you can't create an AI Studio key (an organisation can block them), use Google Cloud (Vertex AI) instead:
//   gcloud auth login
//   export GOOGLE_CLOUD_PROJECT="your-project-id"     # billing must be enabled on it
//   export GOOGLE_CLOUD_LOCATION="global"             # optional ("global" is the default; a model can be "not found" in one region and fine in another)
//   export GEMINI_MODEL="google/gemini-..."           # optional: Vertex model names start with "google/"
//   dotnet run --project tools/llm-check
//
// An access token comes from `gcloud auth print-access-token`, or from GOOGLE_CLOUD_ACCESS_TOKEN if you set it.
//
// It makes a handful of small calls and writes nothing to the repository. Each check says what it saw. A failed
// check doesn't stop the others, and the tool exits 0 only if every check that matters passed.
//
//   1. the OpenAI-compatible endpoint answers a chat request with the model you named
//   2. given handbook extracts and told to use only those, it answers from them
//   3. given extracts that don't contain the answer, what it says (shown, for you to judge: the rules ask it to decline)
//   4. (informational) it will call a tool when asked, and what the reply looks like: Part 4B needs this

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using SantasWorkshop.Shared;

const string DefaultModel = "gemini-3.8-flash";
const string DefaultBaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/";

string? Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;

string apiKey, model, baseUrl, route;
string? vertexModelsUrl = null;
if (Env("GEMINI_API_KEY") is { } studioKey)
{
    route = "Google AI Studio";
    apiKey = studioKey;
    model = Env("GEMINI_MODEL") ?? DefaultModel;
    baseUrl = (Env("GEMINI_BASE_URL") ?? DefaultBaseUrl).TrimEnd('/');
}
else if (Env("GOOGLE_CLOUD_PROJECT") is { } project)
{
    // The same thing Part 4A's ElfLlm does, written out again so this tool needs nothing from the part.
    var location = Env("GOOGLE_CLOUD_LOCATION") ?? "global";
    route = $"Google Cloud (Vertex AI), project {project}, {location}";
    model = Env("GEMINI_MODEL") ?? $"google/{DefaultModel}";
    var host = location == "global" ? "aiplatform.googleapis.com" : $"{location}-aiplatform.googleapis.com";
    baseUrl = (Env("GEMINI_BASE_URL") ?? $"https://{host}/v1/projects/{project}/locations/{location}/endpoints/openapi").TrimEnd('/');
    vertexModelsUrl = $"https://{host}/v1beta1/publishers/google/models?pageSize=200";

    try { apiKey = Env("GOOGLE_CLOUD_ACCESS_TOKEN") ?? GcloudToken(); }
    catch (Exception ex)
    {
        Console.WriteLine($"\n   ❌ Couldn't get a Google Cloud access token: {ex.Message}\n      Run `gcloud auth login`, or set GOOGLE_CLOUD_ACCESS_TOKEN.\n");
        return 1;
    }
}
else
{
    Console.WriteLine("\n   ❌ Neither GEMINI_API_KEY nor GOOGLE_CLOUD_PROJECT is set.\n" +
                      "      Get a free key at https://aistudio.google.com/apikey, or use Google Cloud: gcloud auth login, then\n" +
                      "      export GOOGLE_CLOUD_PROJECT=\"your-project-id\". Never commit a key or a token.\n");
    return 1;
}

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

Console.WriteLine();
Console.WriteLine("🎄 Santa's Workshop — checking Gemini against the real API");
Console.WriteLine();
Console.WriteLine($"   Route    : {route}");
Console.WriteLine($"   Endpoint : {baseUrl}/chat/completions");
Console.WriteLine($"   Model    : {model}");
Console.WriteLine($"   Key      : {Mask(apiKey)}");
Console.WriteLine();

var failures = 0;

// The same wording Part 4A's Step 7 asks the learner to write, so this checks the prompt shape the part teaches.
const string Rules = """
    You are the Elf Assistant at Santa's Workshop.
    Answer using ONLY the handbook extracts you are given.
    If the extracts do not contain the answer, say that you don't know instead of guessing.
    Say which source each part of your answer comes from.
    """;

var handbook = SeedLoader.LoadHandbookChunks().Chunks;
var testTubeChunk = handbook.First(c => c.Text.Contains("brass test tube", StringComparison.Ordinal));
var reindeerChunk = handbook.First(c => c.Section == "Reindeer Care");

await Check("1. the endpoint answers, with the model you named", async () =>
{
    JsonNode reply;
    try { reply = await ChatAsync([("user", "Reply with the single word: ready")]); }
    catch (InvalidOperationException ex) when (vertexModelsUrl is not null && ex.Message.Contains("404"))
    {
        // The model name wasn't found for this project and region. Ask Vertex which Gemini models it CAN see.
        Console.WriteLine($"        The model '{model}' wasn't found. Asking Vertex which Gemini models this project can see ...");
        try
        {
            // Listing publisher models bills the call to a project, which Google asks for explicitly when you sign in with gcloud.
            using var listRequest = new HttpRequestMessage(HttpMethod.Get, vertexModelsUrl);
            listRequest.Headers.Add("x-goog-user-project", Env("GOOGLE_CLOUD_PROJECT")!);
            using var listing = await http.SendAsync(listRequest);
            var body = await listing.Content.ReadAsStringAsync();
            var names = System.Text.RegularExpressions.Regex.Matches(body, "publishers/google/models/(gemini[^\"/@]*)")
                .Select(m => m.Groups[1].Value).Distinct().Order().ToList();
            Console.WriteLine(listing.IsSuccessStatusCode
                ? (names.Count > 0
                    ? $"        Gemini models listed (use one as GEMINI_MODEL=google/<name>):\n          {string.Join("\n          ", names)}"
                    : "        The list came back with no gemini models in it.")
                : $"        Couldn't list them: {(int)listing.StatusCode} {Truncate(body, 250)}");
        }
        catch (Exception listError) { Console.WriteLine($"        Couldn't list them: {listError.Message}"); }

        throw;
    }
    var text = reply["choices"]![0]!["message"]!["content"]!.GetValue<string>().Trim();
    return (text.Length > 0, $"model reported: {reply["model"]}; it said: \"{Truncate(text, 80)}\"; tokens {reply["usage"]?["total_tokens"]}");
});

await Check("2. told to use only the extracts, it answers from them", async () =>
{
    var text = await AskAsync("How do I tell whether a toy part is a small part?", testTubeChunk);
    Console.WriteLine($"        {Truncate(text.ReplaceLineEndings(" "), 300)}");
    return (text.Contains("test tube", StringComparison.OrdinalIgnoreCase), "mentions the brass test tube from the extract");
});

await Check("3. extracts that don't contain the answer: does it decline? (read it and judge)", async () =>
{
    var text = await AskAsync("What is the capital of France?", reindeerChunk);
    Console.WriteLine($"        {Truncate(text.ReplaceLineEndings(" "), 300)}");
    var declined = text.Contains("don't know", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("do not know", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("not contain", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("doesn't contain", StringComparison.OrdinalIgnoreCase);
    return (declined, declined ? "it declined" : "it did NOT clearly decline: the prompt rules may need strengthening");
}, informational: true);

await Check("4. a tool call: does it ask for the tool, and what does the reply look like? (Part 4B)", async () =>
{
    var body = new JsonObject
    {
        ["model"] = model,
        ["messages"] = new JsonArray(Msg("user", "Look up the handbook rules on reindeer feeding. Use the tool.")),
        ["tools"] = new JsonArray(new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = "search_handbook",
                ["description"] = "Searches the Elf Handbook.",
                ["parameters"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject { ["question"] = new JsonObject { ["type"] = "string" } },
                    ["required"] = new JsonArray("question"),
                },
            },
        }),
        ["tool_choice"] = "auto",
    };
    var reply = await PostAsync("/chat/completions", body);
    var message = reply["choices"]![0]!["message"]!;
    var calls = message["tool_calls"]?.AsArray();
    return (calls is { Count: > 0 },
        calls is { Count: > 0 }
            ? $"tool_calls[0]: {calls[0]!["function"]!["name"]}({calls[0]!["function"]!["arguments"]}), finish_reason {reply["choices"]![0]!["finish_reason"]}"
            : $"no tool call. It said: \"{Truncate(message["content"]?.ToString() ?? "", 100)}\"");
}, informational: true);

Console.WriteLine(failures == 0 ? "   All the checks that matter passed." : $"   {failures} check(s) that matter failed.");
Console.WriteLine();
return failures == 0 ? 0 : 1;

// ─────────────────────────────────────────────────────────────────────────────

async Task Check(string name, Func<Task<(bool Ok, string Detail)>> run, bool informational = false)
{
    try
    {
        var (ok, detail) = await run();
        Console.WriteLine($"   {(ok ? "✅" : informational ? "⚠️ " : "❌")} {name}");
        Console.WriteLine($"        {detail}");
        if (!ok && !informational) failures++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"   {(informational ? "⚠️ " : "❌")} {name}");
        Console.WriteLine($"        {ex.Message}");
        if (!informational) failures++;
    }

    Console.WriteLine();
}

async Task<string> AskAsync(string question, SantasWorkshop.Shared.Models.HandbookChunk chunk)
{
    var user = $"Handbook extracts:\n\n[1] Source: {chunk.Source}\n{chunk.Text}\n\nQuestion: {question}";
    var reply = await ChatAsync([("system", Rules), ("user", user)]);
    return reply["choices"]![0]!["message"]!["content"]!.GetValue<string>();
}

async Task<JsonNode> ChatAsync((string Role, string Content)[] messages) =>
    await PostAsync("/chat/completions", new JsonObject
    {
        ["model"] = model,
        ["messages"] = new JsonArray(messages.Select(x => Msg(x.Role, x.Content)).ToArray()),
    });

static JsonNode Msg(string role, string content) => new JsonObject { ["role"] = role, ["content"] = content };

async Task<JsonNode> PostAsync(string path, JsonObject body)
{
    using var response = await http.PostAsJsonAsync(baseUrl + path, body);
    var text = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        // Show what limits Google reports, when it reports any: this is how real free-tier limits are learned.
        var headers = string.Join("; ", response.Headers.Where(h => h.Key.Contains("limit", StringComparison.OrdinalIgnoreCase) || h.Key.Equals("Retry-After", StringComparison.OrdinalIgnoreCase))
            .Select(h => $"{h.Key}: {string.Join(",", h.Value)}"));
        throw new InvalidOperationException(
            $"{path} answered {(int)response.StatusCode} {response.ReasonPhrase}: {Truncate(text, 400)}" + (headers.Length > 0 ? $" [{headers}]" : ""));
    }

    return JsonNode.Parse(text)!;
}

static string GcloudToken()
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

    return process.ExitCode == 0 && token.Length > 0
        ? token
        : throw new InvalidOperationException(error.Length > 0 ? error : "gcloud printed no token.");
}

static string Truncate(string value, int length) => value.Length <= length ? value : value[..length] + "…";

static string Mask(string value) => value.Length <= 8 ? "********" : $"{value[..4]}…{value[^4..]}";
