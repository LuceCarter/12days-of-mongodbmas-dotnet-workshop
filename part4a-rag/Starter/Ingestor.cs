using MongoDB.Driver;
using SantasWorkshop.Shared.Models;

namespace Part4A.Starter;

/// <summary>Step 4: ingestion. Chunk each section, embed each chunk, and store it with what it came from.</summary>
public sealed class Ingestor(
    IMongoCollection<HandbookChunk> chunks,
    IEmbedder embedder,
    int chunkWords = Ingestor.DefaultChunkWords,
    int overlapWords = Ingestor.DefaultOverlapWords)
{
    /// <summary>How many words go in a chunk. The shipped vectors were made with 80.</summary>
    public const int DefaultChunkWords = 80;

    /// <summary>How many words one chunk shares with the next. The shipped vectors were made with 20.</summary>
    public const int DefaultOverlapWords = 20;

    private readonly IMongoCollection<HandbookChunk> _chunks = chunks;
    private readonly IEmbedder _embedder = embedder;
    private readonly int _chunkWords = chunkWords;
    private readonly int _overlapWords = overlapWords;

    /// <summary>The id of a chunk: the section's name in lower case with hyphens, then its position: <c>toy-safety-0</c>.</summary>
    public static string ChunkId(string section, int index) =>
        $"{string.Join('-', section.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))}-{index}";

    /// <summary>
    /// Step 4 (badge objective: data collection, preparation and ingestion) — store every chunk of
    /// <paramref name="sections"/>, embedded. Returns how many chunks it stored.
    /// </summary>
public async Task<int> IngestAsync(IReadOnlyList<HandbookSection> sections)
    {
        // TODO (Step 4): for each section
        //   1. cut its Body into chunks with your Chunker (_chunkWords, _overlapWords);
        //   2. embed each chunk's text as a "document" (that is what the model expects for text you are STORING);
        //   3. make a HandbookChunk with
        //        Id          ChunkId(section.Section, index)
        //        Section     the section's heading
        //        Audience    the section's audience
        //        Source      $"The Elf Handbook › {section.Section}"   (what the answer will cite)
        //        ChunkIndex  its position in the section, from 0
        //        Text        the chunk's text
        //        Embedding   the vector
        //   4. store them.
        //
        // And make it safe to run twice AND safe to run after the handbook changed. Ingesting the same sections again
        // must leave ONE copy of each chunk, not two (a chunk's Id is stable, so a replace-by-id does that). And if a section
        // got shorter, its old last chunk must go: otherwise your assistant keeps quoting text that was deleted.
        throw new NotImplementedException("Step 4: embed the chunks and store them.");
    }
}
