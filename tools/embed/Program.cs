// Generates the vectors Part 3B ships with: one per toy description, and one per example question.
//
//   export VOYAGE_API_KEY="..."            # never commit this
//   dotnet run --project tools/embed
//
// You only need this to REGENERATE the shipped data (a new model, or a changed catalogue). Learners
// don't: the vectors it writes are committed to the repo, so Part 3B works with no key at all.
//
// What it writes, into src/Shared/SeedData/:
//   toy-embeddings.json     every toy's description, embedded as a DOCUMENT
//   query-embeddings.json   a handful of example questions, embedded as QUERIES
//
// It writes NOTHING unless every vector came back the right length and the right count, so a
// half-finished run can't leave the repo with half the data.
//
// Options:
//   --out <folder>   write somewhere other than src/Shared/SeedData (used to test this tool)
//
// Environment:
//   VOYAGE_API_KEY   required
//   VOYAGE_BASE_URL  optional, defaults to https://api.voyageai.com (used to test this tool offline)

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using SantasWorkshop.Shared;

const string Model = "voyage-4";
const int Dimensions = 1024;

// The example questions Part 3B's README and tests use. A question is only useful here if it shares
// no words with the toy it should find: that is the whole point of searching by meaning.
string[] questions =
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

Console.WriteLine();
Console.WriteLine("🎄 Santa's Workshop — generating the Part 3B vectors");
Console.WriteLine();

var apiKey = Environment.GetEnvironmentVariable("VOYAGE_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("   ❌ VOYAGE_API_KEY isn't set.");
    Console.WriteLine();
    Console.WriteLine("   Create a free key at https://www.mongodb.com/docs/voyageai/ and set it:");
    Console.WriteLine("       export VOYAGE_API_KEY=\"your-key-here\"");
    Console.WriteLine("   Never commit the key.");
    Console.WriteLine();
    return 1;
}

var baseUrl = Environment.GetEnvironmentVariable("VOYAGE_BASE_URL") is { Length: > 0 } custom
    ? custom.TrimEnd('/')
    : "https://api.voyageai.com";

var outFolder = ArgumentValue("--out") ?? Path.Combine(FindRepoRoot(), "src", "Shared", "SeedData");
Directory.CreateDirectory(outFolder);

var toys = SeedLoader.LoadToys();

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

try
{
    Console.WriteLine($"   Model      : {Model} ({Dimensions} dimensions)");
    Console.WriteLine($"   Endpoint   : {baseUrl}/v1/embeddings");
    Console.WriteLine($"   Key        : {Mask(apiKey)}");
    Console.WriteLine();

    var (toyVectors, toyTokens) = await EmbedAsync(toys.Select(t => t.Description).ToList(), "document");
    var (questionVectors, questionTokens) = await EmbedAsync(questions, "query");

    // Normalisation is NOT something the Voyage docs state, so measure it rather than assume it.
    var norms = toyVectors.Concat(questionVectors).Select(Norm).ToList();
    Console.WriteLine($"   ✅ {toys.Count} toy descriptions (as documents)   {toyTokens} tokens");
    Console.WriteLine($"   ✅ {questions.Length} questions (as queries)           {questionTokens} tokens");
    Console.WriteLine($"   ℹ️  vector length (norm): min {norms.Min():F4}, max {norms.Max():F4}");
    Console.WriteLine();

    WriteFile("toy-embeddings.json", "document",
        toys.Select((t, i) => (t.Name, toyVectors[i])));
    WriteFile("query-embeddings.json", "query",
        questions.Select((q, i) => (q, questionVectors[i])));

    Console.WriteLine($"   Wrote toy-embeddings.json and query-embeddings.json to:");
    Console.WriteLine($"       {outFolder}");
    Console.WriteLine();
    Console.WriteLine("   Next: commit those two files, then rebuild.");
    Console.WriteLine();
    return 0;
}
catch (EmbedException ex)
{
    Console.WriteLine($"   ❌ {ex.Message}");
    Console.WriteLine();
    Console.WriteLine("   Nothing was written.");
    Console.WriteLine();
    return 1;
}
catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
{
    Console.WriteLine($"   ❌ Couldn't reach {baseUrl}: {ex.Message}");
    Console.WriteLine();
    Console.WriteLine("   Nothing was written.");
    Console.WriteLine();
    return 1;
}

