using Microsoft.Extensions.AI;
using Part4B.Starter;

namespace Part4B.Tests;

/// <summary>Step 5: the model-and-tools cycle. Badge objectives: cyclical graphs; tools and LLM function calling.
/// No database, no network, no key: a scripted model plays the language model.</summary>
public sealed class Step05_ToolLoopTests(AgentFixture fixture) : AgentTestBase(fixture)
{
    /// <summary>A tool that records what it was asked and answers with <paramref name="answer"/>.</summary>
    private static (AIFunction Tool, List<string> Calls) Echo(string name, string answer)
    {
        var calls = new List<string>();
        var function = AIFunctionFactory.Create((string input) => { calls.Add(input); return answer; }, name, $"Test tool {name}.");
        return (function, calls);
    }

    private static string ThrowingTool(string input) => throw new InvalidOperationException("the shelf is on fire");

    private static Dictionary<string, object?> Arg(string value) => new() { ["input"] = value };

    private static List<ChatMessage> Start(string question) => [Say(ChatRole.User, question)];

    [Fact(DisplayName = "Step 5 (cyclical graphs) — a model that just answers is asked once, and there is no cycle")]
    public async Task Step05_NoToolNeeded()
    {
        var chat = new ScriptedChat((_, _) => Say(ChatRole.Assistant, "Hello, elf."));

        var result = await ToolLoop.RunAsync(chat, Start("hi"), []);

        Assert.Equal("Hello, elf.", result.Text);
        Assert.Equal(1, result.ModelCalls);
        Assert.Equal(1, chat.CallCount);
        Assert.Single(result.NewMessages);
    }

    [Fact(DisplayName = "Step 5 (cyclical graphs) — a tool call sends the result BACK to the model: two model calls, one tool run")]
    public async Task Step05_OneTurnOfTheCycle()
    {
        var (tool, calls) = Echo("lookup", "the answer is 42");
        var chat = new ScriptedChat((_, n) => n == 1
            ? Asks(("c1", "lookup", Arg("life")))
            : Say(ChatRole.Assistant, "It is 42."));

        var result = await ToolLoop.RunAsync(chat, Start("what is it?"), [tool]);

        Assert.Equal("It is 42.", result.Text);
        Assert.Equal(2, result.ModelCalls);
        Assert.Equal(["life"], calls);
        // The second time the model is asked, it is shown its own request and the tool's result.
        Assert.Contains("[call lookup(input=life)]", chat.Shown(2));
        Assert.Contains("the answer is 42", chat.Shown(2));
    }

    [Fact(DisplayName = "Step 5 (cyclical graphs) — the cycle repeats: what one tool finds decides the next tool call")]
    public async Task Step05_TheCycleRepeats()
    {
        var (first, firstCalls) = Echo("find_order", "ORD-7");
        var (second, secondCalls) = Echo("get_status", "packed");
        var chat = new ScriptedChat((seen, n) => n switch
        {
            1 => Asks(("c1", "find_order", Arg("Ingrid"))),
            2 => Asks(("c2", "get_status", Arg("ORD-7"))),
            _ => Say(ChatRole.Assistant, "Ingrid's order is packed."),
        });

        var result = await ToolLoop.RunAsync(chat, Start("where is Ingrid's order?"), [first, second]);

        Assert.Equal(3, result.ModelCalls);
        Assert.Equal(["Ingrid"], firstCalls);
        Assert.Equal(["ORD-7"], secondCalls);
        Assert.Equal("Ingrid's order is packed.", result.Text);
    }

    [Fact(DisplayName = "Step 5 (tools) — several calls in one reply are all run, and answered together in one tool message, in order")]
    public async Task Step05_SeveralCallsAtOnce()
    {
        var (a, aCalls) = Echo("a", "alpha");
        var (b, bCalls) = Echo("b", "beta");
        var chat = new ScriptedChat((_, n) => n == 1
            ? Asks(("x1", "a", Arg("1")), ("x2", "b", Arg("2")))
            : Say(ChatRole.Assistant, "done"));

        var result = await ToolLoop.RunAsync(chat, Start("go"), [a, b]);

        Assert.Equal(["1"], aCalls);
        Assert.Equal(["2"], bCalls);
        var toolMessage = Assert.Single(result.NewMessages, m => m.Role == ChatRole.Tool);
        var results = toolMessage.Contents.OfType<FunctionResultContent>().ToList();
        Assert.Equal(["x1", "x2"], results.Select(r => r.CallId));
    }

