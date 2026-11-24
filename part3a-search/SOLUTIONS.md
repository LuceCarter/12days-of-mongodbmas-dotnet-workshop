# Part 3A — Solutions

Every answer, with the reasoning. **Using this isn't cheating** — reading a worked example
is how most people learn a new query. Try a step first, then come here.

Each solution below is exactly what's verified by the tests.

<details>
<summary><b>Step 1 — CreateDynamicIndexAsync</b> (objective 1)</summary>

```csharp
public async Task CreateDynamicIndexAsync()
{
    var definition = new BsonDocument("mappings", new BsonDocument("dynamic", true));

    await _toys.SearchIndexes.CreateOneAsync(new CreateSearchIndexModel(_dynamicIndex, definition));

    // Creating an index returns at once. Wait until the server says it will answer queries.
    await WaitUntilQueryableAsync(_dynamicIndex);
}
```

An index *definition* is just a document, and `{ mappings: { dynamic: true } }` is the shortest
one there is: "index every field you find, and work out its type yourself".

**Why the wait?** `CreateOneAsync` returns as soon as the server has *accepted* the index, not when it
has *built* it. A query sent in that gap doesn't fail. It comes back **empty**, which looks exactly
like "nothing matched". Locally the gap is a fraction of a second; on an Atlas free cluster it is about
a minute. Waiting on `queryable` is right on both. `Task.Delay(5000)` is right on neither.

**Shell:** `db.toys.createSearchIndex("toys-dynamic", { mappings: { dynamic: true } })`
</details>

<details>
<summary><b>Step 2 — SearchDescriptionsAsync</b> (objectives 1 and 5)</summary>

```csharp
public async Task<List<ToyHit>> SearchDescriptionsAsync(string words)
{
    return await _toys.Aggregate()
        .Search(Builders<Toy>.Search.Text(t => t.Description, words), indexName: _dynamicIndex)
        .Project<ToyHit>(HitProjection)
        .ToListAsync();
}
```

`Search(...)` adds a `$search` stage, which must be **the first stage** of a pipeline. Printed, the
query is exactly what you would type in the shell:

```js
{ $search: { index: "toys-dynamic", text: { query: "dinosaur", path: "description" } } }
```

`indexName:` matters. Leave it out and Atlas Search looks for an index called `default`, finds none, and
returns nothing, with no error.

The `score` in each hit comes from `{ $meta: "searchScore" }` in the projection. Atlas Search sorts by it
for you, best first. That is the main difference from a `Find()`: there you filter and then sort; here the
database also tells you *how well* each document matched.

**Why only one toy?** The index is *dynamic*, so `description` was indexed as ordinary text with the
standard analyzer, which splits on spaces and lower-cases words but doesn't know that "dinosaurs" is the
plural of "dinosaur". Step 5 fixes that.

**Shell:** `db.toys.aggregate([ { $search: { index: "toys-dynamic", text: { query: "dinosaur", path: "description" } } } ])`
</details>

<details>
<summary><b>Step 3 — DescribeIndexAsync</b> (objective 3)</summary>

```csharp
public async Task<IndexSummary> DescribeIndexAsync(string indexName)
{
    var indexes = await (await _toys.SearchIndexes.ListAsync(indexName)).ToListAsync();
    var index = indexes.Single();

    var mappings = index["latestDefinition"]["mappings"].AsBsonDocument;

    // A dynamic index has no "fields" at all: there is nothing to list, because it maps whatever it meets.
    var fields = mappings.GetValue("fields", new BsonDocument()).AsBsonDocument.Names.ToList();

    return new IndexSummary(indexName, index["queryable"].AsBoolean, mappings["dynamic"].AsBoolean, fields);
}
```

The point of this step is what comes back: for the dynamic index, **no fields**. Nothing is listed
because nothing was decided. That is the whole trade:

| | Dynamic | Static |
|---|---|---|
| You write | one line | a line per field |
| Good for | exploring, prototypes, documents whose fields vary | anything you ship |
| Types | guessed: strings become `string`, numbers `number`, and so on | chosen by you |
| Analyzers, `token`, `autocomplete` | not available | available |
| Index size | everything, including fields you never search | only what you list |

**Shell:** `db.toys.getSearchIndexes("toys-dynamic")`
</details>

<details>
<summary><b>Step 4 — FindInCategoryAsync</b> (objective 3)</summary>

```csharp
public async Task<List<ToyHit>> FindInCategoryAsync(string category, string indexName)
{
    return await _toys.Aggregate()
        .Search(Builders<Toy>.Search.Equals(t => t.Category, category), indexName: indexName)
        .Project<ToyHit>(HitProjection)
        .ToListAsync();
}
```

