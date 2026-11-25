using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Part4A.Starter;

/// <summary>Where an answer came from.</summary>
public sealed record Citation(string Source, string ChunkId);

/// <summary>What the assistant says, and whether the handbook backed it.</summary>
/// <param name="Text">The answer.</param>
/// <param name="Sources">The chunks the answer was built from, best first. Empty when nothing relevant was found.</param>
/// <param name="Grounded"><c>false</c> when the handbook had nothing relevant and the model was not asked.</param>
public sealed record ElfAnswer(string Text, IReadOnlyList<Citation> Sources, bool Grounded);

/// <summary>
/// Steps 8 and 9: the whole pipeline. A question comes in, is embedded, finds chunks, becomes a prompt, and a
/// language model writes the answer.
/// </summary>
/// <remarks>
/// Look at the constructor: <b>two different models</b>. The <see cref="IEmbedder"/> turns text into vectors so the
/// retriever can <i>find</i>; the <see cref="IChatClient"/> writes the answer. They are separate on purpose.
/// </remarks>
public sealed class ElfAssistant(
    Retriever retriever,
    IEmbedder embedder,
    IChatClient generator,
    double minScore = ElfAssistant.DefaultMinScore)
{
    /// <summary>
    /// A chunk scoring below this isn't "relevant", it's merely the closest of a bad lot. A vector search always
    /// returns its nearest neighbours, even when nothing is near, so something has to say "not close enough".
    /// 0.67 sits between the best an unrelated question scored (0.627) and the weakest answerable one (0.717) against these vectors.
    /// It is a measurement of THIS model and THIS data, not a constant of nature.
    /// </summary>
    public const double DefaultMinScore = 0.67;

    /// <summary>What it says when the handbook has nothing relevant. The model isn't asked.</summary>
    public const string NoAnswer =
        "I couldn't find that in the Elf Handbook, so I'd rather not guess. Try rephrasing it, or ask the Head of Letters.";

    private readonly Retriever _retriever = retriever;
    private readonly IEmbedder _embedder = embedder;
    private readonly IChatClient _generator = generator;
    private readonly double _minScore = minScore;

    /// <summary>
    /// Step 8 (badge objectives: the architecture's components; a generator separate from the embedding model) —
    /// answer <paramref name="question"/> from the handbook.
    /// </summary>
public async Task<ElfAnswer> AskAsync(string question, string? section = null, string? audience = null, int k = 4)
    {
        // TODO (Step 8):
        //   1. embed the question as a "query" (_embedder);
        //   2. retrieve k chunks with your Retriever, narrowed by section / audience if given;
        //   3. keep only the chunks whose Score is at least _minScore;
        //   4. if NONE are left, return new ElfAnswer(NoAnswer, [], Grounded: false) WITHOUT calling the model;
        //   5. otherwise build the messages with GroundedPrompt.BuildPrompt, ask _generator.GetResponseAsync(...),
        //      and return the model's text with a Citation (Source, Id) for each chunk it was given, Grounded: true.
        throw new NotImplementedException("Step 8: the whole pipeline.");
    }

    /// <summary>
    /// Step 9 (badge objective: a retrieval tool) — the same retriever, shaped the way Microsoft Agent Framework
    /// asks for one: a function from a question to a few <see cref="TextSearchProvider.TextSearchResult"/>s.
    /// </summary>
    /// <remarks>
    /// Part 4B hands this to a <c>TextSearchProvider</c>, and the agent decides <i>for itself</i> when to call it.
    /// </remarks>
public Func<string, CancellationToken, Task<IEnumerable<TextSearchProvider.TextSearchResult>>> AsTextSearch(
        int k = 4, string? section = null, string? audience = null)
    {
        // TODO (Step 9): return a function that, given the question:
        //   embeds it as a "query", retrieves k chunks (narrowed by section / audience), and returns each as a
        //   TextSearchResult with  SourceName = the chunk's Source  and  Text = the chunk's text.
        //   Leave out chunks scoring below _minScore, as AskAsync does.
        throw new NotImplementedException("Step 9: a retrieval tool for an agent.");
    }
}
