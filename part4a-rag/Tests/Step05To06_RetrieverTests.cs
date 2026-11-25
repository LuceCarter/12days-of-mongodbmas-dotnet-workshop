using MongoDB.Bson;
using MongoDB.Driver;
using Part4A.Starter;
using SantasWorkshop.Shared.Models;

namespace Part4A.Tests;

/// <summary>Steps 5–6: the index with filter fields, and a prefiltered retriever.
/// Badge objective: a prefiltered retriever. Needs Atlas Local, and no key: the chunks ship with their vectors.</summary>
public sealed class Step05To06_RetrieverTests(HandbookFixture fixture) : HandbookTestBase(fixture)
{
    // ---- Step 5 ---------------------------------------------------------------------------

    private async Task<(IMongoCollection<HandbookChunk> Scratch, Retriever Yours)> BuildAsync(string name)
    {
        var scratch = await Fixture.FreshScratchCollectionAsync($"scratch_{name}");
        return (scratch, new Retriever(scratch, indexName: $"unused-{name}"));
    }

    private static async Task<BsonArray> FieldsAsync(IMongoCollection<HandbookChunk> collection, string index) =>
        (await (await collection.SearchIndexes.ListAsync(index)).ToListAsync()).Single()["latestDefinition"]["fields"].AsBsonArray;

    [Fact(DisplayName = "Step 5 (prefiltered retriever) — the index is a vector index over the chunks' embedding, with the right number of dimensions")]
    public async Task Step05_DeclaresTheVectorField()
    {
        var (scratch, yours) = await BuildAsync("idx-vector");

        await yours.CreateIndexAsync("idx-vector");

        var vector = (await FieldsAsync(scratch, "idx-vector")).Select(f => f.AsBsonDocument).Single(f => f["type"] == "vector");
        Assert.Equal("embedding", vector["path"].AsString);
        Assert.Equal(1024, vector["numDimensions"].ToInt32());
        Assert.Equal("cosine", vector["similarity"].AsString);
    }

    [Fact(DisplayName = "Step 5 (prefiltered retriever) — section and audience are declared as filter fields")]
    public async Task Step05_DeclaresTheFilterFields()
    {
        var (scratch, yours) = await BuildAsync("idx-filter");

        await yours.CreateIndexAsync("idx-filter");

        var filters = (await FieldsAsync(scratch, "idx-filter")).Select(f => f.AsBsonDocument)
            .Where(f => f["type"] == "filter").Select(f => f["path"].AsString).Order().ToList();
        Assert.Equal(["audience", "section"], filters);
    }

    [Fact(DisplayName = "Step 5 (prefiltered retriever) — it returns only once the index answers queries, and it finds a chunk by its own vector")]
    public async Task Step05_TheIndexAnswersQueries()
    {
        var (scratch, yours) = await BuildAsync("idx-query");

        await yours.CreateIndexAsync("idx-query");

        // Written here with the plain driver, so this checks YOUR index and not your retriever.
        var options = new VectorSearchOptions<HandbookChunk> { IndexName = "idx-query", NumberOfCandidates = 100 };

        // A new index is "queryable" a moment before it has read every document. Give it time to catch up
        // (the same eventual consistency you'd meet in production), then ask.
        List<HandbookChunk> found = [];
        for (var attempt = 0; attempt < 120 && found.Count == 0; attempt++)
        {
            found = await scratch.Aggregate()
                .VectorSearch(c => c.Embedding, new QueryVector(VectorOfChunk("reindeer-care-1")), 3, options)
                .ToListAsync();
            if (found.Count == 0) await Task.Delay(250);
        }

        Assert.Equal("reindeer-care-1", found[0].Id);
    }

    // ---- Step 6 ---------------------------------------------------------------------------

    private static readonly string Bedtime = "How do I know if a toy is safe for a baby?";
    private static readonly string ReindeerQuestion = "What should I do when a reindeer won't eat?";

    [Fact(DisplayName = "Step 6 (prefiltered retriever) — with no filter it finds the same chunks as comparing against every one")]
    public async Task Step06_MatchesTheBruteForceAnswer()
    {
        var query = VectorForQuestion(Bedtime);

        var found = await Retriever.RetrieveAsync(query, k: 5);

        Assert.Equal(Nearest(query, 5).Select(x => x.Id), found.Select(c => c.Id));
        Assert.Equal(5, found.Count);
    }

    [Fact(DisplayName = "Step 6 (prefiltered retriever) — results come best first, each with its source and text, and the score is the rescaled cosine")]
    public async Task Step06_ReturnsScoredChunks()
    {
        var query = VectorForQuestion(Bedtime);

        var found = await Retriever.RetrieveAsync(query, k: 4);

        for (var i = 1; i < found.Count; i++)
            Assert.True(found[i - 1].Score >= found[i].Score, "Closest first.");

        var top = found[0];
        var chunk = Shipped.Single(c => c.Id == top.Id);
        Assert.Equal(chunk.Text, top.Text);
        Assert.Equal(chunk.Source, top.Source);
        Assert.Equal(chunk.Section, top.Section);
        Assert.Equal((1 + Cosine(query, chunk.Embedding!)) / 2, top.Score, precision: 3);
    }