// ─────────────────────────────────────────────────────────────────────────────

async Task<(List<float[]> Vectors, int Tokens)> EmbedAsync(IReadOnlyList<string> texts, string inputType)
{
    var request = new JsonObject
    {
        ["input"] = new JsonArray(texts.Select(t => (JsonNode)JsonValue.Create(t)!).ToArray()),
        ["model"] = Model,
        ["input_type"] = inputType,
        ["output_dimension"] = Dimensions,
    };

    using var response = await http.PostAsJsonAsync($"{baseUrl}/v1/embeddings", request);
    var body = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        // The body is Voyage's own explanation (bad key, rate limit, ...). It never contains our key.
        var detail = body.Length > 300 ? body[..300] + "…" : body;
        var hint = response.StatusCode == System.Net.HttpStatusCode.Forbidden
            ? "\n\n   Is this the right host for your key? A key created on voyageai.com uses api.voyageai.com (the default);\n" +
              "   a key created in MongoDB Atlas uses https://ai.mongodb.com. Set it with:\n" +
              "       export VOYAGE_BASE_URL=\"https://ai.mongodb.com\""
            : "";
        throw new EmbedException($"Voyage answered {(int)response.StatusCode} {response.ReasonPhrase}: {detail}{hint}");
    }

    var data = JsonNode.Parse(body)?["data"]?.AsArray()
        ?? throw new EmbedException("The response had no 'data' array. Has the API changed?");

    // `index` says which input each vector belongs to. Don't assume the order.
    var vectors = new float[texts.Count][];
    foreach (var item in data)
    {
        var index = item!["index"]!.GetValue<int>();
        vectors[index] = item["embedding"]!.AsArray().Select(v => v!.GetValue<float>()).ToArray();
    }

    for (var i = 0; i < vectors.Length; i++)
    {
        if (vectors[i] is null)
            throw new EmbedException($"No vector came back for input {i} ({inputType}).");
        if (vectors[i].Length != Dimensions)
            throw new EmbedException(
                $"Input {i} ({inputType}) came back with {vectors[i].Length} dimensions, not {Dimensions}. " +
                "Part 3B's index and tests all say " + Dimensions + ".");
    }

    var tokens = JsonNode.Parse(body)?["usage"]?["total_tokens"]?.GetValue<int>() ?? 0;
    return (vectors.ToList(), tokens);
}

void WriteFile(string name, string inputType, IEnumerable<(string Key, float[] Vector)> items)
{
    var document = new JsonObject
    {
        ["model"] = Model,
        ["dimensions"] = Dimensions,
        ["inputType"] = inputType,
        ["embeddings"] = new JsonObject(items.Select(i =>
            KeyValuePair.Create(i.Key, (JsonNode?)new JsonArray(i.Vector.Select(v => (JsonNode)JsonValue.Create(v)!).ToArray())))),
    };

    File.WriteAllText(Path.Combine(outFolder, name),
        document.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) + "\n");
}

static double Norm(float[] v) => Math.Sqrt(v.Sum(x => (double)x * x));

static string Mask(string value) =>
    value.Length <= 8 ? "********" : $"{value[..4]}…{value[^4..]}";

static string? ArgumentValue(string name)
{
    var args = Environment.GetCommandLineArgs();
    var at = Array.IndexOf(args, name);
    return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
}

static string FindRepoRoot()
{
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SantasWorkshop.slnx")))
            directory = directory.Parent;
        if (directory is not null) return directory.FullName;
    }

    throw new InvalidOperationException("Run this from inside the repository (couldn't find SantasWorkshop.slnx).");
}

file sealed class EmbedException(string message) : Exception(message);
