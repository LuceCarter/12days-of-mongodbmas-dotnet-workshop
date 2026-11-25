using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Part4B.Starter;

/// <summary>
/// Step 6: the same agent, built with Microsoft Agent Framework. It runs the cycle you wrote in Step 5, and adds what a real
/// agent needs around it: instructions, sessions, memory, and context providers.
/// </summary>
public static class ElfAgent
{
    /// <summary>
    /// Step 6 (badge objectives: retrieval tools; an agent's reasoning, tools and memory) — build the Elf Assistant.
    /// </summary>
    /// <param name="chat">The language model.</param>
    /// <param name="tools">The tools from Step 4.</param>
    /// <param name="handbook">The handbook search (<see cref="HandbookSearch.AsTextSearch"/>), or <c>null</c> for an agent with no handbook.</param>
    /// <param name="history">Where the conversation is remembered (Step 9). <c>null</c> means in memory only.</param>
    public static ChatClientAgent CreateAgent(
        IChatClient chat,
        IList<AITool> tools,
        Func<string, CancellationToken, Task<IEnumerable<TextSearchProvider.TextSearchResult>>>? handbook = null,
        ChatHistoryProvider? history = null)
    {
        // TODO (Step 6): build the agent with chat.AsAIAgent(new ChatClientAgentOptions { ... }) where
        //
        //   Name                    "Elf Assistant"
        //   ChatOptions             new ChatOptions { Instructions = <your Step 1 text>, Tools = tools }
        //   ChatHistoryProvider     history
        //   AIContextProviders      when `handbook` is not null, ONE TextSearchProvider made from it, with options
        //                             SearchTime            = TextSearchProviderOptions.TextSearchBehavior.OnDemandFunctionCalling
        //                             FunctionToolName      = ElfAgentInstructions.HandbookTool
        //                             FunctionToolDescription = something a model can use to decide when to search
        //
        // "On demand" is the difference between RAG and an agent. Part 4A's RAG searched the handbook on EVERY question and put the
        // result in the prompt. Here the search is offered as a TOOL, and the model decides whether it needs it: it won't search
        // the handbook to find out what ORD-1001 is.
        //
        // The instructions must name every tool the agent has: the ones in `tools`, plus the handbook tool when there is one.
        throw new NotImplementedException("Step 6: build the agent.");
    }
}
