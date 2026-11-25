// Chats with the Elf Assistant, for real: YOUR Part 4B agent, a real language model, and a conversation kept in MongoDB.
//
//   dotnet run --project tools/seed -- --handbook         # once: toys, orders and the handbook's chunks
//   export GEMINI_API_KEY="..."                           # or use Google Cloud: see docs/setup.md. Never commit a key.
//   export VOYAGE_API_KEY="..."                           # embeds the handbook search. Never commit it.
//   export VOYAGE_BASE_URL="https://ai.mongodb.com"       # only if your Voyage key was created in MongoDB Atlas
//   dotnet run --project tools/elf-chat
//
// Try:  "Where is ORD-1001?"   then   "Who is it for?"   (it remembers which order you mean)
//       "How do I tell if a toy part is too small for a baby?"   (it searches the handbook)
//
// Then QUIT (type /quit) and run it again with the same --conversation. It picks the conversation up where you left off,
// because the conversation lives in MongoDB, not in the program.
//
// Options:
//   --conversation <name>   which conversation to join (default: demo)
//
// Keys can come from environment variables or from .NET user secrets, which live outside the repository:
//   dotnet user-secrets set "GEMINI_API_KEY" "..." --project tools/elf-chat
// An environment variable wins if both are set.
//
// It needs Part 4B's Steps 1 to 9 done: it runs your instructions, tools, agent and memory.

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using Part4B.Starter;
using SantasWorkshop.Shared;
using SantasWorkshop.Shared.Models;

var secrets = new ConfigurationBuilder().AddUserSecrets(typeof(Program).Assembly, optional: true).Build();
foreach (var key in new[] { "VOYAGE_API_KEY", "VOYAGE_BASE_URL", "GEMINI_API_KEY", "GEMINI_MODEL", "GEMINI_BASE_URL", "GOOGLE_CLOUD_PROJECT", "GOOGLE_CLOUD_LOCATION", "GOOGLE_CLOUD_ACCESS_TOKEN" })
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)) && secrets[key] is { Length: > 0 } fromSecrets)
        Environment.SetEnvironmentVariable(key, fromSecrets);

var conversation = OptionValue("--conversation") ?? "demo";

Console.WriteLine();
Console.WriteLine("🎄 Santa's Workshop — chatting with the Elf Assistant");
Console.WriteLine();
Console.WriteLine($"   Conversation : {conversation}   (type /quit to leave, /history to count what is remembered)");
Console.WriteLine();

try
{
    var database = MongoContext.GetDatabase(MongoContext.CreateClient());
    var toys = database.GetCollection<Toy>("toys");
    var orders = database.GetCollection<Order>("orders");
    var chunks = database.GetCollection<HandbookChunk>("handbook_chunks");

    if (await toys.CountDocumentsAsync(FilterDefinition<Toy>.Empty) == 0 || await chunks.CountDocumentsAsync(FilterDefinition<HandbookChunk>.Empty) == 0)
    {
        Console.WriteLine("   ❌ There is nothing to look up. Load the toys, orders and handbook first:\n       dotnet run --project tools/seed -- --handbook\n");
        return 1;
    }

    await EnsureHandbookIndexAsync(chunks);

    var store = new MongoChatStore(database.GetCollection<StoredMessage>("elf_conversations"));
    await store.EnsureIndexesAsync(TimeSpan.FromHours(24));

    var embedder = VoyageEmbedder.FromEnvironment();
    var agent = ElfAgent.CreateAgent(
        ElfLlm.FromEnvironment(),
        new ElfTools(toys, orders).CreateTools(),
        new HandbookSearch(chunks, embedder).AsTextSearch(),
        new MongoChatHistoryProvider(store));

    var session = await agent.CreateSessionAsync();
    MongoChatHistoryProvider.UseConversation(session, conversation);

    var remembered = (await store.LoadAsync(conversation)).Count;
    Console.WriteLine(remembered == 0
        ? "   (A new conversation. Ask the Elf Assistant something.)"
        : $"   (Picking up an earlier conversation: {remembered} messages remembered.)");
    Console.WriteLine();

    while (true)
    {
        Console.Write("you > ");
        var line = Console.ReadLine();
        if (line is null || line.Trim() is "/quit" or "/exit") break;
        if (string.IsNullOrWhiteSpace(line)) continue;

        if (line.Trim() == "/history")
        {
            Console.WriteLine($"      {(await store.LoadAsync(conversation)).Count} messages are stored for '{conversation}'.\n");
            continue;
        }

        var response = await agent.RunAsync(line, session);

        // Show the tools it chose to use: the agent deciding for itself is the point.
        foreach (var call in response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>())
            Console.WriteLine($"      🔧 {call.Name}({string.Join(", ", (call.Arguments ?? new Dictionary<string, object?>()).Select(a => $"{a.Key}: {a.Value}"))})");

        Console.WriteLine($"elf > {response.Text}");
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
catch (InvalidOperationException ex) when (ex.Message.Contains("API_KEY") || ex.Message.Contains("GOOGLE_CLOUD") || ex.Message.Contains("language model"))
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

// Part 4A's Step 5 builds this index. Make sure it is there, so this tool works even if you've only done Part 4B.
static async Task EnsureHandbookIndexAsync(IMongoCollection<HandbookChunk> chunks)
{
    if ((await (await chunks.SearchIndexes.ListAsync(HandbookSearch.DefaultIndex)).ToListAsync()).Count > 0) return;

    Console.WriteLine($"   Creating the '{HandbookSearch.DefaultIndex}' vector index (a few seconds)...");

    var definition = new BsonDocument("fields", new BsonArray
    {
        new BsonDocument { { "type", "vector" }, { "path", "embedding" }, { "numDimensions", 1024 }, { "similarity", "cosine" } },
        new BsonDocument { { "type", "filter" }, { "path", "section" } },
        new BsonDocument { { "type", "filter" }, { "path", "audience" } },
    });
    await chunks.SearchIndexes.CreateOneAsync(new CreateSearchIndexModel(HandbookSearch.DefaultIndex, SearchIndexType.VectorSearch, definition));

    while (true)
    {
        var found = await (await chunks.SearchIndexes.ListAsync(HandbookSearch.DefaultIndex)).ToListAsync();
        if (found.Count > 0 && found[0]["queryable"].AsBoolean) break;
        await Task.Delay(500);
    }
}

string? OptionValue(string name)
{
    var at = Array.IndexOf(args, name);
    return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
}
