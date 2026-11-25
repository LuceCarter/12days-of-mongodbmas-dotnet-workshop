using System.Text.Json;
using Microsoft.Extensions.AI;
using MongoDB.Bson;
using MongoDB.Driver;
using Part4B.Starter;
using SantasWorkshop.Shared;
using SantasWorkshop.Shared.Models;

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]
[assembly: AssemblyFixture(typeof(Part4B.Tests.AgentFixture))]

namespace Part4B.Tests;

/// <summary>
/// Loads the toys, the orders and the handbook's chunks into <b>their own database</b>, once, and builds one reference vector index over
/// the chunks, before any test runs.
/// </summary>
/// <remarks>
/// <para>The toys, orders and chunks are only <em>read</em>, so every test shares them. The conversation tests write, so each gets a
/// scratch collection of its own.</para>
/// <para>The reference collection gets a <b>new name every run</b>, and a new index is only used once it has read every document: a dropped
/// collection's search index is torn down in the background, and a new one reports "ready" before it has finished reading.</para>
/// </remarks>
public sealed class AgentFixture : IAsyncLifetime
{
    public const string DatabaseName = "santas_workshop_part4b_tests";
    public const string ReferenceIndex = "ref-handbook";
    public const int Dimensions = 1024;

    public IMongoClient Client { get; private set; } = null!;
    public IMongoDatabase Database { get; private set; } = null!;
    public IMongoCollection<Toy> Toys { get; private set; } = null!;
    public IMongoCollection<Order> Orders { get; private set; } = null!;
    public IMongoCollection<HandbookChunk> Chunks { get; private set; } = null!;

    public ElfTools Tools { get; private set; } = null!;

    public static SeedLoader.HandbookFile Shipped { get; } = SeedLoader.LoadHandbookChunks();

    public async ValueTask InitializeAsync()
    {
        Client = MongoContext.CreateClient();
        Database = MongoContext.GetDatabase(Client, DatabaseName);

        try
        {
            foreach (var leftover in await (await Database.ListCollectionNamesAsync()).ToListAsync())
                if (leftover.StartsWith("scratch_", StringComparison.Ordinal))
                    await Database.DropCollectionAsync(leftover);

            await Database.DropCollectionAsync("toys");
            await Database.DropCollectionAsync("orders");
            Toys = Database.GetCollection<Toy>("toys");
            Orders = Database.GetCollection<Order>("orders");
            await Toys.InsertManyAsync(SeedLoader.LoadToys());
            await Orders.InsertManyAsync(SeedLoader.LoadOrders());

            Chunks = Database.GetCollection<HandbookChunk>($"scratch_reference_{Guid.NewGuid():N}");
            await Chunks.InsertManyAsync(Shipped.Chunks);
            await CreateReferenceIndexAsync(Chunks);
        }
        catch (Exception ex) when (ex is not TimeoutException)
        {
            throw new InvalidOperationException(
                $"""

                 Couldn't reach MongoDB with Atlas Vector Search, so these tests can't run.

                 Start it from the repo root:
                     docker compose up -d

                 Then check everything is healthy (it checks Search too):
                     dotnet run --project tools/doctor

                 (Underlying error: {ex.GetType().Name} — {ex.Message.Split('\n')[0]})

                 """, ex);
        }

        Tools = new ElfTools(Toys, Orders);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>A throwaway collection of stored messages, for a test that writes. It has no search index, so it can't collide with anything.</summary>
    public IMongoCollection<StoredMessage> FreshMessages(string name) =>
        Database.GetCollection<StoredMessage>($"scratch_{name}_{Guid.NewGuid():N}");

    private static async Task CreateReferenceIndexAsync(IMongoCollection<HandbookChunk> collection)
    {
        var definition = new BsonDocument("fields", new BsonArray
        {
            new BsonDocument { { "type", "vector" }, { "path", "embedding" }, { "numDimensions", Dimensions }, { "similarity", "cosine" } },
            new BsonDocument { { "type", "filter" }, { "path", "section" } },
            new BsonDocument { { "type", "filter" }, { "path", "audience" } },
        });

        await collection.SearchIndexes.CreateOneAsync(new CreateSearchIndexModel(ReferenceIndex, SearchIndexType.VectorSearch, definition));

        var giveUpAt = DateTime.UtcNow.AddMinutes(3);
        while (DateTime.UtcNow < giveUpAt)
        {
            var found = await (await collection.SearchIndexes.ListAsync(ReferenceIndex)).ToListAsync();
            if (found.Count > 0 && found[0]["queryable"].AsBoolean) break;
            await Task.Delay(250);
        }

        var expected = (int)await collection.CountDocumentsAsync(FilterDefinition<HandbookChunk>.Empty);
        var probe = Shipped.Chunks[0].Embedding!.Select(x => (double)x).ToList();

        while (DateTime.UtcNow < giveUpAt)
        {
            var stage = new BsonDocument("$vectorSearch", new BsonDocument
            {
                { "index", ReferenceIndex }, { "path", "embedding" }, { "queryVector", new BsonArray(probe) },
                { "numCandidates", expected * 10 }, { "limit", expected },
            });

            if ((await collection.Aggregate<BsonDocument>(new[] { stage }).ToListAsync()).Count == expected) return;
            await Task.Delay(250);
        }

        throw new TimeoutException("The reference vector index never became queryable and complete. Is mongot running? Try the doctor.");
    }
}

/// <summary>Shared helpers.</summary>
public abstract class AgentTestBase(AgentFixture fixture)
{
    protected AgentFixture Fixture { get; } = fixture;
    protected ElfTools Tools => Fixture.Tools;
    protected static IReadOnlyList<Toy> Toys { get; } = SeedLoader.LoadToys();
    protected static IReadOnlyList<Order> Orders { get; } = SeedLoader.LoadOrders();

