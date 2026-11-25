using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;
using MongoDB.Bson;
using MongoDB.Driver;
using Part4A.Starter;
using SantasWorkshop.Shared;
using SantasWorkshop.Shared.Models;

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]
[assembly: AssemblyFixture(typeof(Part4A.Tests.HandbookFixture))]

namespace Part4A.Tests;

/// <summary>
/// Loads the handbook's shipped chunks <b>with their vectors</b> once, and builds one <b>reference</b> vector index
/// over them, before any test runs.
/// </summary>
/// <remarks>
/// <para>Retrieval tests only <em>read</em>, so they share one set of chunks and one index. What they can't share is
/// <b>your</b> index (Step 5 has to watch your <c>CreateIndexAsync</c> build one) or <b>your</b> ingestion (Step 4
/// has to watch your code store chunks), so those run against scratch collections. Every other step reads the
/// reference index, so Step 6 doesn't fail just because Step 5 isn't done.</para>
/// <para>The data lives in its <b>own database</b>, <see cref="DatabaseName"/>.</para>
/// </remarks>
public sealed class HandbookFixture : IAsyncLifetime
{
    public const string DatabaseName = "santas_workshop_part4a_tests";
    public const string ReferenceIndex = "ref-handbook";
    public const int Dimensions = 1024;

    public IMongoClient Client { get; private set; } = null!;
    public IMongoDatabase Database { get; private set; } = null!;
    public IMongoCollection<HandbookChunk> Chunks { get; private set; } = null!;

    /// <summary>A <see cref="Retriever"/> pointed at the reference index.</summary>
    public Retriever Retriever { get; private set; } = null!;

    /// <summary>Every chunk with its vector, as shipped.</summary>
    public static SeedLoader.HandbookFile Shipped { get; } = SeedLoader.LoadHandbookChunks();

    /// <summary>The example questions and their vectors, as shipped.</summary>
    public static SeedLoader.EmbeddingFile Questions { get; } = SeedLoader.LoadHandbookQuestions();

    public async ValueTask InitializeAsync()
    {
        Client = MongoContext.CreateClient();
        Database = MongoContext.GetDatabase(Client, DatabaseName);

        try
        {
            // Dropping a collection drops its indexes too, so every run starts clean. That includes the
            // scratch collections earlier runs left behind.
            foreach (var leftover in await (await Database.ListCollectionNamesAsync()).ToListAsync())
                if (leftover.StartsWith("scratch_", StringComparison.Ordinal))
                    await Database.DropCollectionAsync(leftover);

            // A new name every run, because a dropped collection's search index is torn down in the background: building
            // an index with the same name on a same-named collection straight away can collide with the old one, and
            // then it never answers. (The "scratch_" prefix is what makes the cleanup above find it next time.)
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

        Retriever = new Retriever(Chunks, ReferenceIndex);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>A throwaway copy of the shipped chunks, vectors included, for a test that builds an index itself.</summary>
    /// <remarks>
    /// The name gets a unique suffix every time: search indexes are torn down in the background when a collection is
    /// dropped, so re-using a name straight away can collide with the old one.
    /// </remarks>
    public async Task<IMongoCollection<HandbookChunk>> FreshScratchCollectionAsync(string name, bool withChunks = true)
    {
        var scratch = Database.GetCollection<HandbookChunk>($"{name}_{Guid.NewGuid():N}");
        if (withChunks) await scratch.InsertManyAsync(Shipped.Chunks);
        return scratch;
    }

    /// <summary>Written out here rather than borrowed from <see cref="Retriever"/>, so the fixture never depends on your code.</summary>
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

        // "queryable" means the index will answer. It doesn't mean it has finished reading the documents: a query a moment
        // after can come back with fewer than are there, or none. So ask for every chunk and wait until they all arrive.
        var expected = (int)await collection.CountDocumentsAsync(FilterDefinition<HandbookChunk>.Empty);
        var probe = Shipped.Chunks[0].Embedding!.Select(x => (double)x).ToList();

        while (DateTime.UtcNow < giveUpAt)
        {
            var stage = new BsonDocument("$vectorSearch", new BsonDocument
            {
                { "index", ReferenceIndex }, { "path", "embedding" }, { "queryVector", new BsonArray(probe) },
                { "numCandidates", expected * 10 }, { "limit", expected },
            });

            var seen = await collection.Aggregate<BsonDocument>(new[] { stage }).ToListAsync();
            if (seen.Count == expected) return;
            await Task.Delay(250);
        }

        throw new TimeoutException("The reference vector index never became queryable and complete. Is mongot running? Try the doctor.");
    }
}

/// <summary>Shared helpers for working out what the right answer is.</summary>
public abstract class HandbookTestBase(HandbookFixture fixture)
{
    protected HandbookFixture Fixture { get; } = fixture;
    protected Retriever Retriever => Fixture.Retriever;
    protected static IReadOnlyList<HandbookChunk> Shipped => HandbookFixture.Shipped.Chunks;

