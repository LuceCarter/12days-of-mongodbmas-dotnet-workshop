using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Driver;
using Part3B.Starter;
using SantasWorkshop.Shared;
using SantasWorkshop.Shared.Models;

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]
[assembly: AssemblyFixture(typeof(Part3B.Tests.VectorFixture))]

namespace Part3B.Tests;

/// <summary>
/// Loads the toys <b>with their vectors</b> once, and builds one <b>reference</b> vector index over them,
/// before any test runs.
/// </summary>
/// <remarks>
/// <para>Vector tests only <em>read</em>, so they share one catalogue. What they can't share is
/// <b>your</b> index: Step 4's test has to watch <i>your</i> <c>CreateVectorIndexAsync</c> build one, so
/// that runs your code against a scratch collection. Every other step queries the reference index, so
/// Step 5 doesn't fail just because Step 4 isn't done.</para>
/// <para>The data lives in its <b>own database</b>, <see cref="DatabaseName"/>.</para>
/// </remarks>
public sealed class VectorFixture : IAsyncLifetime
{
    public const string DatabaseName = "santas_workshop_part3b_tests";
    public const string ReferenceIndex = "ref-vectors";
    public const int Dimensions = 1024;

    public IMongoClient Client { get; private set; } = null!;
    public IMongoDatabase Database { get; private set; } = null!;
    public IMongoCollection<Toy> Toys { get; private set; } = null!;

    /// <summary>A <see cref="GiftFinder"/> pointed at the reference index, with no embedder.</summary>
    public GiftFinder Finder { get; private set; } = null!;

    /// <summary>Every toy with its vector, as shipped.</summary>
    public static IReadOnlyList<Toy> Catalogue { get; } = SeedLoader.LoadEmbeddedToys();

    /// <summary>The example questions and their vectors, as shipped.</summary>
    public static SeedLoader.EmbeddingFile Questions { get; } = SeedLoader.LoadQueryEmbeddings();

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

            await Database.DropCollectionAsync("toys");
            Toys = Database.GetCollection<Toy>("toys");
            await Toys.InsertManyAsync(Catalogue);

            await CreateReferenceIndexAsync(Toys);
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

        Finder = new GiftFinder(Toys, embedder: null, ReferenceIndex);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>A throwaway copy of the catalogue, vectors included, for a test that builds an index itself.</summary>
    /// <remarks>
    /// The collection name gets a unique suffix every time. Search indexes are torn down in the background
    /// when a collection is dropped, so re-using a name straight away can collide with the old index (which,
    /// after a red run, was built with the wrong number of dimensions). A fresh name can't collide with anything.
    /// </remarks>
    public async Task<IMongoCollection<Toy>> FreshScratchCollectionAsync(string name)
    {
        var scratch = Database.GetCollection<Toy>($"{name}_{Guid.NewGuid():N}");
        await scratch.InsertManyAsync(Catalogue);
        return scratch;
    }

    /// <summary>Written out here rather than borrowed from <see cref="GiftFinder"/>, so the fixture never
    /// depends on your code.</summary>
    private static async Task CreateReferenceIndexAsync(IMongoCollection<Toy> collection)
    {
        var definition = new BsonDocument("fields", new BsonArray
        {
            new BsonDocument { { "type", "vector" }, { "path", "descriptionEmbedding" }, { "numDimensions", Dimensions }, { "similarity", "cosine" } },
            new BsonDocument { { "type", "filter" }, { "path", "minAge" } },
            new BsonDocument { { "type", "filter" }, { "path", "maxAge" } },
            new BsonDocument { { "type", "filter" }, { "path", "inStock" } },
            new BsonDocument { { "type", "filter" }, { "path", "category" } },
        });

        await collection.SearchIndexes.CreateOneAsync(new CreateSearchIndexModel(ReferenceIndex, SearchIndexType.VectorSearch, definition));

        var giveUpAt = DateTime.UtcNow.AddMinutes(3);
        while (DateTime.UtcNow < giveUpAt)
        {
            var found = await (await collection.SearchIndexes.ListAsync(ReferenceIndex)).ToListAsync();
            if (found.Count > 0 && found[0]["queryable"].AsBoolean) return;
            await Task.Delay(250);
        }

        throw new TimeoutException("The reference vector index never became queryable. Is mongot running? Try the doctor.");
    }
}