    [Fact(DisplayName = "Step 6 (prefiltered retriever) — naming a section looks only inside that section")]
    public async Task Step06_FiltersBySection()
    {
        // A question about reindeer, asked of the Toy Safety section alone.
        var query = VectorForQuestion(ReindeerQuestion);

        var found = await Retriever.RetrieveAsync(query, k: 3, section: "Toy Safety");

        Assert.Equal(3, found.Count);
        Assert.All(found, c => Assert.Equal("Toy Safety", c.Section));
        Assert.Equal(Nearest(query, 3, c => c.Section == "Toy Safety").Select(x => x.Id), found.Select(c => c.Id));
    }

    [Fact(DisplayName = "Step 6 (prefiltered retriever) — the same question without the filter finds the reindeer chunks (so the filter really did something)")]
    public async Task Step06_TheFilterChangedTheAnswer()
    {
        var query = VectorForQuestion(ReindeerQuestion);

        var unfiltered = await Retriever.RetrieveAsync(query, k: 3);

        Assert.Equal("Reindeer Care", unfiltered[0].Section);
    }

    [Fact(DisplayName = "Step 6 (prefiltered retriever) — naming an audience looks only at chunks written for it")]
    public async Task Step06_FiltersByAudience()
    {
        var query = VectorForQuestion(Bedtime);

        var found = await Retriever.RetrieveAsync(query, k: 6, audience: "toy-makers");

        Assert.Equal(6, found.Count);
        Assert.All(found, c => Assert.Equal("toy-makers", c.Audience));
        Assert.Equal(Nearest(query, 6, c => c.Audience == "toy-makers").Select(x => x.Id), found.Select(c => c.Id));
    }

    [Fact(DisplayName = "Step 6 (prefiltered retriever) — a section and an audience together must both match")]
    public async Task Step06_BothFiltersMustMatch()
    {
        var query = VectorForQuestion(Bedtime);

        var found = await Retriever.RetrieveAsync(query, k: 4, section: "Quality Control", audience: "toy-makers");
        var impossible = await Retriever.RetrieveAsync(query, k: 4, section: "Quality Control", audience: "stable-elves");

        Assert.NotEmpty(found);
        Assert.All(found, c => { Assert.Equal("Quality Control", c.Section); Assert.Equal("toy-makers", c.Audience); });
        Assert.Empty(impossible);
    }

    [Fact(DisplayName = "Step 6 (prefiltered retriever) — it still returns k chunks when the nearest ones are all in the wrong section: that is what a PRE-filter does")]
    public async Task Step06_APreFilterKeepsLookingUntilItHasEnough()
    {
        var query = VectorForQuestion(ReindeerQuestion);
        var inSection = Shipped.Count(c => c.Section == "Elf Wellbeing");

        // The nearest chunks to a reindeer question are about reindeer. A post-filter would search, find those, and
        // throw them all away. A pre-filter searches only Elf Wellbeing, so it has plenty to return.
        var found = await Retriever.RetrieveAsync(query, k: inSection, section: "Elf Wellbeing");

        Assert.Equal(inSection, found.Count);
        Assert.All(found, c => Assert.Equal("Elf Wellbeing", c.Section));
    }

    [Fact(DisplayName = "Step 6 (prefiltered retriever) — a section that doesn't exist gives nothing, not an error")]
    public async Task Step06_NothingMatchesTheFilter()
    {
        var found = await Retriever.RetrieveAsync(VectorForQuestion(Bedtime), k: 3, section: "No Such Section");

        Assert.Empty(found);
    }

    [Fact(DisplayName = "Step 6 (prefiltered retriever) — it returns no more than k")]
    public async Task Step06_HonoursK()
    {
        var found = await Retriever.RetrieveAsync(VectorForQuestion(Bedtime), k: 2);

        Assert.Equal(2, found.Count);
    }

    // The question and the section that should hold the answer, against the REAL vectors. These check the idea,
    // so they assert the section, never an exact score.
    [Theory(DisplayName = "Step 6 (prefiltered retriever) — a question in plain words finds the right section of the handbook")]
    [InlineData("How do I know if a toy is safe for a baby?", "Toy Safety")]
    [InlineData("What should I do when a reindeer won't eat?", "Reindeer Care")]
    [InlineData("How do I wrap something fragile?", "Wrapping and Labelling")]
    [InlineData("In what order do presents go onto the sleigh?", "Loading the Sleigh")]
    [InlineData("A child wrote to say they are sorry for being naughty. How is that counted?", "The Naughty and Nice Ledger")]
    [InlineData("I'm worn out and it's the busiest week of the year.", "Elf Wellbeing")]
    [InlineData("Can I show my family around the toy floor?", "Workshop Rules")]
    [InlineData("What should I do if a machine catches fire?", "Emergencies")]
    public async Task Step06_FindsTheRightSection(string question, string expectedSection)
    {
        var found = await Retriever.RetrieveAsync(VectorForQuestion(question), k: 3);

        Assert.True(found[0].Section == expectedSection,
            $"For \"{question}\" the best chunk was in {found[0].Section}, not {expectedSection}. The top three were in: {string.Join(", ", found.Select(c => c.Section))}.");
    }
}
