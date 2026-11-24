using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using SantasWorkshop.Shared.Models;

namespace Part3B.Starter;

/// <summary>
/// The workshop's gift finder, now searching by <b>meaning</b>. Every method here is one step in the
/// Part 3B README, and one idea the Vector Search Fundamentals badge assesses.
/// </summary>
/// <remarks>
/// Replace each <c>throw new NotImplementedException</c> with real code (and fix the one that is
/// already written but wrong). Run <c>dotnet test part3b-vector-search/Tests</c> as you go — the tests
/// tell you when a step is right. Stuck? <c>SOLUTIONS.md</c> has the answers.
///
/// You do <b>not</b> need an API key for any of it. Every toy already has a vector, made by Voyage AI's
/// <c>voyage-3.5</c> model and shipped with the repository, and so do the example questions.
/// </remarks>
public sealed class GiftFinder(
    IMongoCollection<Toy> toys,
    VoyageEmbedder? embedder = null,
    string indexName = GiftFinder.DefaultIndex)
{
    /// <summary>What Step 4 calls its index.</summary>
    public const string DefaultIndex = "toys-vectors";

    /// <summary>Asks for a result row as <c>{ name, score }</c>.</summary>
    public const string HitProjection = "{ _id: 0, name: 1, score: { $meta: 'vectorSearchScore' } }";

    private readonly IMongoCollection<Toy> _toys = toys;
    private readonly VoyageEmbedder? _embedder = embedder;
    private readonly string _indexName = indexName;

    // ─────────────────────────────────────────────────────────────────────────
    //  Already written for you
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Waits until Atlas has finished building <paramref name="name"/> and will answer queries.
    /// </summary>
    /// <remarks>
    /// Creating a vector index <b>returns straight away</b>. Query it too soon and you get an empty
    /// result and no error. Don't use <c>Task.Delay</c> — ask the server. (Part 3A made the same point
    /// about search indexes.)
    /// </remarks>
    public async Task WaitUntilQueryableAsync(string name, TimeSpan? timeout = null)
    {
        var giveUpAt = DateTime.UtcNow + (timeout ?? TimeSpan.FromMinutes(3));

        while (DateTime.UtcNow < giveUpAt)
        {
            var indexes = await (await _toys.SearchIndexes.ListAsync(name)).ToListAsync();

            if (indexes.Count > 0 && indexes[0]["queryable"].AsBoolean)
                return;

            await Task.Delay(250);
        }

        throw new TimeoutException(
            $"The vector index '{name}' still isn't queryable. Is mongot running? Try: dotnet run --project tools/doctor");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Part A — What is a vector?
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 1 (badge objective 1) — how many numbers is a toy's vector, and how long is it?
    /// </summary>
    public Task<EmbeddingFacts> DescribeEmbeddingAsync(string toyName)
    {
        // TODO (Step 1): Find the toy called `toyName` (_toys.Find(t => t.Name == toyName)), take its
        // DescriptionEmbedding, and return:
        //   Dimensions — how many numbers are in it (the array's Length)
        //   Magnitude  — its length as an arrow: the square root of the sum of every number squared
        //
        // If there is no such toy, or it has no vector, throw an InvalidOperationException that says so.
        throw new NotImplementedException("Step 1: how many dimensions, and how long?");
    }

    /// <summary>
    /// Step 2 (badge objectives 1 and 4) — how alike are two vectors? Three ways to say it.
    /// </summary>
    /// <remarks>Pure maths: no database. Vectors of different lengths can't be compared.</remarks>
    public static SimilarityScores Compare(float[] a, float[] b)
    {
        // TODO (Step 2): Work out, over every position i:
        //   DotProduct        — the sum of a[i] * b[i]
        //   Cosine            — the dot product divided by (the magnitude of a times the magnitude of b)
        //   EuclideanDistance — the square root of the sum of (a[i] - b[i]) squared
        //
        // Use double for the sums: a 1024-number sum in float drifts in the last digits.
        // And if the two vectors are different lengths, throw an ArgumentException that says so.
        throw new NotImplementedException("Step 2: cosine, dot product and Euclidean distance, by hand.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Part B — Doing it the slow way, then the right way
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 3 (badge objective 2) — find the <paramref name="count"/> toys closest to a vector by
    /// comparing it with <b>every</b> toy, in C#. Best match first; the score is the cosine.
    /// </summary>
    public Task<List<ToyHit>> ClosestByHandAsync(float[] queryVector, int count)
    {
        // TODO (Step 3): Load every toy, use Compare(...).Cosine against each one's DescriptionEmbedding,
        // sort best-first, take `count`, and return them as ToyHit(name, cosine).
        //
        // It works. Now imagine a million toys: that is a million comparisons for one question, every
        // time. That is the problem the vector index in Step 4 exists to solve.
        throw new NotImplementedException("Step 3: brute force, in C#.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Part C — The vector index
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 4 (badge objectives 3 and 4) — create a vector search index over the toys' vectors,
    /// and wait until it is ready.
    /// </summary>
    /// <remarks>
    /// This one is already written. An elf wrote it, it runs, it reports <i>ready</i> — and nothing
    /// that queries it works. Something in the definition doesn't match the vectors. (The README
    /// shows you how to make the server tell you what.)
    /// </remarks>
    public async Task CreateVectorIndexAsync(string similarity = "cosine")
    {
        // BUG (Step 4): the index builds, and reports ready, and is wrong. Find the number that doesn't
        // match the data, and fix it. Nothing else here needs changing.
        //
        // (The elf copied this definition from a tutorial for a different embedding model.)
        var definition = new BsonDocument("fields", new BsonArray
        {
            new BsonDocument
            {
                { "type", "vector" },
                { "path", "descriptionEmbedding" },
                { "numDimensions", 1536 },
                { "similarity", similarity },
            },

            // Fields a query can narrow by BEFORE it searches (Step 6). Every one has to be declared.
            new BsonDocument { { "type", "filter" }, { "path", "minAge" } },
            new BsonDocument { { "type", "filter" }, { "path", "maxAge" } },
            new BsonDocument { { "type", "filter" }, { "path", "inStock" } },
            new BsonDocument { { "type", "filter" }, { "path", "category" } },
        });

        await _toys.SearchIndexes.CreateOneAsync(
            new CreateSearchIndexModel(_indexName, SearchIndexType.VectorSearch, definition));

        await WaitUntilQueryableAsync(_indexName);
    }

    /// <summary>
    /// Step 5 (badge objective 5) — your first <c>$vectorSearch</c>: the <paramref name="count"/> toys
    /// whose vectors are closest to <paramref name="queryVector"/>, best first.
    /// </summary>
    /// <remarks>
    /// Shell equivalent: <c>{ $vectorSearch: { index, path: "descriptionEmbedding", queryVector,
    /// numCandidates, limit } }</c>
    /// </remarks>
    public Task<List<ToyHit>> FindSimilarAsync(float[] queryVector, int count)
    {
        // TODO (Step 5): Aggregate().VectorSearch(...)
        //   field         t => t.DescriptionEmbedding
        //   query vector  new QueryVector(queryVector)
        //   limit         count
        //   options       new VectorSearchOptions<Toy> { IndexName = _indexName, NumberOfCandidates = ... }
        // then .Project<ToyHit>(HitProjection) and .ToListAsync().
        //
        // NumberOfCandidates is how many neighbours the search looks at before keeping the best `count`.
        // It can't be less than `count` and can't be more than 10,000. The docs suggest about 20 times
        // `count`: look at many, keep few.
        throw new NotImplementedException("Step 5: a $vectorSearch stage.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Part D — Narrowing the search
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 6 (badge objective 6) — like Step 5, but only toys that suit a child of
    /// <paramref name="age"/> and are in stock, narrowed <b>before</b> the search.
    /// </summary>
    public Task<List<ToyHit>> GiftsForAgeAsync(float[] queryVector, int age, int count)
    {
        // TODO (Step 6): Step 5 again, with a Filter in the options:
        //   Filter = Builders<Toy>.Filter.And(
        //       Builders<Toy>.Filter.Lte(t => t.MinAge, age),
        //       Builders<Toy>.Filter.Gte(t => t.MaxAge, age),
        //       Builders<Toy>.Filter.Eq(t => t.InStock, true))
        //
        // Every field in a filter must have been declared as a `filter` field in the index (it was, in Step 4).
        throw new NotImplementedException("Step 6: a $vectorSearch with a pre-filter.");
    }

    /// <summary>
    /// The <b>wrong</b> way to do Step 6, already written so you can compare: search first, filter
    /// afterwards. See what it returns.
    /// </summary>
    public async Task<List<ToyHit>> GiftsForAgePostFilteredAsync(float[] queryVector, int age, int count)
    {
        var closest = await FindSimilarAsync(queryVector, count);
        var suitable = (await _toys.Find(t => t.MinAge <= age && t.MaxAge >= age && t.InStock).ToListAsync())
            .Select(t => t.Name).ToHashSet();

        return closest.Where(h => suitable.Contains(h.Name)).ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Part E — From a question to a gift
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 8 (badge objective 2 and 5) — someone types a question. Find the gifts that answer it:
    /// embed the question, then search with the vector you get back.
    /// </summary>
    public Task<List<ToyHit>> FindGiftsAsync(string question, int? age, int count)
    {
        // TODO (Step 8): Three moves.
        //   1. Turn the question into a vector with the embedder — as a "query", not a "document":
        //          var vector = await _embedder!.EmbedAsync(question, "query");
        //   2. If `age` has a value, call GiftsForAgeAsync(vector, age.Value, count) ...
        //   3. ... otherwise FindSimilarAsync(vector, count).
        //
        // If there is no embedder, throw an InvalidOperationException that says why.
        throw new NotImplementedException("Step 8: embed the question, then search.");
    }
}

/// <summary>One result: the toy's name, and how close it was (0 to 1, higher is closer).</summary>
[BsonIgnoreExtraElements]
public sealed record ToyHit(
    [property: BsonElement("name")] string Name,
    [property: BsonElement("score")] double Score);

/// <summary>What Step 1 reports about a vector.</summary>
public sealed record EmbeddingFacts(int Dimensions, double Magnitude);

/// <summary>What Step 2 reports about a pair of vectors.</summary>
public sealed record SimilarityScores(double Cosine, double DotProduct, double EuclideanDistance);
