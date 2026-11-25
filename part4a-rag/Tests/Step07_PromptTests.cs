using Microsoft.Extensions.AI;
using Part4A.Starter;

namespace Part4A.Tests;

/// <summary>Step 7: the grounded prompt. Badge objectives: what RAG is, and the LLM limitations it addresses.
/// No database, no network, no key: it only builds text.</summary>
public sealed class Step07_PromptTests(HandbookFixture fixture) : HandbookTestBase(fixture)
{
    private const string Question = "How do I know if a toy is safe for a baby?";

    private static List<RetrievedChunk> Chunks() =>
    [
        new() { Id = "toy-safety-1", Section = "Toy Safety", Audience = "toy-makers", Source = "The Elf Handbook › Toy Safety", ChunkIndex = 1, Text = "Drop each loose part into the brass test tube.", Score = 0.82 },
        new() { Id = "quality-control-0", Section = "Quality Control", Audience = "toy-makers", Source = "The Elf Handbook › Quality Control", ChunkIndex = 0, Text = "Every finished toy goes to the Quality Bench.", Score = 0.74 },
    ];

    [Fact(DisplayName = "Step 7 (what RAG is) — it builds two messages: the rules, then the extracts and the question")]
    public void Step07_TwoMessagesInOrder()
    {
        var messages = GroundedPrompt.BuildPrompt(Question, Chunks());

        Assert.Equal(2, messages.Count);
        Assert.Equal(ChatRole.System, messages[0].Role);
        Assert.Equal(ChatRole.User, messages[1].Role);
    }

    [Fact(DisplayName = "Step 7 (LLM limitations) — the rules say to use only the extracts, and to admit it when they don't help")]
    public void Step07_TheRulesStopTheModelGuessing()
    {
        var rules = GroundedPrompt.BuildPrompt(Question, Chunks())[0].Text.ToLowerInvariant();

        Assert.Contains("only", rules);
        Assert.Matches("(do not|don't|doesn't|does not|cannot|can't|not) (know|contain|say|find|answer)", rules);
        Assert.Contains("source", rules);
    }

    [Fact(DisplayName = "Step 7 (what RAG is) — the extracts' text and sources are in the prompt, best first")]
    public void Step07_TheExtractsAreInThePrompt()
    {
        var chunks = Chunks();

        var user = GroundedPrompt.BuildPrompt(Question, chunks)[1].Text;

        foreach (var chunk in chunks)
        {
            Assert.Contains(chunk.Text, user);
            Assert.Contains(chunk.Source, user);
        }

        Assert.True(user.IndexOf(chunks[0].Text, StringComparison.Ordinal) < user.IndexOf(chunks[1].Text, StringComparison.Ordinal),
            "The best extract should come first.");
    }

    [Fact(DisplayName = "Step 7 (what RAG is) — the question comes last, after every extract")]
    public void Step07_TheQuestionIsLast()
    {
        var chunks = Chunks();

        var user = GroundedPrompt.BuildPrompt(Question, chunks)[1].Text;

        Assert.Contains(Question, user);
        Assert.True(user.LastIndexOf(Question, StringComparison.Ordinal) > user.IndexOf(chunks[^1].Text, StringComparison.Ordinal),
            "Put the question after the extracts.");
    }

    [Fact(DisplayName = "Step 7 (what RAG is) — with no extracts it still builds a prompt, so the model can be told there is nothing")]
    public void Step07_NoExtractsStillBuilds()
    {
        var messages = GroundedPrompt.BuildPrompt(Question, []);

        Assert.Equal(2, messages.Count);
        Assert.Contains(Question, messages[1].Text);
    }
}