Run against `toys-dynamic`, this returns **nothing** and doesn't complain. The query is right; the
*index* can't answer it. A dynamic index maps a string as `string`, which is cut into words for
full-text search. `equals` wants a single exact value, which is what the `token` type stores. Step 5 maps
`category` as `token`, and the same call then finds the three puzzles.

So: the query didn't change, the answer did, and the difference was in the index. Whenever a `$search`
returns nothing you expected, the index is the first place to look.

**Shell:** `{ $search: { index: "toys-static", equals: { path: "category", value: "puzzle" } } }`
</details>

<details>
<summary><b>Step 5 — CreateStaticIndexAsync</b> (objective 4)</summary>

```csharp
public async Task CreateStaticIndexAsync()
{
    var definition = BsonDocument.Parse("""
        { mappings: { dynamic: false, fields: {
            name:         [ { type: "string" }, { type: "autocomplete" } ],
            description:  { type: "string", analyzer: "lucene.english" },
            tags:         { type: "string" },
            category:     { type: "token" },
            price:        { type: "number" },
            minAge:       { type: "number" },
            maxAge:       { type: "number" },
            inStock:      { type: "boolean" },
            discontinued: { type: "boolean" }
        } } }
        """);

    await _toys.SearchIndexes.CreateOneAsync(new CreateSearchIndexModel(_staticIndex, definition));
    await WaitUntilQueryableAsync(_staticIndex);
}
```

Reading it field by field, which is exactly what the README's plan table asked for:

- **`name`** has **two** types, as a list. One field can be searched in more than one way: as ordinary
  text (`string`) and as as-you-type (`autocomplete`, used in Step 10).
- **`description`** is `string` with the **English analyzer**. An analyzer decides how text becomes
  searchable words; this one removes "the" and "a", and **stems** words, so "dinosaurs" and "dinosaur"
  become the same word.
- **`tags`** is `string`. Each tag is a word to find.
- **`category`** is `token`: the whole value, as one exact unit. That is what `equals` needs.
- **`minAge`, `maxAge`** are `number`: whole numbers you can compare with `range`.
- **`inStock`, `discontinued`** are `boolean`.
- **`price`** is `number`. **This is the trap.** The server accepts the mapping, builds the index and
  reports it ready, and then Step 6 finds out the price never made it in: the stored type is Decimal128,
  which `number` can't hold.

`dynamic: false` is what makes this a static mapping: only the fields listed exist. Anything not in
the list, `stockCount` for one, can't be searched.

**Shell:** `db.toys.createSearchIndex("toys-static", { mappings: { dynamic: false, fields: { ... } } })`
</details>

<details>
<summary><b>Step 6 — ToysUnderPriceAsync</b> (objective 4)</summary>

```csharp
public async Task<List<ToyHit>> ToysUnderPriceAsync(string words, decimal maxPrice)
{
    var search = Builders<Toy>.Search;

    // The text search is the part Atlas Search is good at, and it can see everything it needs.
    // The price check is an ordinary $match AFTER it: a $match filters on the stored value, and
    // MongoDB compares Decimal128 correctly.
    return await _toys.Aggregate()
        .Search(search.Text(new[] { "name", "description", "tags" }, words), indexName: _staticIndex)
        .Match(t => t.Price <= maxPrice)
        .Project<ToyHit>(HitProjection)
        .ToListAsync();
}
```

**The trap, in full.** `price` is stored as **Decimal128**, on purpose: money and binary floating point
don't mix (that was Part 1). Atlas Search's `number` type indexes `int32`, `int64` and `double`.
**It does not index Decimal128.** It bites in two layers:

1. **Hand the query a `decimal`**, the type `maxPrice` already is, and the server refuses, loudly:
   `"range.lte" Type is not supported: DECIMAL128`. That is the helpful one. It is the server telling you.
2. **Cast it to a `double`** to make that error go away, and the query runs, returns **nothing**, and says
   nothing. The mapping was accepted and the index reported ready, but every `price` was left out of the
   index when it was built, so there is nothing for a `range` to match. The elf in the Starter took exactly
   this road, and an empty result is also what "no toy costs that little" looks like.

We measured both rather than assumed them. Over this data, a `range` on `price` as a `double` query finds
no toys. The same `range` over a field that really is a `double` finds them.

**What to do about it, in order of preference:**

1. **Filter the price in a `$match` after the `$search`**, as above. `$search` finds and ranks; `$match`
   narrows. This costs nothing here, because `$search` has already cut 34 toys down to a few.
