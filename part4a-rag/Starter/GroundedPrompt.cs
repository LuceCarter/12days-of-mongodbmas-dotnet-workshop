using Microsoft.Extensions.AI;

namespace Part4A.Starter;

/// <summary>Step 7: the prompt that turns a language model into a reader of <i>your</i> documents.</summary>
/// <remarks>
/// <para>A language model knows what it was trained on, up to a date, and nothing about your handbook. Ask it about
/// the brass test tube by the sanding bench and it will, helpfully and with total confidence, invent one. Those are
/// the limits RAG exists for: <b>knowledge it never had</b>, <b>knowledge that has changed</b>, and <b>answers
/// it can't point to a source for</b>.</para>
/// <para>RAG's answer is to put the right text <i>in the prompt</i> and tell the model to use only that.</para>
/// </remarks>
public static class GroundedPrompt
{
    /// <summary>
    /// Step 7 (badge objectives: what RAG is; the LLM limits it addresses) — build the messages to send to the
    /// generator: a system message that sets the rules, then a user message that holds the retrieved chunks and the question.
    /// </summary>
public static List<ChatMessage> BuildPrompt(string question, IReadOnlyList<RetrievedChunk> chunks)
    {
        // TODO (Step 7): return exactly two messages.
        //
        //   1. ChatRole.System — the rules. It should tell the model to
        //        - answer using ONLY the handbook extracts it is given,
        //        - say that it doesn't know, rather than guess, when the extracts don't contain the answer,
        //        - name the source of what it uses (the extract's source, e.g. "The Elf Handbook › Toy Safety").
        //
        //   2. ChatRole.User — the extracts, in the order you were given them (best first), each with its source
        //      and its text, and THEN the question, last. (Models pay most attention to the end of a prompt.)
        //
        // Nothing here calls a model. It only builds the text, which is why you can test it exactly.
        throw new NotImplementedException("Step 7: build the grounded prompt.");
    }
}
