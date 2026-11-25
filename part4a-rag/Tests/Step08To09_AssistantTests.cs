using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Part4A.Starter;

namespace Part4A.Tests;

/// <summary>Steps 8–9: the whole pipeline, and a retrieval tool for an agent.
/// Badge objectives: the RAG architecture's components; a generator separate from the embedding model.
/// No network and no key: stand-ins play the embedding model and the generator.</summary>
public sealed class Step08To09_AssistantTests(HandbookFixture fixture) : HandbookTestBase(fixture)
{
    private const string Question = "How do I know if a toy is safe for a baby?";

    private ElfAssistant AssistantFor(float[] questionVector, out FakeEmbedder embedder, out FakeChat chat, double minScore = ElfAssistant.DefaultMinScore, string reply = "Use the brass test tube.")
    {
        embedder = new FakeEmbedder(_ => questionVector);
        chat = new FakeChat(reply);
        return new ElfAssistant(Retriever, embedder, chat, minScore);
    }

    // ---- Step 8 ---------------------------------------------------------------------------

    [Fact(DisplayName = "Step 8 (RAG architecture) — a question is embedded as a QUERY, then the retrieved text goes to the generator")]
    public async Task Step08_EmbedsRetrievesAndGenerates()
    {
        var assistant = AssistantFor(VectorForQuestion(Question), out var embedder, out var chat);

        var answer = await assistant.AskAsync(Question);

        var (text, inputType) = Assert.Single(embedder.Calls);
        Assert.Equal(Question, text);
        Assert.Equal("query", inputType);

        var messages = Assert.Single(chat.Calls);
        var topChunk = Shipped.Single(c => c.Id == Nearest(VectorForQuestion(Question), 1)[0].Id);
        Assert.Contains(topChunk.Text, string.Join("\n", messages.Select(m => m.Text)));

        Assert.Equal("Use the brass test tube.", answer.Text);
        Assert.True(answer.Grounded);
    }

    [Fact(DisplayName = "Step 8 (a generator separate from the embedding model) — the embedder is only ever given the question; the generator only ever gets the prompt")]
    public async Task Step08_TheTwoModelsDoDifferentJobs()
    {
        var assistant = AssistantFor(VectorForQuestion(Question), out var embedder, out var chat);

        await assistant.AskAsync(Question);

        // The embedding model sees the question and nothing else: it is not asked to write an answer, and it never
        // sees the handbook text at search time. The generator is never asked for a vector.
        Assert.All(embedder.Calls, call => Assert.Equal(Question, call.Text));
        var prompt = Assert.Single(chat.Calls);
        Assert.Contains(prompt, m => m.Role == ChatRole.System);
        Assert.Contains(Shipped.Select(c => c.Text), t => prompt.Any(m => (m.Text ?? "").Contains(t)));
    }

    [Fact(DisplayName = "Step 8 (RAG architecture) — the answer names the chunks it was built from, best first")]
    public async Task Step08_CitesItsSources()
    {
        var assistant = AssistantFor(VectorForQuestion(Question), out _, out _, minScore: 0);

        var answer = await assistant.AskAsync(Question, k: 3);

        var expected = Nearest(VectorForQuestion(Question), 3).Select(x => x.Id).ToList();
        Assert.Equal(expected, answer.Sources.Select(s => s.ChunkId));
        Assert.All(answer.Sources, s => Assert.Equal(Shipped.Single(c => c.Id == s.ChunkId).Source, s.Source));
    }

    [Fact(DisplayName = "Step 8 (a prefiltered retriever) — a section and an audience narrow what the generator is shown")]
    public async Task Step08_TheFilterReachesTheRetriever()
    {
        var assistant = AssistantFor(VectorForQuestion(Question), out _, out var chat, minScore: 0);

        var answer = await assistant.AskAsync(Question, section: "Reindeer Care", k: 3);

        Assert.Equal(3, answer.Sources.Count);
        Assert.All(answer.Sources, s => Assert.StartsWith("reindeer-care-", s.ChunkId));
        var shown = string.Join("\n", Assert.Single(chat.Calls).Select(m => m.Text));
        Assert.DoesNotContain(Shipped.First(c => c.Section == "Toy Safety").Text, shown);
    }

