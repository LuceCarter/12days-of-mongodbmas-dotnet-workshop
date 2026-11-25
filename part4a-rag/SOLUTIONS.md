# Part 4A — Solutions

Every answer, with the reasoning. **Using this isn't cheating**: reading a worked example
is how most people learn a new technique. Try a step first, then come here.

Each solution below is exactly what the tests check.

<details>
<summary><b>Step 1 — ParseHandbook</b> (unstructured source; collect and prepare data)</summary>

```csharp
public static List<HandbookSection> ParseHandbook(string markdown)
{
    var sections = new List<HandbookSection>();

    // A section starts at a line that begins "## ". Put a newline in front so a file that STARTS with one counts,
    // then skip whatever comes before the first: the title and a note are not a section.
    var blocks = ("\n" + markdown.Replace("\r\n", "\n")).Split("\n## ").Skip(1);

    foreach (var block in blocks)
    {
        var lines = block.Split('\n');
        var heading = lines[0].Trim();

        var audienceLine = lines.FirstOrDefault(l => l.StartsWith("Audience:", StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"The section '{heading}' has no 'Audience:' line.");

        var body = string.Join("\n", lines.Skip(1).Where(l => l != audienceLine)).Trim();

        sections.Add(new HandbookSection(heading, audienceLine["Audience:".Length..].Trim(), body));
    }

    return sections;
}
```

**Collecting** is deciding what knowledge the assistant should have: here, one markdown file. **Preparing** is turning it
into pieces a program can work with, and this is where RAG pipelines quietly go wrong: a lost heading, a stray `\r`,
a section that bled into the next. Every later step inherits whatever you get wrong here.

Two details worth the trouble:

- **`\r\n`.** Git on Windows can check a file out with Windows line endings. Normalising first means the parser doesn't
  care which machine it is on.
- **The `Audience:` check.** An audience is what the retriever filters on (Step 6). A section without one would just
  never be found by a filtered search, and you'd spend an hour wondering why. Failing *here*, and naming the section,
  turns that into a one-line fix.

The handbook is **unstructured**: prose with no schema, which is the most common thing people want to ask questions of
(manuals, wikis, tickets, policies, transcripts).
</details>

<details>
<summary><b>Step 2 — ToyToText</b> (structured source)</summary>

```csharp
public static string ToyToText(Toy toy)
{
    var price = toy.Price.ToString("0.00", CultureInfo.InvariantCulture);
    var stock = toy.InStock ? "In stock." : "Out of stock.";
    var status = toy.Discontinued ? " Discontinued: the elves have stopped making it." : "";

    return $"{toy.Name} ({toy.Category}). For ages {toy.MinAge} to {toy.MaxAge}. £{price}. {stock}{status} {toy.Description}";
}
```

A toy is **structured**: named fields with types. An embedding model reads text, and a language model quotes text, so the
record has to become a sentence first. What goes in the sentence is a decision about *what the assistant should be able
to answer*.

The stock and discontinued facts are the point. Leave them out and a model is told only that a toy exists, so it will
cheerfully recommend a toy that isn't made any more. **Anything that changes what a sensible answer is has to be in the text.**

The other way to use structured data is not to embed it at all, but to let the assistant **query** it live. That is
Part 4B: an agent with a tool that looks stock up *now*, because stock changes every hour and a vector made last week
doesn't. Embed what is *about meaning* and changes slowly (a handbook); query what is *about facts* and changes quickly
(a stock count). Good RAG systems use both.

**`CultureInfo.InvariantCulture`.** On a machine set to a country that writes `89,99`, `{toy.Price}` would print a comma.
The text goes to a model and into a vector, so it shouldn't depend on where the code ran.
</details>

<details>
<summary><b>Step 3 — SplitIntoChunks</b> (chunk overlap)</summary>

```csharp
public static List<string> SplitIntoChunks(string text, int size, int overlap)
{
    ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
    ArgumentOutOfRangeException.ThrowIfNegative(overlap);
    ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(overlap, size);

    var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    var chunks = new List<string>();

    for (var start = 0; start < words.Length; start += size - overlap)
    {
        chunks.Add(string.Join(' ', words.Skip(start).Take(size)));

        // This window reached the last word: a further one would be nothing but words already seen.
        if (start + size >= words.Length) break;
    }

    return chunks;
}
```

