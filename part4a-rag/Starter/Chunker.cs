namespace Part4A.Starter;

/// <summary>Step 3: cut a long text into chunks that overlap.</summary>
/// <remarks>
/// <para>A vector can only stand for so much. Embed a whole section and it blurs into the average of everything in it;
/// embed a few sentences and it is about <i>one thing</i>. So the handbook is cut into chunks.</para>
/// <para>But a cut can land in the middle of an idea, and a chunk that begins halfway through one is hard to find
/// and hard to read. <b>Overlap</b> repeats the end of one chunk at the start of the next, so an idea that straddles a cut
/// is whole in at least one of them.</para>
/// </remarks>
public static class Chunker
{
    /// <summary>
    /// Step 3 (badge objective: chunk overlap) — split <paramref name="text"/> into chunks of at most
    /// <paramref name="size"/> <b>words</b>, where each chunk starts <paramref name="overlap"/> words before the
    /// previous one ended.
    /// </summary>
    /// <remarks>
    /// With <c>size: 5, overlap: 2</c>, the text <c>a b c d e f g h i j</c> becomes
    /// <c>a b c d e</c>, <c>d e f g h</c>, <c>g h i j</c> — each chunk starts 3 words (size − overlap) after the last one started.
    /// </remarks>
public static List<string> SplitIntoChunks(string text, int size, int overlap)
    {
        // TODO (Step 3):
        //   - Split the text into words (on any whitespace, ignoring empty entries).
        //   - Slide a window of `size` words forward by (size - overlap) words each time.
        //   - Join each window's words back together with single spaces.
        //   - Stop as soon as a window reaches the last word. Do NOT emit a final chunk that is nothing but
        //     words the previous chunk already contained.
        //
        // And guard the edges, with ArgumentOutOfRangeException:
        //   size must be at least 1, and overlap must be at least 0 and LESS than size.
        //   (overlap >= size would never move forward: an infinite loop.)
        // Empty or whitespace-only text has no chunks: return an empty list.
        throw new NotImplementedException("Step 3: split text into overlapping chunks.");
    }
}
