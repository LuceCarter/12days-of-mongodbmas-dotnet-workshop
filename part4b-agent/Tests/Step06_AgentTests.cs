using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Part4B.Starter;

namespace Part4B.Tests;

/// <summary>Step 6: the same agent, built with Microsoft Agent Framework. Badge objectives: retrieval tools; an agent's reasoning, tools and memory.
/// Needs MongoDB, and no key: a scripted model plays the language model and a stand-in plays the embedding model.</summary>
public sealed class Step06_AgentTests(AgentFixture fixture) : AgentTestBase(fixture)
{
    private HandbookSearch Handbook(FakeEmbedder embedder) => new(Fixture.Chunks, embedder, AgentFixture.ReferenceIndex);

    private static float[] VectorOfChunk(string id) => AgentFixture.Shipped.Chunks.Single(c => c.Id == id).Embedding!;

    private static ScriptedChat JustAnswers(string text = "Hello, elf.") => new((_, _) => Say(ChatRole.Assistant, text));

    [Fact(DisplayName = "Step 6 (an agent's reasoning) — it is an agent called the Elf Assistant, and it answers")]
    public async Task Step06_ItAnswers()
    {
        var agent = ElfAgent.CreateAgent(JustAnswers("Hello, elf."), Tools.CreateTools());

        var response = await agent.RunAsync("hi");

        Assert.Equal("Elf Assistant", agent.Name);
        Assert.Equal("Hello, elf.", response.Text);
    }

    [Fact(DisplayName = "Step 6 (an agent's reasoning) — the model is given the instructions, and they name every tool it has")]
    public async Task Step06_TheInstructionsReachTheModel()
    {
        var chat = JustAnswers();
        var agent = ElfAgent.CreateAgent(chat, Tools.CreateTools(), Handbook(new FakeEmbedder(_ => VectorOfChunk("toy-safety-1"))).AsTextSearch());

        await agent.RunAsync("hi");

        var instructions = chat.Calls[0].Options?.Instructions ?? "";
        Assert.Contains(ElfAgentInstructions.FindToysTool, instructions);
        Assert.Contains(ElfAgentInstructions.OrderStatusTool, instructions);
        Assert.Contains(ElfAgentInstructions.HandbookTool, instructions);
    }

    [Fact(DisplayName = "Step 6 (tools) — a tool the model asks for is run, and its result goes back to the model: the cycle, without you writing it")]
    public async Task Step06_TheFrameworkRunsTheCycle()
    {
        var chat = new ScriptedChat((_, n) => n == 1
            ? Asks(("c1", ElfAgentInstructions.OrderStatusTool, new() { ["orderNumber"] = "ORD-1001" }))
            : Say(ChatRole.Assistant, "It is open."));
        var agent = ElfAgent.CreateAgent(chat, Tools.CreateTools());

        var response = await agent.RunAsync("Where is ORD-1001?");

        Assert.Equal("It is open.", response.Text);
        Assert.Equal(2, chat.CallCount);
        Assert.Contains(Orders.Single(o => o.OrderNumber == "ORD-1001").Child, chat.Shown(2));
    }

    [Fact(DisplayName = "Step 6 (retrieval tools) — the handbook is OFFERED as a tool, next to the others")]
    public async Task Step06_TheHandbookIsATool()
    {
        var chat = JustAnswers();
        var agent = ElfAgent.CreateAgent(chat, Tools.CreateTools(), Handbook(new FakeEmbedder(_ => VectorOfChunk("toy-safety-1"))).AsTextSearch());

        await agent.RunAsync("hi");

        Assert.Equal([ElfAgentInstructions.FindToysTool, ElfAgentInstructions.OrderStatusTool, ElfAgentInstructions.HandbookTool], chat.ToolsOffered(1).Order());
    }

    [Fact(DisplayName = "Step 6 (retrieval tools) — and it is searched ON DEMAND: a question that doesn't need the handbook doesn't search it")]
    public async Task Step06_TheHandbookIsNotSearchedEveryTurn()
    {
        var embedder = new FakeEmbedder(_ => VectorOfChunk("toy-safety-1"));
        var agent = ElfAgent.CreateAgent(JustAnswers(), Tools.CreateTools(), Handbook(embedder).AsTextSearch());

        await agent.RunAsync("What is ORD-1001?");

        // Part 4A's RAG searched on every question. An agent decides: this model never asked, so nothing was embedded.
        Assert.Empty(embedder.Calls);
    }

    [Fact(DisplayName = "Step 6 (retrieval tools) — when the model does ask, the search runs and the handbook passages go back to it")]
    public async Task Step06_TheModelCanSearchTheHandbook()
    {
        var embedder = new FakeEmbedder(_ => VectorOfChunk("toy-safety-1"));
        var argument = await HandbookArgumentNameAsync();
        var chat = new ScriptedChat((_, n) => n == 1
            ? Asks(("c1", ElfAgentInstructions.HandbookTool, new() { [argument] = "what is a small part?" }))
            : Say(ChatRole.Assistant, "A part that fits the brass test tube is a small part."));
        var agent = ElfAgent.CreateAgent(chat, Tools.CreateTools(), Handbook(embedder).AsTextSearch());

        var response = await agent.RunAsync("How do I tell if a part is too small for a baby?");

        Assert.Contains("small part", response.Text);
        Assert.Equal("query", Assert.Single(embedder.Calls).InputType);
        var shown = chat.Shown(chat.CallCount);
        Assert.True(shown.Contains("brass test tube"), $"The model's last call showed:\n{shown}");
    }

    /// <summary>What the framework calls its search tool's one parameter: ask the agent's own tool definition, rather than assume.</summary>
    private async Task<string> HandbookArgumentNameAsync()
    {
        var probe = new ScriptedChat((_, _) => Say(ChatRole.Assistant, "x"));
        var agent = ElfAgent.CreateAgent(probe, Tools.CreateTools(), Handbook(new FakeEmbedder(_ => VectorOfChunk("toy-safety-1"))).AsTextSearch());
        await agent.RunAsync("hi");

        var function = (AIFunction)probe.Calls[0].Options!.Tools!.Single(t => t.Name == ElfAgentInstructions.HandbookTool);
        return function.JsonSchema.GetProperty("properties").EnumerateObject().First().Name;
    }

    [Fact(DisplayName = "Step 6 (an agent's tools) — with no handbook it has no handbook tool, and doesn't claim one")]
    public async Task Step06_NoHandbookNoHandbookTool()
    {
        var chat = JustAnswers();
        var agent = ElfAgent.CreateAgent(chat, Tools.CreateTools());

        await agent.RunAsync("hi");

        Assert.Equal([ElfAgentInstructions.FindToysTool, ElfAgentInstructions.OrderStatusTool], chat.ToolsOffered(1).Order());
        Assert.DoesNotContain(ElfAgentInstructions.HandbookTool, chat.Calls[0].Options?.Instructions ?? "");
    }
}
