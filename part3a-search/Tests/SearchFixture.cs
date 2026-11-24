using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using Part3A.Starter;
using SantasWorkshop.Shared;
using SantasWorkshop.Shared.Models;

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]
[assembly: AssemblyFixture(typeof(Part3A.Tests.SearchFixture))]

namespace Part3A.Tests;

/// <summary>
/// Builds the reference catalogue and two <b>reference</b> search indexes once, before any test runs.
/// </summary>
/// <remarks>
/// <para>Search tests only <em>read</em>, so they can share one catalogue. What they can't share is
/// <b>your</b> indexes: a test for Step 1 has to watch <i>your</i> <c>CreateDynamicIndexAsync</c> create an
/// index, and a test for Step 9 shouldn't fail because Step 5 isn't done yet. So the queries in Steps 2
/// and 4 to 10 are tested against reference indexes this fixture builds itself, and Steps 1 and 5 are
/// tested by running your code against a scratch collection.</para>
/// <para>The data lives in its <b>own database</b>, <see cref="DatabaseName"/>, so running the tests never
/// touches the practice data you explore in Compass.</para>
/// </remarks>
public sealed class SearchFixture : IAsyncLifetime
{
    public const string DatabaseName = "santas_workshop_part3a_tests";
    public const string ReferenceDynamic = "ref-dynamic";
    public const string ReferenceStatic = "ref-static";

    /// <summary>The static mapping the reference index uses. It is the answer to Step 5, so the
    /// Step 5 test is free to check yours against the same expectations.</summary>
    public const string StaticDefinition = """
        { mappings: { dynamic: false, fields: {
            name:         [ { type: "string" }, { type: "autocomplete" } ],
            description:  { type: "string", analyzer: "lucene.english" },
            tags:         { type: "string" },
            category:     { type: "token" },
            price:        { type: "number" },
            minAge:       { type: "number" },
            maxAge:       { type: "number" },
            inStock:      { type: "boolean" },
            discontinued: { type: "boolean" }
        } } }
        """;

    public IMongoClient Client { get; private set; } = null!;
    public IMongoDatabase Database { get; private set; } = null!;
    public IMongoCollection<Toy> Toys { get; private set; } = null!;

    /// <summary>A <see cref="ToySearch"/> pointed at the reference indexes.</summary>
    public ToySearch Search { get; private set; } = null!;

    /// <summary>The data as it is on disk, for working out what the right answer is.</summary>
    public static IReadOnlyList<Toy> Catalogue { get; } = SeedLoader.LoadToys();

    public async ValueTask InitializeAsync()
    {
        Client = MongoContext.CreateClient();
        Database = MongoContext.GetDatabase(Client, DatabaseName);

        try
        {
            // Dropping a collection drops its search indexes too, so every run starts clean. That includes
            // the scratch collections earlier runs left behind.
            foreach (var leftover in await (await Database.ListCollectionNamesAsync()).ToListAsync())
                if (leftover.StartsWith("scratch_", StringComparison.Ordinal))
                    await Database.DropCollectionAsync(leftover);

            await Database.DropCollectionAsync("toys");
            Toys = Database.GetCollection<Toy>("toys");
            await Toys.InsertManyAsync(Catalogue);

            await CreateAsync(Toys, ReferenceDynamic, """{ mappings: { dynamic: true } }""");
            await CreateAsync(Toys, ReferenceStatic, StaticDefinition);
        }
        catch (Exception ex) when (ex is not TimeoutException)
        {
            throw new InvalidOperationException(
                $"""

                 Couldn't reach MongoDB with Atlas Search, so these tests can't run.

                 Start it from the repo root:
                     docker compose up -d

                 Then check everything is healthy (it checks Search too):
                     dotnet run --project tools/doctor

                 (Underlying error: {ex.GetType().Name} — {ex.Message.Split('\n')[0]})

                 """, ex);
        }

        Search = new ToySearch(Toys, ReferenceDynamic, ReferenceStatic);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>Makes a throwaway copy of the catalogue for a test that creates indexes itself.</summary>
    /// <remarks>
    /// The collection name gets a unique suffix every time. Search indexes are torn down in the background
    /// when a collection is dropped, so re-using a name straight away can collide with the old index.
    /// </remarks>
    public async Task<IMongoCollection<Toy>> FreshScratchCollectionAsync(string name)
    {
        var scratch = Database.GetCollection<Toy>($"{name}_{Guid.NewGuid():N}");
        await scratch.InsertManyAsync(Catalogue);
        return scratch;
    }

    /// <summary>Creates a search index and waits until it is queryable. Written out here rather than
    /// borrowed from <see cref="ToySearch"/>, so the fixture never depends on your code.</summary>
    private static async Task CreateAsync(IMongoCollection<Toy> collection, string name, string json)
    {
        await collection.SearchIndexes.CreateOneAsync(new CreateSearchIndexModel(name, BsonDocument.Parse(json)));

        var giveUpAt = DateTime.UtcNow.AddMinutes(3);
        while (DateTime.UtcNow < giveUpAt)
        {
            var found = await (await collection.SearchIndexes.ListAsync(name)).ToListAsync();
            if (found.Count > 0 && found[0]["queryable"].AsBoolean) return;
            await Task.Delay(250);
        }

        throw new TimeoutException($"The reference index '{name}' never became queryable. Is mongot running? Try the doctor.");
    }
}

/// <summary>Shared helpers for working out what the right answer is.</summary>
public abstract class SearchTestBase(SearchFixture fixture)
{
    protected SearchFixture Fixture { get; } = fixture;
    protected ToySearch Search => Fixture.Search;
    protected IMongoCollection<Toy> Toys => Fixture.Toys;
    protected static IReadOnlyList<Toy> Catalogue => SearchFixture.Catalogue;

    /// <summary>Toys that have <paramref name="word"/> as a whole word in the name, description or tags.</summary>
    protected static List<string> ToysMentioning(string word, bool name = true, bool description = true, bool tags = true) =>
        Catalogue.Where(t =>
                (name && HasWord(t.Name, word)) ||
                (description && HasWord(t.Description, word)) ||
                (tags && t.Tags.Any(tag => HasWord(tag, word))))
            .Select(t => t.Name).ToList();

    private static bool HasWord(string text, string word) =>
        Regex.IsMatch(text, $@"\b{Regex.Escape(word)}\b", RegexOptions.IgnoreCase);

    protected static void AssertSameToys(IEnumerable<string> expected, IEnumerable<ToyHit> found, string because = "")
    {
        var want = expected.OrderBy(n => n).ToList();
        var got = found.Select(h => h.Name).OrderBy(n => n).ToList();
        Assert.True(want.SequenceEqual(got),
            $"{because}\n  expected ({want.Count}): {string.Join(", ", want)}\n  got      ({got.Count}): {string.Join(", ", got)}");
    }

    protected static void AssertBestFirst(IReadOnlyList<ToyHit> hits)
    {
        Assert.NotEmpty(hits);
        Assert.All(hits, h => Assert.True(h.Score > 0, $"{h.Name} has a score of {h.Score}. Did the projection ask for searchScore?"));
        for (var i = 1; i < hits.Count; i++)
            Assert.True(hits[i - 1].Score >= hits[i].Score, "Results should come back best match first.");
    }
}