**Why chunk at all?** A vector summarises *everything* in the text it was made from. A whole section becomes one blurry
average. A few sentences are about one thing, so the question "what do I do when a reindeer won't eat?" lands close to
the chunk that says so, not close to a section that also covers hooves, harnesses and rest.

**Why overlap?** A cut lands wherever the word count says, not where an idea ends. Without overlap, a sentence that
straddles a boundary is half in one chunk and half in the next, and **neither half alone answers anything**. With 20 words
of overlap, whatever sits on a boundary is whole in at least one chunk.

It isn't free: more overlap means more chunks (here `start` moves 60 words each time, not 80), so more to embed, store,
and search. And chunks that are *nearly* the same can crowd the same answer into the top results. The usual starting
points are 10–20% overlap, and then **measuring on real questions**. The chunker's `size` and `overlap` here were chosen
to make the handbook's sections, of about 200 to 260 words, come out as three to five chunks each.

The two things this step guards are real bugs:

- `overlap >= size` never moves `start` forward: an **infinite loop**.
- Without the `break`, a text of exactly `size` words produces a second, one-word-shorter chunk that is wholly inside the
  first: noise that matches the same questions twice.

We split on **words** because that is simple and deterministic. Real systems often chunk by *tokens* (what the model
counts), by sentences, or by the document's own structure (headings, paragraphs). The ideas of size and overlap are the same.
</details>

<details>
<summary><b>Step 4 — IngestAsync</b> (data collection, preparation and ingestion)</summary>

```csharp
public async Task<int> IngestAsync(IReadOnlyList<HandbookSection> sections)
{
    var ready = new List<HandbookChunk>();

    foreach (var section in sections)
    {
        var pieces = Chunker.SplitIntoChunks(section.Body, _chunkWords, _overlapWords);

        for (var index = 0; index < pieces.Count; index++)
        {
            ready.Add(new HandbookChunk
            {
                Id = ChunkId(section.Section, index),
                Section = section.Section,
                Audience = section.Audience,
                Source = $"The Elf Handbook › {section.Section}",
                ChunkIndex = index,
                Text = pieces[index],
                Embedding = await _embedder.EmbedAsync(pieces[index], "document"),
            });
        }
    }

    if (ready.Count == 0) return 0;

    // Replace by id, so running this twice leaves one copy of each chunk ...
    await _chunks.BulkWriteAsync(ready.Select(chunk =>
        new ReplaceOneModel<HandbookChunk>(Builders<HandbookChunk>.Filter.Eq(c => c.Id, chunk.Id), chunk) { IsUpsert = true }));

    // ... and remove the chunks of these sections that the new text no longer has.
    var names = sections.Select(s => s.Section).ToList();
    var keep = ready.Select(c => c.Id).ToList();
    await _chunks.DeleteManyAsync(c => names.Contains(c.Section) && !keep.Contains(c.Id));

    return ready.Count;
}
```

**Ingestion is a pipeline**: load, clean, chunk, embed, store. Each chunk is stored **with** what it came from
(`Section`, `Audience`, `Source`, `ChunkIndex`), not just its vector. That metadata is what makes the rest of the
part work: **`Section` and `Audience` are what the retriever filters on**, and **`Source` is what the answer
cites**. A RAG system whose chunks don't remember where they came from can't prefilter and can't cite.

Embed chunks with `"document"`, and (Step 8) questions with `"query"`. Voyage's model is trained to expect the two
differently, and mixing them up costs quality quietly, with no error.

**Idempotence.** Ingestion runs again every time the handbook changes. Replace-by-id means it never doubles up. The
`DeleteMany` matters just as much and is easy to forget: if a section got *shorter*, its old last chunk would otherwise sit
in the collection for ever and your assistant would keep quoting a rule someone deleted.

The embeddings are made one at a time here, to keep it readable. For a large corpus you would **batch** (Voyage takes many
texts per request) and keep an eye on its rate limits.