    [Fact(DisplayName = "Step 8 (LLM limitations) — when nothing in the handbook is close enough it says so, and the model isn't asked")]
    public async Task Step08_AdmitsWhenItDoesNotKnow()
    {
        // The exact opposite of a real chunk's vector: as unlike the handbook as a vector can be.
        var opposite = VectorOfChunk("toy-safety-1").Select(x => -x).ToArray();
        var assistant = AssistantFor(opposite, out _, out var chat);

        var answer = await assistant.AskAsync("What is the capital of France?");

        Assert.Equal(ElfAssistant.NoAnswer, answer.Text);
        Assert.False(answer.Grounded);
        Assert.Empty(answer.Sources);
        Assert.Empty(chat.Calls);
    }

    [Fact(DisplayName = "Step 8 (LLM limitations) — a high enough bar keeps even an exact match out, and the same question gets an answer when the bar is low")]
    public async Task Step08_TheBarIsTheMinimumScore()
    {
        var vector = VectorOfChunk("toy-safety-1");

        var strict = AssistantFor(vector, out _, out var strictChat, minScore: 1.01);
        var relaxed = AssistantFor(vector, out _, out var relaxedChat, minScore: 0.9);

        Assert.False((await strict.AskAsync(Question)).Grounded);
        Assert.Empty(strictChat.Calls);

        var found = await relaxed.AskAsync(Question);
        Assert.True(found.Grounded);
        Assert.Equal("toy-safety-1", found.Sources[0].ChunkId);
        Assert.Single(relaxedChat.Calls);
    }

    // ---- Step 9 ---------------------------------------------------------------------------

    [Fact(DisplayName = "Step 9 (a retrieval tool) — it becomes a function an agent can call: a question in, handbook extracts out")]
    public async Task Step09_ReturnsExtracts()
    {
        var assistant = AssistantFor(VectorForQuestion(Question), out var embedder, out _, minScore: 0);

        var search = assistant.AsTextSearch(k: 3);
        var results = (await search(Question, CancellationToken.None)).ToList();

        Assert.Equal("query", Assert.Single(embedder.Calls).InputType);
        var expected = Nearest(VectorForQuestion(Question), 3).Select(x => Shipped.Single(c => c.Id == x.Id)).ToList();
        Assert.Equal(expected.Select(c => c.Text), results.Select(r => r.Text));
        Assert.Equal(expected.Select(c => c.Source), results.Select(r => r.SourceName));
    }

    [Fact(DisplayName = "Step 9 (a retrieval tool) — the tool can be narrowed to a section, and it drops chunks that aren't close enough")]
    public async Task Step09_RespectsTheFilterAndTheBar()
    {
        var narrowed = AssistantFor(VectorForQuestion(Question), out _, out _, minScore: 0).AsTextSearch(k: 3, section: "Emergencies");
        var inSection = (await narrowed(Question, CancellationToken.None)).ToList();
        Assert.Equal(3, inSection.Count);
        Assert.All(inSection, r => Assert.Equal("The Elf Handbook › Emergencies", r.SourceName));

        var opposite = VectorOfChunk("toy-safety-1").Select(x => -x).ToArray();
        var nothing = AssistantFor(opposite, out _, out _).AsTextSearch();
        Assert.Empty(await nothing("What is the capital of France?", CancellationToken.None));
    }

    [Fact(DisplayName = "Step 9 (a retrieval tool) — Agent Framework accepts it as a TextSearchProvider")]
    public void Step09_AgentFrameworkAcceptsIt()
    {
        var assistant = AssistantFor(VectorForQuestion(Question), out _, out _);

        var provider = new TextSearchProvider(assistant.AsTextSearch(), new TextSearchProviderOptions
        {
            SearchTime = TextSearchProviderOptions.TextSearchBehavior.OnDemandFunctionCalling,
            FunctionToolName = "SearchElfHandbook",
            FunctionToolDescription = "Searches the Elf Handbook for rules and procedures.",
        });

        Assert.NotNull(provider);
    }
}
