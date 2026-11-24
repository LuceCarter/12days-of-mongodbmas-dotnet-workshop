using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Search;
using SantasWorkshop.Shared.Models;

namespace Part3A.Starter;

/// <summary>
/// The workshop's gift finder. Every method here is one step in the Part 3A README, and one
/// idea the Search Fundamentals badge assesses.
/// </summary>
/// <remarks>
/// Replace each <c>throw new NotImplementedException</c> with a real index definition or search
/// query (and fix the one that is already written but wrong). Run
/// <c>dotnet test part3a-search/Tests</c> as you go — the tests tell you when a step is right.
/// Stuck? <c>SOLUTIONS.md</c> has the answers.
///
/// Two kinds of thing live here. <b>Index definitions</b> say what Atlas Search should be able to
/// find; <b>queries</b> use the <c>$search</c> stage to find it. Queries are written with the
/// driver's typed builders, which turn into the same <c>$search</c> JSON you see in the shell:
/// <code>
/// var hits = await _toys.Aggregate()
///     .Search(Builders&lt;Toy&gt;.Search.Text(t =&gt; t.Description, "dinosaur"), indexName: "toys-dynamic")
///     .Project&lt;ToyHit&gt;("{ _id: 0, name: 1, score: { $meta: 'searchScore' } }")
///     .ToListAsync();
/// </code>
/// </remarks>
public sealed class ToySearch(
    IMongoCollection<Toy> toys,
    string dynamicIndex = ToySearch.DefaultDynamicIndex,
    string staticIndex = ToySearch.DefaultStaticIndex)
{
    /// <summary>What Step 1 calls its index.</summary>
    public const string DefaultDynamicIndex = "toys-dynamic";

    /// <summary>What Step 5 calls its index. Steps 6 to 10 all search this one.</summary>
    public const string DefaultStaticIndex = "toys-static";

    /// <summary>Asks Atlas Search to build a result row as <c>{ name, score }</c>.</summary>
    public const string HitProjection = "{ _id: 0, name: 1, score: { $meta: 'searchScore' } }";

    private readonly IMongoCollection<Toy> _toys = toys;
    private readonly string _dynamicIndex = dynamicIndex;
    private readonly string _staticIndex = staticIndex;

    // ─────────────────────────────────────────────────────────────────────────
    //  Already written for you
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Waits until Atlas Search has finished building <paramref name="indexName"/> and will
    /// answer queries.
    /// </summary>
    /// <remarks>
    /// Creating a search index <b>returns straight away</b>, long before the index is ready.
    /// Query it too soon and you get an empty result and <i>no error at all</i>. So: create,
    /// wait, then query. On the local database this takes a fraction of a second; on an Atlas
    /// free cluster it takes about a minute. Don't use <c>Task.Delay</c> — ask the server.
    /// </remarks>
    public async Task WaitUntilQueryableAsync(string indexName, TimeSpan? timeout = null)
    {
        var giveUpAt = DateTime.UtcNow + (timeout ?? TimeSpan.FromMinutes(3));

        while (DateTime.UtcNow < giveUpAt)
        {
            var indexes = await (await _toys.SearchIndexes.ListAsync(indexName)).ToListAsync();

            if (indexes.Count > 0 && indexes[0]["queryable"].AsBoolean)
                return;

            await Task.Delay(250);
        }

        throw new TimeoutException(
            $"The search index '{indexName}' still isn't queryable. " +
            "Is mongot running? Try: dotnet run --project tools/doctor");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Part A — Your first search index
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 1 (badge objective 1) — create a search index that maps every field automatically,
    /// and wait until it is ready.
    /// </summary>
    /// <remarks>
    /// Shell equivalent:
    /// <c>db.toys.createSearchIndex("toys-dynamic", { mappings: { dynamic: true } })</c>
    /// </remarks>
    public Task CreateDynamicIndexAsync()
    {
        // TODO (Step 1): Three things, in this order.
        //   1. Build the definition:  { mappings: { dynamic: true } }
        //        new BsonDocument("mappings", new BsonDocument("dynamic", true))
        //   2. Create the index, named _dynamicIndex:
        //        await _toys.SearchIndexes.CreateOneAsync(new CreateSearchIndexModel(_dynamicIndex, definition));
        //   3. Wait for it:  await WaitUntilQueryableAsync(_dynamicIndex);
        //
        // (Make the method async — it is only `Task` here so the stub compiles.)
        throw new NotImplementedException("Step 1: create the dynamic search index, then wait for it.");
    }

    /// <summary>
    /// Step 2 (badge objectives 1 and 5) — your first <c>$search</c>: toys whose
    /// <b>description</b> contains <paramref name="words"/>, best match first, using the dynamic index.
    /// </summary>
    /// <remarks>
    /// Shell equivalent: <c>db.toys.aggregate([ { $search: { index: "toys-dynamic",
    /// text: { query: "dinosaur", path: "description" } } }, { $project: ... } ])</c>
    /// </remarks>
    public Task<List<ToyHit>> SearchDescriptionsAsync(string words)
    {
        // TODO (Step 2): Aggregate().Search(...) with a text operator on the description.
        //   Builders<Toy>.Search.Text(t => t.Description, words)
        //   pass  indexName: _dynamicIndex   (or it looks for an index called "default")
        //   then  .Project<ToyHit>(HitProjection)  and  .ToListAsync()
        //
        // Atlas Search returns the best match FIRST. You don't sort anything.
        throw new NotImplementedException("Step 2: a $search with a text operator.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Part B — Dynamic or static?
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 3 (badge objective 3) — what did this index actually map?
    /// </summary>
    /// <remarks>
    /// Shell equivalent: <c>db.toys.getSearchIndexes("toys-dynamic")</c>
    /// </remarks>
    public Task<IndexSummary> DescribeIndexAsync(string indexName)
    {
        // TODO (Step 3): List the index with  _toys.SearchIndexes.ListAsync(indexName)  and read
        // the first document it returns:
        //   "queryable"                             → Queryable
        //   "latestDefinition" → "mappings" → "dynamic"   → Dynamic  (true or false)
        //   "latestDefinition" → "mappings" → "fields"    → the NAMES of the mapped fields
        //                                                    (a dynamic index has no "fields" at all)
        //
        // Return  new IndexSummary(indexName, queryable, dynamic, fieldNames).
        throw new NotImplementedException("Step 3: read an index's definition back.");
    }

    /// <summary>
    /// Step 4 (badge objective 3) — every toy in <paramref name="category"/>, found with an exact
    /// match, using whichever index you name.
    /// </summary>
    /// <remarks>
    /// Run it against the dynamic index, then the static one (Step 5) and compare. Shell
    /// equivalent: <c>{ $search: { index, equals: { path: "category", value: "puzzle" } } }</c>
    /// </remarks>
    public Task<List<ToyHit>> FindInCategoryAsync(string category, string indexName)
    {
        // TODO (Step 4): An `equals` operator on the category, against the index passed in.
        //   Builders<Toy>.Search.Equals(t => t.Category, category)
        //   .Search(..., indexName: indexName)
        throw new NotImplementedException("Step 4: a $search with an equals operator.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Part C — A static mapping, and the types it can hold
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 5 (badge objective 4) — create an index that maps exactly the fields in the README's
    /// plan, with the right type for each, and wait until it is ready.
    /// </summary>
    public Task CreateStaticIndexAsync()
    {
        // TODO (Step 5): Same three moves as Step 1 — definition, CreateOneAsync, wait — but name the
        // index _staticIndex and turn dynamic OFF, listing fields one by one. The README's plan
        // table says which type each field gets. The shape is:
        //
        //   { mappings: { dynamic: false, fields: {
        //       name:        [ { type: "string" }, { type: "autocomplete" } ],   // two types on ONE field
        //       description: { type: "string", analyzer: "lucene.english" },
        //       ...
        //   } } }
        //
        // BsonDocument.Parse("""{ ... }""") takes the JSON exactly as the shell and Atlas show it.
        throw new NotImplementedException("Step 5: a static mapping with string, token, number and boolean fields.");
    }

    /// <summary>
    /// Step 6 (badge objective 4) — toys matching <paramref name="words"/> that cost
    /// <paramref name="maxPrice"/> or less.
    /// </summary>
    /// <remarks>
    /// This one is already written. An elf wrote it and went home, and it comes back empty. The
    /// price really is mapped in the index, and the query really is a <c>range</c> over it. Something
    /// about the <b>type</b> of <c>price</c> is not right. (The README has a ⚠️ box about exactly this.)
    /// </remarks>
    public async Task<List<ToyHit>> ToysUnderPriceAsync(string words, decimal maxPrice)
    {
        // BUG (Step 6): this finds nothing, and doesn't say why. Why can't Atlas Search see the
        // price? Don't change the text operator or the index. Change WHERE the price is checked.
        //
        // (The elf's first try passed `maxPrice` straight in, and the server refused it:
        //  "range.lte Type is not supported: DECIMAL128". So the elf cast it to a double. That made the
        //  error go away. It did not make the problem go away. Hint: that error was the helpful one.)
        var search = Builders<Toy>.Search;

        var query = search.Compound()
            .Must(search.Text(new[] { "name", "description", "tags" }, words))
            .Filter(search.Range("price", SearchRangeBuilder.Lte((double)maxPrice)));

        return await _toys.Aggregate()
            .Search(query, indexName: _staticIndex)
            .Project<ToyHit>(HitProjection)
            .ToListAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Part D — Real queries, ranked
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 7 (badge objective 5) — gift ideas for a child of <paramref name="age"/> matching
    /// <paramref name="words"/>: in stock, not discontinued, and suitable for that age.
    /// </summary>
    public Task<List<ToyHit>> GiftIdeasAsync(string words, int age)
    {
        // TODO (Step 7): A compound operator — Builders<Toy>.Search.Compound() — with four clauses:
        //   .Must(...)     the text must match in name, description or tags   (this one SCORES)
        //   .Filter(...)   inStock is true                                    (filters don't score)
        //   .Filter(...)   minAge <= age  and  maxAge >= age                  (two range operators)
        //   .MustNot(...)  discontinued is true
        //
        // Search.Text accepts several paths:  search.Text(new[] { "name", "description", "tags" }, words)
        // Search.Equals(t => t.InStock, true)
        // Search.Range(t => t.MinAge, SearchRangeBuilder.Lte(age))    // ints are fine — unlike price!
        throw new NotImplementedException("Step 7: a compound search with must, filter and mustNot.");
    }

    /// <summary>
    /// Step 8 (badge objective 2) — the same words as Step 7's, but a match in the toy's
    /// <b>name</b> counts five times as much as a match anywhere else.
    /// </summary>
    public Task<List<ToyHit>> RankedAsync(string words)
    {
        // TODO (Step 8): Compound again, but with two `Should` clauses (and MinimumShouldMatch(1)):
        //   a text search on "name" with its score boosted:
        //       search.Text("name", words, new SearchTextOptions<Toy> { Score = new SearchScoreDefinitionBuilder<Toy>().Boost(5) })
        //   a text search on "description" and "tags", unboosted.
        //
        // Results come back best-first. The score each toy got comes back in ToyHit.Score.
        throw new NotImplementedException("Step 8: boost a field with a score option.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Part E — Typos and as-you-type
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 9 (badge objective 5) — find toys even when <paramref name="words"/> has a typo in it.
    /// </summary>
    public Task<List<ToyHit>> FuzzySearchAsync(string words)
    {
        // TODO (Step 9): A text search over name, description and tags with fuzzy matching switched on:
        //   search.Text(paths, words, new SearchTextOptions<Toy> { Fuzzy = new SearchFuzzyOptions { MaxEdits = 2 } })
        //
        // MaxEdits is how many single-letter changes a word may be away from a real one (1 or 2).
        throw new NotImplementedException("Step 9: a text operator with fuzzy matching.");
    }

    /// <summary>
    /// Step 10 (badge objective 5) — complete what the user has typed so far:
    /// up to <paramref name="limit"/> toy names that have a word starting with <paramref name="prefix"/>.
    /// </summary>
    public Task<List<string>> SuggestAsync(string prefix, int limit = 5)
    {
        // TODO (Step 10): The autocomplete operator, on name — it only works because Step 5 mapped
        // `name` as an autocomplete field.
        //   Builders<Toy>.Search.Autocomplete(t => t.Name, prefix)
        //   .Limit(limit)  then project just the names.
        throw new NotImplementedException("Step 10: an autocomplete query.");
    }
}

/// <summary>One search result: the toy's name, and how well it matched.</summary>
/// <remarks>The score is only meaningful relative to other scores from the same query.</remarks>
[BsonIgnoreExtraElements]
public sealed record ToyHit(
    [property: BsonElement("name")] string Name,
    [property: BsonElement("score")] double Score);

/// <summary>What <see cref="ToySearch.DescribeIndexAsync"/> found out about an index.</summary>
public sealed record IndexSummary(string Name, bool Queryable, bool Dynamic, IReadOnlyList<string> MappedFields);