2. **Keep a searchable copy.** Store a second field, say `priceInPence` as an `int`, and map *that*.
   Then `range` works and ranking stays in one stage. The price of this is a field to keep in sync.
3. **Don't store money as `double`.** It would make the index happy and the till unhappy.

**How would you ever notice?** From the second layer, you wouldn't, from the query alone. The habit that
catches it: when a `range` or `equals` returns nothing you expected, check the *type* of the field in the
data against the types the mapping supports. And read errors like `Type is not supported: DECIMAL128`
before you make them go away.

**Shell:**
```js
db.toys.aggregate([
  { $search: { index: "toys-static", text: { query: "space", path: ["name", "description", "tags"] } } },
  { $match: { price: { $lte: NumberDecimal("15") } } }
])
```
</details>

<details>
<summary><b>Step 7 — GiftIdeasAsync</b> (objective 5)</summary>

```csharp
public async Task<List<ToyHit>> GiftIdeasAsync(string words, int age)
{
    var search = Builders<Toy>.Search;

    var query = search.Compound()
        .Must(search.Text(new[] { "name", "description", "tags" }, words))
        .Filter(search.Equals(t => t.InStock, true))
        .Filter(search.Range(t => t.MinAge, SearchRangeBuilder.Lte(age)))
        .Filter(search.Range(t => t.MaxAge, SearchRangeBuilder.Gte(age)))
        .MustNot(search.Equals(t => t.Discontinued, true));

    return await _toys.Aggregate()
        .Search(query, indexName: _staticIndex)
        .Project<ToyHit>(HitProjection)
        .ToListAsync();
}
```

`compound` is how a real query is built: several operators, each with a different job.

| Clause | Must the document match? | Does it affect the score? |
|---|---|---|
| `must` | yes | yes |
| `should` | no (unless `minimumShouldMatch`) | yes: matching raises the score |
| `filter` | yes | **no** |
| `mustNot` | it must **not** | no |

So the words the child's relative typed go in `must`, because they decide *how well* a toy fits. "In
stock", "right age" and "not discontinued" are yes-or-no questions that say nothing about how *good* a
match is, so they are filters, and don't distort the ranking.

Ages need two ranges because a toy has a *span*: it suits age 6 if `minAge <= 6` **and** `maxAge >= 6`.
Both are ints, which Atlas Search handles. (Compare Step 6.)

**Shell:**
```js
{ $search: { index: "toys-static", compound: {
    must:    [ { text: { query: "space", path: ["name", "description", "tags"] } } ],
    filter:  [ { equals: { path: "inStock", value: true } },
               { range:  { path: "minAge", lte: 6 } },
               { range:  { path: "maxAge", gte: 6 } } ],
    mustNot: [ { equals: { path: "discontinued", value: true } } ] } } }
```
</details>

<details>
<summary><b>Step 8 — RankedAsync</b> (objective 2)</summary>

```csharp
public async Task<List<ToyHit>> RankedAsync(string words)
{
    var search = Builders<Toy>.Search;
    var boosted = new SearchTextOptions<Toy> { Score = new SearchScoreDefinitionBuilder<Toy>().Boost(5) };

    var query = search.Compound()
        .Should(search.Text(t => t.Name, words, boosted))
        .Should(search.Text(new[] { "description", "tags" }, words))
        .MinimumShouldMatch(1);

    return await _toys.Aggregate()
        .Search(query, indexName: _staticIndex)
        .Project<ToyHit>(HitProjection)
        .ToListAsync();
}
```

Each `should` clause adds its score when it matches, and `MinimumShouldMatch(1)` says at least one must.
The name clause's score is multiplied by 5.

Search for "wooden" without the boost and the **Skipping Rope, Wooden Handles** comes 5th, because the
word only appears once in its text and "wooden" is in a lot of descriptions. With the boost it comes 2nd,
behind only the **Wooden Tool Bench**, which has the word in both name and tags. Someone typing "wooden"
into a shop's search box is far more likely to want something *called* wooden. Boosting is how you tell
the search engine what you know about your data.

**Where the score comes from.** Atlas Search uses BM25, a relevance formula in which a word counts more
when it is **rare** across all your documents, appears **often** in this one, and sits in a **short**
field. That is why "dinosaur" scores higher on the Dinosaur Dig Kit (in its name, tags *and* description) than on the
Pterodactyl Glider (only a tag).

**Ties are normal.** Search for "family": six toys all score exactly 0.768, because each has the word
once, in a tag. The order within a tie isn't something to rely on.

Add `scoreDetails: true` to the `$search` and project `{ $meta: "searchScoreDetails" }` to see the whole
calculation for a result. See Level up.