**Shell** (what one stored chunk looks like):
```js
db.handbook_chunks.findOne({ _id: "toy-safety-1" }, { embedding: 0 })
// { _id: "toy-safety-1", section: "Toy Safety", audience: "toy-makers", source: "The Elf Handbook › Toy Safety",
//   chunkIndex: 1, text: "...brass test tube..." }
```
</details>

<details>
<summary><b>Step 5 — CreateIndexAsync</b> (a prefiltered retriever, part 1)</summary>

```csharp
public async Task CreateIndexAsync(string name)
{
    var definition = new BsonDocument("fields", new BsonArray
    {
        new BsonDocument { { "type", "vector" }, { "path", "embedding" }, { "numDimensions", Dimensions }, { "similarity", "cosine" } },
        new BsonDocument { { "type", "filter" }, { "path", "section" } },
        new BsonDocument { { "type", "filter" }, { "path", "audience" } },
    });

    await _chunks.SearchIndexes.CreateOneAsync(new CreateSearchIndexModel(name, SearchIndexType.VectorSearch, definition));

    await WaitUntilQueryableAsync(name);
}
```

This is Part 3B's Step 4 again with different fields, so the traps are the same ones:

- **`numDimensions` must match the model**: 1024 for the vectors in this repo. Wrong, and the index builds happily, then
  every query errors (or, if you pad the query, quietly returns nothing).
- **Only declared fields can be filtered on.** `section` and `audience` are declared as `filter`. Ask for a filter on
  a field the index doesn't declare and you get `Path 'x' needs to be indexed as filter`. Each declaration is
  something more to build and keep, so declare what you will really filter by, not everything.
- **Creating returns straight away.** Wait for `queryable`, or your first query returns nothing and says nothing.

**Shell:**
```js
db.handbook_chunks.createSearchIndex("handbook-vectors", "vectorSearch", { fields: [
  { type: "vector", path: "embedding", numDimensions: 1024, similarity: "cosine" },
  { type: "filter", path: "section" },
  { type: "filter", path: "audience" } ] })
```
</details>

<details>
<summary><b>Step 6 — RetrieveAsync</b> (a prefiltered retriever)</summary>

```csharp
public async Task<List<RetrievedChunk>> RetrieveAsync(float[] queryVector, int k, string? section = null, string? audience = null)
{
    var filters = new List<FilterDefinition<HandbookChunk>>();
    if (section is not null) filters.Add(Builders<HandbookChunk>.Filter.Eq(c => c.Section, section));
    if (audience is not null) filters.Add(Builders<HandbookChunk>.Filter.Eq(c => c.Audience, audience));

    var options = new VectorSearchOptions<HandbookChunk>
    {
        IndexName = _indexName,
        NumberOfCandidates = Math.Min(k * 20, 10_000),
        Filter = filters.Count == 0 ? null : Builders<HandbookChunk>.Filter.And(filters),
    };

    return await _chunks.Aggregate()
        .VectorSearch(c => c.Embedding, new QueryVector(queryVector), k, options)
        .Project<RetrievedChunk>(HitProjection)
        .ToListAsync();
}
```

A **retriever** is the "R" in RAG: given a question's vector, it returns the few chunks most worth showing the model.
A **prefiltered** retriever narrows *what it searches* first. An elf who asks as a `sleigh-crew` member doesn't
want reindeer-feeding rules ranked above loading rules because their wording happened to be closer.

The filter goes **inside** `$vectorSearch`. That makes it a pre-filter: the search looks only at chunks that pass and
still returns `k` of them. Do the same thing *after* the search and you get whichever of the nearest `k` happened to be in
the right section, which can be none (the test for this asks a reindeer question of the Elf Wellbeing section: the nearest chunks
are all about reindeer, and a post-filter would throw every one away).