    [Fact(DisplayName = "Step 5 (tools) — each result carries the id of the call it answers")]
    public async Task Step05_ResultsPointAtTheirCalls()
    {
        var (tool, _) = Echo("lookup", "found");
        var chat = new ScriptedChat((_, n) => n == 1 ? Asks(("call-77", "lookup", Arg("x"))) : Say(ChatRole.Assistant, "ok"));

        var result = await ToolLoop.RunAsync(chat, Start("q"), [tool]);

        var answer = Assert.Single(result.NewMessages.SelectMany(m => m.Contents).OfType<FunctionResultContent>());
        Assert.Equal("call-77", answer.CallId);
    }

    [Fact(DisplayName = "Step 5 (tools) — a tool that isn't there becomes a result the model can read, and the cycle carries on")]
    public async Task Step05_AnUnknownTool()
    {
        var chat = new ScriptedChat((_, n) => n == 1 ? Asks(("c1", "no_such_tool", Arg("x"))) : Say(ChatRole.Assistant, "Sorry, I can't."));

        var result = await ToolLoop.RunAsync(chat, Start("q"), []);

        Assert.Equal("Sorry, I can't.", result.Text);
        Assert.Contains("no_such_tool", chat.Shown(2));
    }

    [Fact(DisplayName = "Step 5 (tools) — a tool that throws becomes a result the model can read, and the cycle carries on")]
    public async Task Step05_AToolThatThrows()
    {
        var broken = AIFunctionFactory.Create((string input) => ThrowingTool(input), "broken", "Always fails.");
        var chat = new ScriptedChat((_, n) => n == 1 ? Asks(("c1", "broken", Arg("x"))) : Say(ChatRole.Assistant, "That didn't work."));

        var result = await ToolLoop.RunAsync(chat, Start("q"), [broken]);

        Assert.Equal("That didn't work.", result.Text);
        Assert.Contains("the shelf is on fire", chat.Shown(2));
    }

    [Fact(DisplayName = "Step 5 (function calling) — the model is offered the tools on every turn of the cycle")]
    public async Task Step05_ToolsAreOfferedEveryTime()
    {
        var (tool, _) = Echo("lookup", "found");
        var chat = new ScriptedChat((_, n) => n == 1 ? Asks(("c1", "lookup", Arg("x"))) : Say(ChatRole.Assistant, "ok"));

        await ToolLoop.RunAsync(chat, Start("q"), [tool]);

        Assert.Equal(["lookup"], chat.ToolsOffered(1));
        Assert.Equal(["lookup"], chat.ToolsOffered(2));
    }

    [Fact(DisplayName = "Step 5 (cyclical graphs) — the conversation list grows as the cycle runs, so the caller has the whole exchange")]
    public async Task Step05_TheMessagesGrow()
    {
        var (tool, _) = Echo("lookup", "found");
        var messages = Start("q");
        var chat = new ScriptedChat((_, n) => n == 1 ? Asks(("c1", "lookup", Arg("x"))) : Say(ChatRole.Assistant, "ok"));

        var result = await ToolLoop.RunAsync(chat, messages, [tool]);

        // user, assistant (call), tool (result), assistant (answer)
        Assert.Equal([ChatRole.User, ChatRole.Assistant, ChatRole.Tool, ChatRole.Assistant], messages.Select(m => m.Role));
        Assert.Equal(3, result.NewMessages.Count);
    }

    [Fact(DisplayName = "Step 5 (cyclical graphs) — a cycle with no end is stopped: it throws after maxTurns, having asked the model exactly that many times")]
    public async Task Step05_ACycleNeedsALimit()
    {
        var (tool, _) = Echo("lookup", "found");
        var chat = new ScriptedChat((_, n) => Asks(($"c{n}", "lookup", Arg("again"))));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ToolLoop.RunAsync(chat, Start("q"), [tool], maxTurns: 3));

        Assert.Equal(3, chat.CallCount);
        Assert.Contains("3", error.Message);
    }

    [Fact(DisplayName = "Step 5 (cyclical graphs) — an answer on the very last allowed turn is still an answer")]
    public async Task Step05_TheLastTurnCounts()
    {
        var (tool, _) = Echo("lookup", "found");
        var chat = new ScriptedChat((_, n) => n < 3 ? Asks(($"c{n}", "lookup", Arg("x"))) : Say(ChatRole.Assistant, "just in time"));

        var result = await ToolLoop.RunAsync(chat, Start("q"), [tool], maxTurns: 3);

        Assert.Equal("just in time", result.Text);
        Assert.Equal(3, result.ModelCalls);
    }
}