**Shell:**
```js
{ $search: { index: "toys-static", compound: {
    should: [ { text: { query: "wooden", path: "name", score: { boost: { value: 5 } } } },
              { text: { query: "wooden", path: ["description", "tags"] } } ],
    minimumShouldMatch: 1 } } }
```
</details>

<details>
<summary><b>Step 9 — FuzzySearchAsync</b> (objective 5)</summary>

```csharp
public async Task<List<ToyHit>> FuzzySearchAsync(string words)
{
    var search = Builders<Toy>.Search;
    var forgiving = new SearchTextOptions<Toy> { Fuzzy = new SearchFuzzyOptions { MaxEdits = 2 } };

    return await _toys.Aggregate()
        .Search(search.Text(new[] { "name", "description", "tags" }, words, forgiving), indexName: _staticIndex)
        .Project<ToyHit>(HitProjection)
        .ToListAsync();
}
```

`fuzzy` lets a word be a few **single-letter edits** (a change, a missing letter, an extra one) away from
a real word. "dinasaur" is one letter from "dinosaur", so it matches. Without `fuzzy`, the same search
finds nothing at all.

`MaxEdits` can only be 1 or 2. It is a trade: more forgiveness finds more typos, and also more
accidental matches. Short words suffer most. With 2 edits, "cat" is close to "car", "cut" and "hat".
There is a `PrefixLength` option too, which says the first N letters must be right, and which cheapens
the search a lot on a big collection.

**Shell:** `{ $search: { index: "toys-static", text: { query: "dinasaur", path: ["name", "description", "tags"], fuzzy: { maxEdits: 2 } } } }`
</details>

<details>
<summary><b>Step 10 — SuggestAsync</b> (objective 5)</summary>

```csharp
public async Task<List<string>> SuggestAsync(string prefix, int limit = 5)
{
    var hits = await _toys.Aggregate()
        .Search(Builders<Toy>.Search.Autocomplete(t => t.Name, prefix), indexName: _staticIndex)
        .Limit(limit)
        .Project<ToyHit>(HitProjection)
        .ToListAsync();

    return hits.Select(h => h.Name).ToList();
}
```

`autocomplete` only works on a field mapped as `autocomplete`, which is what Step 5's second type on
`name` was for. At index time it cuts each word into every beginning (`ro`, `roc`, `rock`, `rocke`...),
so at query time a half-typed word is simply an exact match. That moves the work to indexing and makes
the query fast enough to run on every keystroke.

Notice that `"wo"` finds *Wooden* Tool Bench, *Woodland* Animal Dominoes, Skipping Rope, *Wooden* Handles
**and** Jigsaw: Santa's *Workshop*. It completes **any** word in the name, not just the first.

`.Limit(limit)` goes straight after the search. The results are already best-first, so the limit keeps the
best few. The same reason Part 2's `$sort` came before its `$limit`: limit last and you throw away the wrong ones.

**Shell:** `{ $search: { index: "toys-static", autocomplete: { query: "dino", path: "name" } } }`, then `{ $limit: 5 }`
</details>

---

## Still red?

**All of them failing with a connection error.** MongoDB isn't running:
```bash
docker compose up -d
dotnet run --project tools/doctor
```

**Every search test returns nothing.** Does the doctor say Search is available? These tests need Atlas Local
(the `docker-compose.yml` one); a plain MongoDB container has no `$search`.

**Step 1 or 5 times out.** The index never became queryable. Check the definition is valid JSON
(a stray comma is enough) and run `db.toys.getSearchIndexes()` in the shell to see the `status`.

**Step 2 finds nothing.** Did you pass `indexName`? Without it Atlas Search looks for an index named `default`.

**Step 4 is empty on the static index too.** Is `category` mapped as `token` in Step 5, not `string`?

**Step 6 still returns nothing.** Casting the price to a `double` silences the error but not the problem. The price check has to leave `$search`: a `Match` *after* it.

**Step 7 includes a toy that's out of stock or too old or too young.** Each rule is its own `Filter`;
the age needs *two* ranges, one on `minAge` and one on `maxAge`.

**Step 8: the Skipping Rope isn't second.** Is the boost on the *name* clause only, and are there two
`Should` clauses with `MinimumShouldMatch(1)`?

**Step 10 is empty.** `name` needs `{ type: "autocomplete" }` in the Step 5 mapping, in a list next to
its `string` type. Fix the mapping, drop the index in the shell with `db.toys.dropSearchIndex("toys-static")`, and run the tests again.

**Index already exists.** The tests use their own database (`santas_workshop_part3a_tests`), but if you ran a step
against your practice database twice, drop the first: `db.toys.dropSearchIndex("toys-dynamic")`.
