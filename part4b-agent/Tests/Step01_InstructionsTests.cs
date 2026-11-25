using Part4B.Starter;

namespace Part4B.Tests;

/// <summary>Step 1: the agent's instructions. Badge objectives: an agent's reasoning, tools and memory; planning its tasks and data.
/// No database, no network, no key.</summary>
public sealed class Step01_InstructionsTests(AgentFixture fixture) : AgentTestBase(fixture)
{
    private static readonly string[] AllTools = [ElfAgentInstructions.FindToysTool, ElfAgentInstructions.OrderStatusTool, ElfAgentInstructions.HandbookTool];

    [Fact(DisplayName = "Step 1 (an agent's tools) — the instructions name every tool the agent has")]
    public void Step01_NamesEveryTool()
    {
        var text = ElfAgentInstructions.Build(AllTools);

        Assert.All(AllTools, tool => Assert.Contains(tool, text));
    }

    [Fact(DisplayName = "Step 1 (an agent's tools) — and only the tools it has: a tool it wasn't given isn't promised")]
    public void Step01_OnlyNamesTheToolsItWasGiven()
    {
        var text = ElfAgentInstructions.Build([ElfAgentInstructions.FindToysTool]);

        Assert.Contains(ElfAgentInstructions.FindToysTool, text);
        Assert.DoesNotContain(ElfAgentInstructions.OrderStatusTool, text);
        Assert.DoesNotContain(ElfAgentInstructions.HandbookTool, text);
    }

    [Fact(DisplayName = "Step 1 (planning tasks and data) — it says which kind of question each tool is for")]
    public void Step01_SaysWhenToUseEachTool()
    {
        var text = ElfAgentInstructions.Build(AllTools).ToLowerInvariant();

        // The plan: toys and stock -> the catalogue, an order -> the order book, rules -> the handbook.
        Assert.Matches("(toy|stock|catalogue)", text);
        Assert.Matches("order", text);
        Assert.Matches("(handbook|rule|procedure)", text);
    }

    [Fact(DisplayName = "Step 1 (an agent's reasoning) — it tells the agent to ask for a missing order number, and never to guess")]
    public void Step01_AsksInsteadOfGuessing()
    {
        var text = ElfAgentInstructions.Build(AllTools).ToLowerInvariant();

        Assert.Contains("order number", text);
        Assert.Matches("(ask|request)", text);
        Assert.Matches("(never|don't|do not|must not)", text);
        Assert.Matches("(guess|invent|make up|fabricate)", text);
    }

    [Fact(DisplayName = "Step 1 (an agent's memory) — it knows it has the earlier messages, so it doesn't ask for things twice")]
    public void Step01_KnowsItRemembers()
    {
        var text = ElfAgentInstructions.Build(AllTools).ToLowerInvariant();

        Assert.Matches("(earlier|previous|already|conversation|remember)", text);
    }

    [Fact(DisplayName = "Step 1 (an agent's reasoning) — it says who it is and who it helps")]
    public void Step01_SaysWhoItIs()
    {
        var text = ElfAgentInstructions.Build(AllTools);

        Assert.Contains("Elf Assistant", text);
        Assert.True(text.Length > 200, "A real set of instructions covers more than a sentence.");
    }
}
