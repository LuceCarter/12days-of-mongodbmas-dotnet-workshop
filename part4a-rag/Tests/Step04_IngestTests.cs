using MongoDB.Driver;
using Part4A.Starter;
using SantasWorkshop.Shared.Models;

namespace Part4A.Tests;

/// <summary>Step 4: ingestion. Badge objective: data collection, preparation and ingestion.
/// No network and no key: a stand-in plays the embedding model.</summary>
public sealed class Step04_IngestTests(HandbookFixture fixture) : HandbookTestBase(fixture)
{
    // Two short sections: the first long enough for two chunks at 80 words with 20 of overlap.
    private static readonly string LongBody = string.Join(' ', Enumerable.Range(1, 100).Select(i => $"word{i}"));

    private static readonly List<HandbookSection> Tiny =
    [
        new("Toy Safety", "toy-makers", LongBody),
        new("Elf Wellbeing", "all-elves", "A short section that is a single chunk."),
    ];

    private async Task<(IMongoCollection<HandbookChunk> Collection, Ingestor Ingestor, FakeEmbedder Embedder)> BuildAsync(string name)
    {
        var collection = await Fixture.FreshScratchCollectionAsync($"scratch_{name}", withChunks: false);
        var embedder = new FakeEmbedder(VectorFromText);
        return (collection, new Ingestor(collection, embedder), embedder);
    }

    [Fact(DisplayName = "Step 4 (ingestion) — it stores one document per chunk and says how many")]
    public async Task Step04_StoresEveryChunk()
    {
        var (collection, ingestor, _) = await BuildAsync("ingest-count");

        var stored = await ingestor.IngestAsync(Tiny);

        Assert.Equal(3, stored);
        Assert.Equal(3, await collection.CountDocumentsAsync(FilterDefinition<HandbookChunk>.Empty));
    }

    [Fact(DisplayName = "Step 4 (ingestion) — each chunk remembers where it came from")]
    public async Task Step04_KeepsTheMetadata()
    {
        var (collection, ingestor, _) = await BuildAsync("ingest-metadata");

        await ingestor.IngestAsync(Tiny);

        var second = await collection.Find(c => c.Id == "toy-safety-1").SingleAsync();
        Assert.Equal("Toy Safety", second.Section);
        Assert.Equal("toy-makers", second.Audience);
        Assert.Equal("The Elf Handbook › Toy Safety", second.Source);
        Assert.Equal(1, second.ChunkIndex);

        var wellbeing = await collection.Find(c => c.Id == "elf-wellbeing-0").SingleAsync();
        Assert.Equal("all-elves", wellbeing.Audience);
        Assert.Equal(0, wellbeing.ChunkIndex);
    }

    [Fact(DisplayName = "Step 4 (ingestion) — the stored text is the chunker's text, and it overlaps")]
    public async Task Step04_StoresTheChunkText()
    {
        var (collection, ingestor, _) = await BuildAsync("ingest-text");

        await ingestor.IngestAsync(Tiny);

        var stored = await collection.Find(c => c.Section == "Toy Safety").SortBy(c => c.ChunkIndex).ToListAsync();
        Assert.Equal(ExpectedChunks(LongBody, Ingestor.DefaultChunkWords, Ingestor.DefaultOverlapWords), stored.Select(c => c.Text));
        Assert.EndsWith("word80", stored[0].Text);
        Assert.StartsWith("word61", stored[1].Text);
    }

    [Fact(DisplayName = "Step 4 (ingestion) — each chunk's vector is what the embedding model said, and chunks are embedded as documents")]
    public async Task Step04_EmbedsEachChunkAsADocument()
    {
        var (collection, ingestor, embedder) = await BuildAsync("ingest-vectors");

        await ingestor.IngestAsync(Tiny);

        Assert.Equal(3, embedder.Calls.Count);
        Assert.All(embedder.Calls, call => Assert.Equal("document", call.InputType));

        foreach (var chunk in await collection.Find(FilterDefinition<HandbookChunk>.Empty).ToListAsync())
            Assert.Equal(VectorFromText(chunk.Text), chunk.Embedding);
    }

    [Fact(DisplayName = "Step 4 (ingestion) — running it twice leaves one copy of each chunk, not two")]
    public async Task Step04_IsSafeToRunTwice()
    {
        var (collection, ingestor, _) = await BuildAsync("ingest-twice");

        await ingestor.IngestAsync(Tiny);
        var again = await ingestor.IngestAsync(Tiny);

        Assert.Equal(3, again);
        Assert.Equal(3, await collection.CountDocumentsAsync(FilterDefinition<HandbookChunk>.Empty));
    }

    [Fact(DisplayName = "Step 4 (ingestion) — when a section gets shorter, the chunks it no longer has are removed, not left behind")]
    public async Task Step04_RemovesChunksThatNoLongerExist()
    {
        var (collection, ingestor, _) = await BuildAsync("ingest-shrink");
        await ingestor.IngestAsync(Tiny);
        Assert.Equal(2, await collection.CountDocumentsAsync(c => c.Section == "Toy Safety"));

        // The handbook is edited: Toy Safety is now short enough for one chunk.
        var edited = new List<HandbookSection> { Tiny[0] with { Body = "Much shorter now." }, Tiny[1] };
        await ingestor.IngestAsync(edited);

        Assert.Equal(1, await collection.CountDocumentsAsync(c => c.Section == "Toy Safety"));
        Assert.Equal("Much shorter now.", (await collection.Find(c => c.Id == "toy-safety-0").SingleAsync()).Text);
        Assert.Equal(0, await collection.CountDocumentsAsync(c => c.Id == "toy-safety-1"));
    }

    [Fact(DisplayName = "Step 4 (ingestion) — over the whole handbook it makes the shipped chunks: same ids, same text")]
    public async Task Step04_IngestingTheHandbookMakesTheShippedChunks()
    {
        var (collection, ingestor, _) = await BuildAsync("ingest-handbook");

        var stored = await ingestor.IngestAsync(ReferenceSections());

        var yours = (await collection.Find(FilterDefinition<HandbookChunk>.Empty).ToListAsync()).OrderBy(c => c.Id).ToList();
        var shipped = Shipped.OrderBy(c => c.Id).ToList();

        Assert.Equal(shipped.Count, stored);
        Assert.Equal(shipped.Select(c => c.Id), yours.Select(c => c.Id));
        Assert.Equal(shipped.Select(c => c.Text), yours.Select(c => c.Text));
        Assert.Equal(shipped.Select(c => c.Audience), yours.Select(c => c.Audience));
    }
}