Filters here are **equalities** (`section == x`, `audience == y`), joined with `and`. That covers the real need, and it is
also all the Semantic Kernel MongoDB connector (Step 9's note) can express today. Part 3B's age range was a `$gte`/`$lte`
pair, which the raw driver handles and that connector doesn't.

`HitProjection` leaves out the vector. A caller wants the text, not a thousand numbers, and sending them back
over the wire to throw them away is a waste.

**Shell:**
```js
db.handbook_chunks.aggregate([
  { $vectorSearch: { index: "handbook-vectors", path: "embedding", queryVector: v, numCandidates: 80, limit: 4,
                     filter: { section: "Toy Safety" } } },
  { $project: { section: 1, source: 1, text: 1, score: { $meta: "vectorSearchScore" } } }
])
```
</details>

<details>
<summary><b>Step 7 — BuildPrompt</b> (what RAG is; the limits of an LLM it addresses)</summary>

```csharp
public static List<ChatMessage> BuildPrompt(string question, IReadOnlyList<RetrievedChunk> chunks)
{
    const string rules = """
        You are the Elf Assistant at Santa's Workshop.
        Answer using ONLY the handbook extracts you are given.
        If the extracts do not contain the answer, say that you don't know instead of guessing.
        Say which source each part of your answer comes from.
        """;

    var extracts = chunks.Count == 0
        ? "(no extracts were found)"
        : string.Join("\n\n", chunks.Select((c, i) => $"[{i + 1}] Source: {c.Source}\n{c.Text}"));

    return
    [
        new ChatMessage(ChatRole.System, rules),
        new ChatMessage(ChatRole.User, $"Handbook extracts:\n\n{extracts}\n\nQuestion: {question}"),
    ];
}
```

**What RAG is:** *retrieval-augmented generation*. Instead of hoping a model remembers the answer, you **retrieve** the
relevant text from your own data, **augment** the prompt with it, and let the model **generate** an answer from it.

**The limits of a language model that this fixes:**

| Limit | What goes wrong | What RAG does |
|---|---|---|
| **It has never seen your data** | Ask about the brass test tube and it invents one, fluently | The right text is in the prompt |
| **Its knowledge stops at a training date** | Last year's rule is stated as today's | Retrieval reads today's collection |
| **It can't say where an answer came from** | You can't check it | The prompt carries sources, so the answer can cite them |
| **It guesses instead of saying "I don't know"** | A confident wrong answer is worse than none | The rules say to use only the extracts, and when to decline |

**Where it doesn't help:** retrieval can only hand over what it finds. If the right chunk isn't retrieved, the model
answers from the wrong ones (or declines): **the quality of a RAG answer is capped by the quality of retrieval**, which
is why Steps 3–6 got most of this part. And a model can still ignore its instructions. The rules lower the risk, they
don't remove it, so you check with real questions.

**Use cases** worth knowing: support and help desks over product docs, internal knowledge assistants (like this one),
question-answering over contracts, policies, tickets and papers, and anywhere "answer from *our* documents and show
where" matters more than "answer from the internet".

The question goes **last**. Models weigh the end of a long prompt heavily, so the thing you are asking should be the
thing nearest to where they start writing.

Nothing here calls a model, so it can be tested exactly. Prompt text is code: keep it where you can read it, and test it.
</details>

<details>
<summary><b>Step 8 — AskAsync</b> (RAG architecture; a generator separate from the embedding model)</summary>

```csharp
public async Task<ElfAnswer> AskAsync(string question, string? section = null, string? audience = null, int k = 4)
{
    // 1. Embed the question, with the same model that embedded the chunks.
    var vector = await _embedder.EmbedAsync(question, "query");

    // 2. Retrieve, narrowed first if we were told to.
    var found = await _retriever.RetrieveAsync(vector, k, section, audience);

    // 3. Keep what is actually relevant, not merely the nearest of a bad lot.
    var relevant = found.Where(c => c.Score >= _minScore).ToList();

    // 4. Nothing relevant: say so, without spending a model call on a guess.
    if (relevant.Count == 0)
        return new ElfAnswer(NoAnswer, [], Grounded: false);

    // 5. Generate from the extracts.
    var response = await _generator.GetResponseAsync(GroundedPrompt.BuildPrompt(question, relevant));

    return new ElfAnswer(response.Text, relevant.Select(c => new Citation(c.Source, c.Id)).ToList(), Grounded: true);
}
```

**The components of a RAG architecture on Atlas**, as this method strings them together:

| Component | In this part | Job |
|---|---|---|
| **Data sources** | the Elf Handbook (and the toys) | what the assistant knows |
| **Chunker** | `Chunker` | cut it into findable pieces |
| **Embedding model** | `IEmbedder` (Voyage AI) | text → vector, at ingestion **and** at question time |
| **Vector store** | MongoDB Atlas, with a vector index | keep the vectors beside their text and metadata; find the nearest |
| **Retriever** | `Retriever` | question vector → the few best chunks, prefiltered |
| **Prompt builder** | `GroundedPrompt` | chunks and question → messages |
| **Generator** | `IChatClient` (Gemini) | write the answer |

**A generator separate from the embedding model.** Look at the constructor: an `IEmbedder` *and* an `IChatClient`.
They are different models, from different companies, doing different jobs. The embedding model has to match what
was stored: change it and every vector must be remade. The generator doesn't, so you can swap Gemini for another model by
changing one line in `ElfLlm` and nothing else. Keeping them apart is what makes each replaceable, and lets you choose the
best tool for each job rather than one model for both.

**The relevance bar.** A vector search *always* returns its nearest neighbours, even when nothing is near. Ask the handbook
the capital of France and you still get four chunks back, the least-unlike ones. Pass those to a model and it may build
an answer out of them. `_minScore` is what lets the system say "I don't know": below it, nothing is returned and **the model
isn't called at all**, which is cheaper, faster and safer than asking it to refuse. Where to set the bar is a measurement
on your model and your data: the README shows how this one was chosen.

`response.Text` is whatever the model wrote. This code never claims it is *correct*, only that it was **grounded**: it
was shown these sources. Whether the model used them faithfully is a different question, and the test you can't write
without a real model and real questions.
</details>

<details>
<summary><b>Step 9 — AsTextSearch</b> (a retrieval tool)</summary>

```csharp
public Func<string, CancellationToken, Task<IEnumerable<TextSearchProvider.TextSearchResult>>> AsTextSearch(
    int k = 4, string? section = null, string? audience = null)
{
    return async (question, cancellationToken) =>
    {
        var vector = await _embedder.EmbedAsync(question, "query", cancellationToken);
        var found = await _retriever.RetrieveAsync(vector, k, section, audience);

        return found
            .Where(c => c.Score >= _minScore)
            .Select(c => new TextSearchProvider.TextSearchResult { SourceName = c.Source, Text = c.Text })
            .ToList();
    };
}
```

Microsoft Agent Framework's **`TextSearchProvider`** is its ready-made RAG building block, and it takes a **function**,
not a collection: *given a question, return a few text results*. This is that function, built from the retriever you
already wrote. That small shape is the point: **the framework doesn't care how you search**, so everything you
built (the chunks, the prefilter, the relevance bar) plugs straight in.

It can run in two ways:

- **`BeforeAIInvoke`**: search on every turn and put the results in the prompt. RAG in its plain form.
- **`OnDemandFunctionCalling`**: give the model the search as a **tool** and let *it* decide when to call it. That is the
  start of an agent, and it is Part 4B.

**The framework's own vector-store connector** (`MongoVectorStore`, in `Microsoft.SemanticKernel.Connectors.MongoDB`)
can build this function too. It is covered in the README, with its trade-offs, because it is a **preview package** and
its filters are equality-only.
</details>

---

## If something goes wrong

**Step 3's last test fails but the others pass.** The shipped vectors were made from chunks of **80 words with 20
overlap**, cut exactly as the worked example shows. If your chunks differ, even by one word, the vectors in the repository
no longer describe your text. The test message names the section and both counts.

**Step 5 passes but Step 6 finds nothing.** The reference index in the tests is not yours, so Step 6 reads that one. If
your own Step 5 index finds nothing, compare its `numDimensions` with **1024**.

**Step 6: "Path 'section' needs to be indexed as filter".** The index doesn't declare `section` (or `audience`) as a
`filter` field. Fix Step 5.

**Step 6: "numCandidates … limit".** `numCandidates` must be at least `limit` and at most 10,000.

**Steps 4–6 time out waiting for the index.** Atlas Local's search engine (`mongot`) isn't running: `docker compose up -d`,
then `dotnet run --project tools/doctor`.

**`handbook-chunks.json isn't part of this build`.** Pull the latest, or generate it yourself with a Voyage key:
`dotnet run --project tools/embed -- --handbook`.
