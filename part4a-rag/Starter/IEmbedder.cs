namespace Part4A.Starter;

/// <summary>
/// Turns text into a vector. <see cref="VoyageEmbedder"/> is the real one; the tests use a stand-in.
/// </summary>
/// <remarks>
/// This is the <b>embedding model</b>. It is deliberately a different thing from the <b>generator</b>
/// (<c>IChatClient</c>) that writes the answer: see <see cref="ElfAssistant"/>.
/// </remarks>
public interface IEmbedder
{
    /// <param name="text">What to embed.</param>
    /// <param name="inputType"><c>"document"</c> for text you are storing, <c>"query"</c> for a question you are searching with.</param>
    Task<float[]> EmbedAsync(string text, string inputType, CancellationToken cancellationToken = default);
}
