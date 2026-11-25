// Asks the Elf Assistant a question, for real: YOUR Part 4A code, a real embedding model and a real language model.
//
//   dotnet run --project tools/seed -- --handbook          # once: load the handbook's chunks
//   export VOYAGE_API_KEY="..."                            # embeds the question. Never commit it.
//   export VOYAGE_BASE_URL="https://ai.mongodb.com"        # only if your Voyage key was created in MongoDB Atlas
//   export GEMINI_API_KEY="..."                            # writes the answer. Never commit it.
//   dotnet run --project tools/ask -- "How do I tell whether a toy part is a small part?"
//
// Options:
//   --section "Toy Safety"      only look inside one section of the handbook
//   --audience toy-makers       only look at chunks written for one audience
//
// Keys can come from environment variables (above) or from .NET user secrets, which live outside the repository:
//   dotnet user-secrets set "GEMINI_API_KEY" "..." --project tools/ask
// An environment variable wins if both are set.
//
// It needs Steps 4 to 9 of Part 4A done (it uses your Retriever, your prompt and your AskAsync), plus the index from Step 5,
// which it creates for you the first time using YOUR CreateIndexAsync.

using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Part4A.Starter;
using SantasWorkshop.Shared;
using SantasWorkshop.Shared.Models;

// The Starter's classes read plain environment variables, so copy any user secrets into them (never overriding one that is set).
var secrets = new ConfigurationBuilder().AddUserSecrets(typeof(Program).Assembly, optional: true).Build();
foreach (var key in new[] { "VOYAGE_API_KEY", "VOYAGE_BASE_URL", "GEMINI_API_KEY", "GEMINI_MODEL", "GEMINI_BASE_URL" })
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)) && secrets[key] is { Length: > 0 } fromSecrets)
        Environment.SetEnvironmentVariable(key, fromSecrets);

var question = args.FirstOrDefault(a => !a.StartsWith("--") && !IsValueOf(a));
if (string.IsNullOrWhiteSpace(question))
{
    Console.WriteLine("\n   Give it a question:\n       dotnet run --project tools/ask -- \"How do I tell whether a toy part is a small part?\"\n");
    return 1;
}

var section = OptionValue("--section");
var audience = OptionValue("--audience");

Console.WriteLine();
Console.WriteLine("🎄 Santa's Workshop — asking the Elf Assistant");
Console.WriteLine();
Console.WriteLine($"   Question : {question}");
if (section is not null) Console.WriteLine($"   Section  : {section}");
if (audience is not null) Console.WriteLine($"   Audience : {audience}");
Console.WriteLine();

try
{
    var chunks = MongoContext.GetDatabase(MongoContext.CreateClient()).GetCollection<HandbookChunk>("handbook_chunks");

    if (await chunks.CountDocumentsAsync(FilterDefinition<HandbookChunk>.Empty) == 0)
    {
        Console.WriteLine("   ❌ There are no chunks to search. Load them first:\n       dotnet run --project tools/seed -- --handbook\n");
        return 1;
    }

    var retriever = new Retriever(chunks);
    var hasIndex = (await (await chunks.SearchIndexes.ListAsync(Retriever.DefaultIndex)).ToListAsync()).Count > 0;
    if (!hasIndex)
    {
        Console.WriteLine($"   Creating the '{Retriever.DefaultIndex}' index with your Step 5 code (this takes a few seconds)...");
        await retriever.CreateIndexAsync(Retriever.DefaultIndex);
    }

    var embedder = VoyageEmbedder.FromEnvironment();
    var generator = ElfLlm.FromEnvironment();

    // Show what the retriever finds and how well each chunk scores, because that is what the relevance bar decides on.
    var vector = await embedder.EmbedAsync(question, "query");
    var found = await retriever.RetrieveAsync(vector, 4, section, audience);

    Console.WriteLine($"   Retrieved (the bar is {ElfAssistant.DefaultMinScore:F2}):");
    foreach (var chunk in found)
        Console.WriteLine($"     {chunk.Score:F3} {(chunk.Score >= ElfAssistant.DefaultMinScore ? "✔" : "✘")}  {chunk.Id}");
    Console.WriteLine();

    var answer = await new ElfAssistant(retriever, embedder, generator).AskAsync(question, section, audience);

    Console.WriteLine($"   {(answer.Grounded ? "Answer (grounded in the handbook)" : "Answer (nothing relevant found; the model was not asked)")}:");
    Console.WriteLine();
    foreach (var line in answer.Text.Split('\n')) Console.WriteLine($"     {line}");
    Console.WriteLine();

    if (answer.Sources.Count > 0)
    {
        Console.WriteLine("   Sources:");
        foreach (var source in answer.Sources) Console.WriteLine($"     {source.Source}  ({source.ChunkId})");
        Console.WriteLine();
    }

    return 0;
}
catch (NotImplementedException ex)
{
    Console.WriteLine($"   ❌ {ex.Message}");
    Console.WriteLine("      This tool runs YOUR code, so finish that step first, then try again.");
    Console.WriteLine();
    return 1;
}
catch (InvalidOperationException ex) when (ex.Message.Contains("API_KEY"))
{
    Console.WriteLine($"   ❌ {ex.Message}");
    Console.WriteLine();
    return 1;
}
catch (HttpRequestException ex)
{
    // The message is the service's own explanation (bad key, wrong host, rate limit). It never contains our key.
    Console.WriteLine($"   ❌ {ex.Message}");
    if (ex.Message.Contains("403"))
        Console.WriteLine("      A Voyage key made in MongoDB Atlas needs:  export VOYAGE_BASE_URL=\"https://ai.mongodb.com\"");
    Console.WriteLine();
    return 1;
}

string? OptionValue(string name)
{
    var at = Array.IndexOf(args, name);
    return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
}

bool IsValueOf(string arg) => args.Select((a, i) => (a, i)).Any(x => x.a.StartsWith("--") && x.i + 1 < args.Length && args[x.i + 1] == arg);
