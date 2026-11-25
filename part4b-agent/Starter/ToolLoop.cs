using Microsoft.Extensions.AI;

namespace Part4B.Starter;

/// <summary>What the loop ended with: the answer, how many times it asked the model, and every message it added along the way.</summary>
public sealed record ToolLoopResult(string Text, int ModelCalls, IReadOnlyList<ChatMessage> NewMessages);

/// <summary>
/// Step 5: the heart of an agent is a <b>cycle</b>. Ask the model; if it wants a tool, run the tool and ask again with
/// the result; repeat until it gives an answer instead of a request.
/// </summary>
/// <remarks>
/// <para>Draw it and it is a graph with a loop in it: a <b>model</b> node, a <b>tools</b> node, an edge from the model to the tools
/// when it asks for one, and an edge <i>back</i> to the model afterwards. An edge out of the model to the end, when it just answers.
/// That back-edge is what "cyclical graph" means, and it is why an agent can do something a single prompt can't: use what it
/// found to decide what to do next.</para>
/// <para>You are writing the cycle by hand once, so you can see it. Step 6 hands it to Agent Framework, which does exactly this.</para>
/// </remarks>
public static class ToolLoop
{
    /// <summary>The most times the model may be asked before the loop gives up.</summary>
    public const int DefaultMaxTurns = 8;

    /// <summary>
    /// Step 5 (badge objectives: cyclical graphs; tools and function calling) — run the model-and-tools cycle starting from
    /// <paramref name="messages"/>.
    /// </summary>
    public static async Task<ToolLoopResult> RunAsync(
        IChatClient chat,
        IList<ChatMessage> messages,
        IList<AITool> tools,
        int maxTurns = DefaultMaxTurns,
        CancellationToken cancellationToken = default)
    {
        // TODO (Step 5): repeat, at most maxTurns times:
        //
        //   1. ask the model:  chat.GetResponseAsync(messages, new ChatOptions { Tools = tools }, cancellationToken)
        //      and add everything it returned (response.Messages) to `messages`, and to a list of new messages.
        //   2. look for FunctionCallContent in what it returned (response.Messages[..].Contents).
        //      NONE -> it has answered: return new ToolLoopResult(response.Text, <model calls so far>, <new messages>).
        //   3. for each call, find the AIFunction in `tools` with the same Name and run it:
        //          await function.InvokeAsync(new AIFunctionArguments(call.Arguments), cancellationToken)
        //      and wrap what comes back as  new FunctionResultContent(call.CallId, result).
        //        - A name that isn't one of your tools: don't throw. Give the model a result that says so, so it can recover.
        //        - A tool that throws: same. Give the model the error's message as the result. (A model can read a result;
        //          it can't read an exception.)
        //   4. add ONE message with role ChatRole.Tool holding all of the results, to `messages` and to the new messages.
        //
        // And the guard that every cycle needs: if the model is still asking for tools after maxTurns calls, stop and throw an
        // InvalidOperationException that says so. A loop with no limit is a bill you didn't agree to.
        throw new NotImplementedException("Step 5: the model-and-tools cycle.");
    }
}
