using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using SantasWorkshop.Shared.Models;

namespace Part4A.Starter;

/// <summary>A chunk the retriever found, and how close it was to the question.</summary>
public sealed class RetrievedChunk
{
    [BsonId] public required string Id { get; set; }
    [BsonElement("section")] public required string Section { get; set; }
    [BsonElement("audience")] public required string Audience { get; set; }
    [BsonElement("source")] public required string Source { get; set; }
    [BsonElement("chunkIndex")] public int ChunkIndex { get; set; }
    [BsonElement("text")] public required string Text { get; set; }

    /// <summary>The vector search score, between 0 and 1. Higher is closer.</summary>
    [BsonElement("score")] public double Score { get; set; }
}

/// <summary>Steps 5 and 6: the vector index over the chunks, and a retriever that can narrow by section and audience first.</summary>
public sealed class Retriever(IMongoCollection<HandbookChunk> chunks, string indexName = Retriever.DefaultIndex)
{
    /// <summary>What Step 5 calls its index.</summary>
    public const string DefaultIndex = "handbook-vectors";

    /// <summary>The embedding model's number of dimensions. Part 3B's trap, again: the index must say the same.</summary>
    public const int Dimensions = 1024;

    /// <summary>Asks for a result row as the chunk's fields, without its vector, plus the score.</summary>
    public const string HitProjection =
        "{ _id: 1, section: 1, audience: 1, source: 1, chunkIndex: 1, text: 1, score: { $meta: 'vectorSearchScore' } }";

    private readonly IMongoCollection<HandbookChunk> _chunks = chunks;
    private readonly string _indexName = indexName;

    // ─────────────────────────────────────────────────────────────────────────
    //  Already written for you
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Waits until Atlas has finished building <paramref name="name"/> and will answer queries. Creating an index
    /// returns straight away; query it too soon and you get nothing, and no error. (Parts 3A and 3B said so too.)
    /// </summary>
    public async Task WaitUntilQueryableAsync(string name, TimeSpan? timeout = null)
    {
        var giveUpAt = DateTime.UtcNow + (timeout ?? TimeSpan.FromMinutes(3));

        while (DateTime.UtcNow < giveUpAt)
        {
            var indexes = await (await _chunks.SearchIndexes.ListAsync(name)).ToListAsync();
            if (indexes.Count > 0 && indexes[0]["queryable"].AsBoolean) return;
            await Task.Delay(250);
        }

        throw new TimeoutException($"The vector index '{name}' still isn't queryable. Is mongot running? Try: dotnet run --project tools/doctor");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Step 5 — the index
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 5 (badge objective: a prefiltered retriever, part 1) — create a vector index named <paramref name="name"/>
    /// over the chunks' <c>embedding</c> field, which also declares <c>section</c> and <c>audience</c> as
    /// <c>filter</c> fields.
    /// </summary>
public async Task CreateIndexAsync(string name)
    {
        // TODO (Step 5): CreateOneAsync with a CreateSearchIndexModel of type SearchIndexType.VectorSearch.
        // Its definition has a "fields" array with three entries:
        //   { type: "vector", path: "embedding", numDimensions: Dimensions, similarity: "cosine" }
        //   { type: "filter", path: "section" }
        //   { type: "filter", path: "audience" }
        //
        // Declare a field as a filter and a query can narrow by it BEFORE the search; leave it out and the query
        // fails with  Path 'section' needs to be indexed as filter.  You pay for each one at index time.
        //
        // Then wait for it with WaitUntilQueryableAsync(name), because creating returns straight away.
        throw new NotImplementedException("Step 5: create the vector index, with filter fields.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Step 6 — the retriever
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 6 (badge objective: a prefiltered retriever) — find the <paramref name="k"/> chunks closest to
    /// <paramref name="queryVector"/>, looking <b>only</b> at chunks that match <paramref name="section"/> and
    /// <paramref name="audience"/> when they are given. Best first.
    /// </summary>
public async Task<List<RetrievedChunk>> RetrieveAsync(float[] queryVector, int k, string? section = null, string? audience = null)
    {
        // TODO (Step 6):
        //   - Build a filter from whichever of section / audience was given: an EQUALITY on each, ANDed together.
        //     (Builders<HandbookChunk>.Filter.Eq(c => c.Section, section) ...). Given neither, there is NO filter.
        //   - _chunks.Aggregate().VectorSearch(c => c.Embedding, new QueryVector(queryVector), k, options)
        //     with options { IndexName = _indexName, NumberOfCandidates = Math.Min(k * 20, 10_000), Filter = filter }
        //   - then .Project<RetrievedChunk>(HitProjection) and .ToListAsync().
        //
        // The filter goes INSIDE the vector search (a pre-filter), not after it: Part 3B showed what a
        // post-filter does when the nearest chunks are all the wrong kind.
        throw new NotImplementedException("Step 6: search, narrowed first.");
    }
}
