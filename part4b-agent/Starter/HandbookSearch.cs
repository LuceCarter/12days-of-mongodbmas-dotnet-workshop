using Microsoft.Agents.AI;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using SantasWorkshop.Shared.Models;

namespace Part4B.Starter;

/// <summary>
/// Searches the Elf Handbook by meaning. <b>Already written for you</b>: it is the retriever you built in Part 4A
/// (embed the question, <c>$vectorSearch</c> the chunks, keep what clears the relevance bar), packaged as the single
/// function an agent's retrieval tool needs.
/// </summary>
/// <remarks>
/// It needs the handbook's chunks in a collection with a vector index. <c>tools/seed -- --handbook</c> loads the
/// chunks, and Part 4A's Step 5 builds the index. The tests build their own, so you don't need either to do the steps.
/// </remarks>
public sealed class HandbookSearch(
    IMongoCollection<HandbookChunk> chunks,
    IEmbedder embedder,
    string indexName = HandbookSearch.DefaultIndex,
    double minScore = HandbookSearch.DefaultMinScore,
    int k = 4)
{
    public const string DefaultIndex = "handbook-vectors";

    /// <summary>The relevance bar Part 4A measured for these vectors: below it, a chunk is merely the nearest of a bad lot.</summary>
    public const double DefaultMinScore = 0.67;

    // Only what the agent needs: the passage, where it came from, and how close it was. (Ask for more fields than the
    // class below has and the driver refuses to read the row.)
    private const string HitProjection = "{ _id: 1, source: 1, text: 1, score: { $meta: 'vectorSearchScore' } }";

    private sealed class Hit
    {
        [BsonId] public string Id { get; set; } = "";
        [BsonElement("source")] public string Source { get; set; } = "";
        [BsonElement("text")] public string Text { get; set; } = "";
        [BsonElement("score")] public double Score { get; set; }
    }

    /// <summary>The handbook passages that answer <paramref name="question"/>, best first. Empty if nothing clears the bar.</summary>
    public async Task<List<TextSearchProvider.TextSearchResult>> SearchAsync(string question, CancellationToken cancellationToken = default)
    {
        var vector = await embedder.EmbedAsync(question, "query", cancellationToken);

        var options = new VectorSearchOptions<HandbookChunk>
        {
            IndexName = indexName,
            NumberOfCandidates = Math.Min(k * 20, 10_000),
        };

        var hits = await chunks.Aggregate()
            .VectorSearch(c => c.Embedding, new QueryVector(vector), k, options)
            .Project<Hit>(HitProjection)
            .ToListAsync(cancellationToken);

        return hits
            .Where(h => h.Score >= minScore)
            .Select(h => new TextSearchProvider.TextSearchResult { SourceName = h.Source, Text = h.Text })
            .ToList();
    }

    /// <summary>The shape Agent Framework's <see cref="TextSearchProvider"/> asks for: a function from a question to some text.</summary>
    public Func<string, CancellationToken, Task<IEnumerable<TextSearchProvider.TextSearchResult>>> AsTextSearch() =>
        async (question, cancellationToken) => await SearchAsync(question, cancellationToken);
}