    protected static float[] VectorOfChunk(string id) => Shipped.Single(c => c.Id == id).Embedding!;

    protected static float[] VectorForQuestion(string question) => HandbookFixture.Questions.Embeddings[question];

    // Independent of the code under test: deliberately its own few lines.
    protected static double Cosine(float[] a, float[] b)
    {
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++) { dot += (double)a[i] * b[i]; na += (double)a[i] * a[i]; nb += (double)b[i] * b[i]; }
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    /// <summary>The honest answer: compare with every chunk, best first, optionally only some of them.</summary>
    protected static List<(string Id, double Cosine)> Nearest(float[] query, int count, Func<HandbookChunk, bool>? where = null) =>
        Shipped.Where(c => where?.Invoke(c) ?? true)
               .Select(c => (c.Id, Cosine(query, c.Embedding!)))
               .OrderByDescending(x => x.Item2).Take(count).ToList();

    /// <summary>The handbook cut into sections by this test project's own few lines, so Step 3 and Step 4 don't depend on Step 1.</summary>
    protected static List<HandbookSection> ReferenceSections()
    {
        var sections = new List<HandbookSection>();
        foreach (var block in SeedLoader.LoadHandbookText().Replace("\r\n", "\n").Split("\n## ").Skip(1))
        {
            var lines = block.Split('\n');
            var audience = lines.First(l => l.StartsWith("Audience:", StringComparison.Ordinal))["Audience:".Length..].Trim();
            var body = string.Join("\n", lines.Skip(1).Where(l => !l.StartsWith("Audience:", StringComparison.Ordinal))).Trim();
            sections.Add(new HandbookSection(lines[0].Trim(), audience, body));
        }

        return sections;
    }

    /// <summary>What a correct chunker makes of <paramref name="text"/>: this test project's own version.</summary>
    protected static List<string> ExpectedChunks(string text, int size, int overlap)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        for (var start = 0; start < words.Length; start += size - overlap)
        {
            chunks.Add(string.Join(' ', words.Skip(start).Take(size)));
            if (start + size >= words.Length) break;
        }

        return chunks;
    }

    /// <summary>A vector made from the text alone: the same text always gets the same vector, different texts differ.</summary>
    protected static float[] VectorFromText(string text)
    {
        var random = new Random(BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(text)), 0));
        var vector = Enumerable.Range(0, HandbookFixture.Dimensions).Select(_ => (float)(random.NextDouble() - 0.5)).ToArray();
        var norm = (float)Math.Sqrt(vector.Sum(x => x * x));
        return vector.Select(x => x / norm).ToArray();
    }
}

/// <summary>A stand-in for the embedding model: answers from a function, and remembers what it was asked.</summary>
internal sealed class FakeEmbedder(Func<string, float[]> embed) : IEmbedder
{
    public List<(string Text, string InputType)> Calls { get; } = [];

    public Task<float[]> EmbedAsync(string text, string inputType, CancellationToken cancellationToken = default)
    {
        Calls.Add((text, inputType));
        return Task.FromResult(embed(text));
    }
}

/// <summary>A stand-in for the generator: says one thing, and remembers what it was shown.</summary>
internal sealed class FakeChat(string reply = "Use the brass test tube.") : IChatClient
{
    public List<List<ChatMessage>> Calls { get; } = [];

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Calls.Add(messages.ToList());
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This stand-in only answers in one piece.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
