// Checks the Voyage claims Part 3B's stretch badge makes, against the real API, so the README states what was seen
// and not what was assumed. A maintainer's tool: learners never need it.
//
//   export VOYAGE_API_KEY="..."                        # never commit this
//   export VOYAGE_BASE_URL="https://ai.mongodb.com"    # only if the key was created in MongoDB Atlas
//   dotnet run --project tools/voyage-check
//
// It makes a handful of small calls and writes nothing to the repository. Each check says what it saw. A failed
// check doesn't stop the others, and the tool exits 0 only if every check that matters passed.
//
//   1. an embedding comes back at the length we ask for, and is length 1
//   2. asking for 256 numbers gives (nearly) the same vector as cutting 1024 down to 256 and rescaling: Matryoshka
//   3. a rerank call returns { data: [ { index, relevance_score } ] }, and what it does to a real shortlist
//   4. (informational) the contextualized-embeddings endpoint answers in the shape the README describes

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using SantasWorkshop.Shared;

const string EmbedModel = "voyage-4";
const string RerankModel = "rerank-3";
const string ContextModel = "voyage-context-4";

var apiKey = Environment.GetEnvironmentVariable("VOYAGE_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("\n   ❌ VOYAGE_API_KEY isn't set. Never commit it.\n");
    return 1;
}

var baseUrl = (Environment.GetEnvironmentVariable("VOYAGE_BASE_URL") is { Length: > 0 } custom ? custom : "https://api.voyageai.com").TrimEnd('/');

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

Console.WriteLine();
Console.WriteLine("🎄 Santa's Workshop — checking Voyage against the real API");
Console.WriteLine();
Console.WriteLine($"   Host   : {baseUrl}");
Console.WriteLine($"   Key    : {Mask(apiKey)}");
Console.WriteLine();

var failures = 0;

// ── 1 and 2: embeddings, and Matryoshka ──────────────────────────────────────────────────────────────────────────
const string Text = "A frosted triceratops that glows a gentle amber and fades out over twenty minutes once the room goes quiet.";

float[]? full = null;
await Check("1. embedding length and magnitude", async () =>
{
    full = (await EmbedAsync(Text, 1024))!;
    var norm = Norm(full);
    return (full.Length == 1024 && Math.Abs(norm - 1) < 0.001,
        $"{full.Length} numbers, magnitude {norm:F6}");
});

await Check("2. 256 numbers from Voyage vs 1024 cut to 256 and rescaled", async () =>
{
    var asked = (await EmbedAsync(Text, 256))!;
    var cut = Truncate(full ?? (await EmbedAsync(Text, 1024))!, 256);
    var cosine = Dot(asked, cut) / (Norm(asked) * Norm(cut));
    return (asked.Length == 256 && cosine > 0.99,
        $"asked for 256 → {asked.Length} numbers; cosine between that and the truncated 1024 vector: {cosine:F4} (1.0000 means identical)");
});

// ── 3: rerank ────────────────────────────────────────────────────────────────────────────────────────────────────
await Check("3. rerank: response shape, and what it does to a real shortlist", async () =>
{
    var toys = SeedLoader.LoadToys();
    var vectors = SeedLoader.LoadToyEmbeddings().Embeddings;
    var questions = SeedLoader.LoadQueryEmbeddings().Embeddings;
    var question = "something calming for a restless child at bedtime";

    // The shortlist stage one would give: the ten toys nearest the question, by cosine on the shipped vectors.
    var shortlist = toys
        .Select(t => (t.Name, t.Description, Score: Dot(questions[question], vectors[t.Name])))
        .OrderByDescending(x => x.Score).Take(10).ToList();

    var reply = await PostAsync("/v1/rerank", new JsonObject
    {
        ["query"] = question,
        ["documents"] = new JsonArray(shortlist.Select(x => (JsonNode)JsonValue.Create(x.Description)!).ToArray()),
        ["model"] = RerankModel,
        ["top_k"] = 5,
    });

    var rows = reply["data"]!.AsArray().Select(r => (Index: r!["index"]!.GetValue<int>(), Score: r["relevance_score"]!.GetValue<double>())).ToList();

    Console.WriteLine("        vector search order (cosine)          →   reranked (relevance)");
    for (var i = 0; i < 5; i++)
        Console.WriteLine($"        {i + 1}. {shortlist[i].Name,-32} {shortlist[i].Score,6:F3}   →   {shortlist[rows[i].Index].Name,-32} {rows[i].Score,6:F3}");

    var sorted = rows.Select(r => r.Score).SequenceEqual(rows.Select(r => r.Score).OrderByDescending(s => s));
    return (rows.Count == 5 && sorted, $"{rows.Count} rows, most relevant first: {sorted}; model {reply["model"]}; tokens {reply["usage"]?["total_tokens"]}");
});

// ── 4: contextualized embeddings (informational) ─────────────────────────────────────────────────────────────────
await Check("4. contextualized embeddings: endpoint and response shape (informational)", async () =>
{
    var reply = await PostAsync("/v1/contextualizedembeddings", new JsonObject
    {
        ["inputs"] = new JsonArray(new JsonArray(
            "The Elf Handbook, chapter 3: toy safety.",
            "Rule one: every toy is tested by an elf who is not the one who made it.")),
        ["input_type"] = "document",
        ["model"] = ContextModel,
    });

    var inner = reply["data"]![0]!["data"]!.AsArray();
    return (true, $"model {reply["model"]}; one document, {inner.Count} chunks, vector length {inner[0]!["embedding"]!.AsArray().Count}; top-level keys: {string.Join(", ", reply.AsObject().Select(kv => kv.Key))}");
}, informational: true);

Console.WriteLine();
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
        if (ex.Message.Contains("403")) Console.WriteLine("        (A key made in MongoDB Atlas needs VOYAGE_BASE_URL=https://ai.mongodb.com)");
        if (!informational) failures++;
    }
    Console.WriteLine();
}

async Task<float[]?> EmbedAsync(string text, int dimensions)
{
    var reply = await PostAsync("/v1/embeddings", new JsonObject
    {
        ["input"] = new JsonArray(text),
        ["model"] = EmbedModel,
        ["input_type"] = "document",
        ["output_dimension"] = dimensions,
    });
    return reply["data"]![0]!["embedding"]!.AsArray().Select(v => v!.GetValue<float>()).ToArray();
}

async Task<JsonNode> PostAsync(string path, JsonObject body)
{
    using var response = await http.PostAsJsonAsync(baseUrl + path, body);
    var text = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
        throw new InvalidOperationException($"{path} answered {(int)response.StatusCode} {response.ReasonPhrase}: {(text.Length > 300 ? text[..300] + "…" : text)}");

    return JsonNode.Parse(text)!;
}

static double Norm(float[] v) => Math.Sqrt(v.Sum(x => (double)x * x));
static double Dot(float[] a, float[] b) { double d = 0; for (var i = 0; i < a.Length; i++) d += (double)a[i] * b[i]; return d; }

static float[] Truncate(float[] v, int n)
{
    var head = v.Take(n).ToArray();
    var norm = Norm(head);
    return head.Select(x => (float)(x / norm)).ToArray();
}

static string Mask(string value) => value.Length <= 8 ? "********" : $"{value[..4]}…{value[^4..]}";