/// <summary>Shared helpers for working out what the right answer is.</summary>
public abstract class VectorTestBase(VectorFixture fixture)
{
    protected VectorFixture Fixture { get; } = fixture;
    protected GiftFinder Finder => Fixture.Finder;
    protected IMongoCollection<Toy> Toys => Fixture.Toys;
    protected static IReadOnlyList<Toy> Catalogue => VectorFixture.Catalogue;

    protected static float[] VectorOf(string toyName) => Catalogue.Single(t => t.Name == toyName).DescriptionEmbedding!;

    protected static float[] VectorForQuestion(string question) => VectorFixture.Questions.Embeddings[question];

    // Independent of the code under test: deliberately its own few lines.
    protected static double Cosine(float[] a, float[] b)
    {
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++) { dot += (double)a[i] * b[i]; na += (double)a[i] * a[i]; nb += (double)b[i] * b[i]; }
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    /// <summary>The honest answer: compare with every toy, best first, optionally only some of them.</summary>
    protected static List<(string Name, double Cosine)> Nearest(float[] query, int count, Func<Toy, bool>? where = null) =>
        Catalogue.Where(t => where?.Invoke(t) ?? true)
                 .Select(t => (t.Name, Cosine(query, t.DescriptionEmbedding!)))
                 .OrderByDescending(x => x.Item2).Take(count).ToList();

    protected static bool Suits(Toy t, int age) => t.InStock && t.MinAge <= age && age <= t.MaxAge;

    protected static void AssertBestFirst(IReadOnlyList<ToyHit> hits)
    {
        Assert.NotEmpty(hits);
        for (var i = 1; i < hits.Count; i++)
            Assert.True(hits[i - 1].Score >= hits[i].Score, "Results should come back closest first.");
    }

    /// <summary>What the server's vector search score is for each similarity.</summary>
    protected static double AtlasScore(string similarity, float[] query, float[] vector)
    {
        double dot = 0, gap2 = 0;
        for (var i = 0; i < query.Length; i++) { dot += (double)query[i] * vector[i]; gap2 += ((double)query[i] - vector[i]) * ((double)query[i] - vector[i]); }
        return similarity switch
        {
            "cosine" => (1 + Cosine(query, vector)) / 2,
            "dotProduct" => (1 + dot) / 2,
            "euclidean" => 1 / (1 + gap2),
            _ => throw new ArgumentOutOfRangeException(nameof(similarity)),
        };
    }
}

/// <summary>A stand-in for the network: answers every embeddings request from a function, and remembers
/// what it was asked.</summary>
internal sealed class FakeVoyage(Func<JsonNode, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<(HttpRequestMessage Request, JsonNode Body)> Calls { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!;
        Calls.Add((request, body));
        return respond(body);
    }

    /// <summary>A 200 whose single vector is <paramref name="vector"/>.</summary>
    public static HttpResponseMessage Ok(float[] vector) => new(System.Net.HttpStatusCode.OK)
    {
        Content = new StringContent(new JsonObject
        {
            ["object"] = "list",
            ["data"] = new JsonArray(new JsonObject
            {
                ["object"] = "embedding",
                ["embedding"] = new JsonArray(vector.Select(v => (JsonNode)JsonValue.Create(v)!).ToArray()),
                ["index"] = 0,
            }),
            ["model"] = "voyage-3.5",
            ["usage"] = new JsonObject { ["total_tokens"] = 7 },
        }.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
    };
}