    protected static string Json(object? value) => JsonSerializer.Serialize(value, AIJsonUtilities.DefaultOptions);

    /// <summary>A message of text, from <paramref name="role"/>.</summary>
    protected static ChatMessage Say(ChatRole role, string text) => new(role, text);

    /// <summary>An assistant message asking for tools.</summary>
    protected static ChatMessage Asks(params (string CallId, string Tool, Dictionary<string, object?>? Args)[] calls) =>
        new(ChatRole.Assistant, calls.Select(c => (AIContent)new FunctionCallContent(c.CallId, c.Tool, c.Args)).ToList());
}

/// <summary>A stand-in for the embedding model.</summary>
internal sealed class FakeEmbedder(Func<string, float[]> embed) : IEmbedder
{
    public List<(string Text, string InputType)> Calls { get; } = [];

    public Task<float[]> EmbedAsync(string text, string inputType, CancellationToken cancellationToken = default)
    {
        Calls.Add((text, inputType));
        return Task.FromResult(embed(text));
    }
}

/// <summary>
/// A stand-in for the model that <b>follows a script</b>: each time it is asked, it calls <c>script</c> with what it was shown and how many
/// times it has been asked, and returns what that says. It remembers every call, so a test can read what the model was shown.
/// </summary>
internal sealed class ScriptedChat(Func<IReadOnlyList<ChatMessage>, int, ChatMessage> script) : IChatClient
{
    public List<(List<ChatMessage> Messages, ChatOptions? Options)> Calls { get; } = [];

    public int CallCount => Calls.Count;

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var seen = messages.ToList();
        Calls.Add((seen, options));
        return Task.FromResult(new ChatResponse(script(seen, Calls.Count)));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This stand-in only answers in one piece.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }

    /// <summary>Every tool name this model was offered on call number <paramref name="call"/> (from 1).</summary>
    public List<string> ToolsOffered(int call) => (Calls[call - 1].Options?.Tools ?? []).Select(t => t.Name).ToList();

    /// <summary>All the text, tool calls and tool results the model was shown on call <paramref name="call"/>, as one string to search.</summary>
    public string Shown(int call) => string.Join("\n", Calls[call - 1].Messages.Select(Render));

    public static string Render(ChatMessage message) =>
        $"{message.Role.Value}: " + string.Join(" ", message.Contents.Select(c => c switch
        {
            TextContent t => t.Text,
            FunctionCallContent f => $"[call {f.Name}({string.Join(", ", (f.Arguments ?? new Dictionary<string, object?>()).Select(a => $"{a.Key}={a.Value}"))})]",
            FunctionResultContent r => $"[result {JsonSerializer.Serialize(r.Result)}]",
            _ => c.GetType().Name,
        }));
}
