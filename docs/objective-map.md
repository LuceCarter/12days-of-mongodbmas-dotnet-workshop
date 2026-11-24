# Objective map

**This file is the contract.** Every badge objective must end up with a step that
teaches it and a test that proves it. An objective with no step is a gap we ship
knowingly or not at all — never silently.

Status key: ✅ covered and tested · 🚧 in progress · ⬜ not started ·
🖐️ manual (taught and checklisted, but not provable by an automated test)

---

## Part 1 — The Workshop Inventory
**Anchor badge:** CRUD Overview (C# edition) · **Level:** Beginner · **Release:** Dec 1

**Status: ✅ complete.** 10/10 objectives covered, 22 tests, all red on the starter code
and green on the published solutions. Verified 2026-11-03.

| # | Objective | Step | Test | Status |
|---|---|---|---|---|
| 1.1 | Insert a document using `InsertOne()` | Step 1 · `AddToyAsync` | `Step01_AddToy_InsertsIt` | ✅ |
| 1.2 | Find a single document using `Find()` and implicit equality | Step 2 · `FindByNameAsync` | `Step02_FindByName_FindsIt`, `…_ReturnsNullForUnknown` | ✅ |
| 1.3 | Use `$set` with `UpdateOne()` to replace a field's value | Step 3 · `UpdatePriceAsync` | `Step03_UpdatePrice_ChangesOnlyThatField` | ✅ |
| 1.4 | Write a `DeleteMany()` that deletes all documents matching a query | Step 4 · `RemoveDiscontinuedAsync` | `Step04_RemoveDiscontinued_DeletesOnlyThose` | ✅ |
| 2.1 | Limit results with comparison operators `$gt`, `$lt`, `$gte`, `$lte` | Steps 5–6 · `FindUnderPriceAsync`, `FindForAgeAsync` | `Step05_…_ReturnsCheapToys`, `Step05_…_IsExclusive`, `Step06_…_ReturnsToysSpanningThatAge`, `Step06_…_DoesNotInvertTheComparisons` | ✅ |
| 2.2 | Limit results with logical operators `$and`, `$or` | Steps 7–8 · `FindInStockForAgeBandAsync`, `FindPuzzlesOrBoardGamesAsync` | `Step07_…_CombinesAllThreeConditions`, `Step08_…_ReturnsBothCategories` | ✅ |
| 2.3 | Query an array field using an equality constraint with a scalar value | Step 9 · `FindByTagAsync` | `Step09_…_FindsEveryDinosaur`, `…_MatchesAnyPositionInTheArray`, `…_UnknownTagReturnsEmpty` | ✅ |
| 3.1 | Use sort and limit to refine results | Step 10 · `FindCheapestInStockAsync` | `Step10_…_ReturnsTheFiveCheapest`, `…_IsAscending`, `…_RespectsTheLimit` | ✅ |
| 3.2 | Use projection with `Find()` to return selected fields | Step 11 · `GetShelfLabelsAsync` | `Step11_…_ReturnsNameAndPriceForEveryToy`, `…_CarriesTheCorrectPrice` | ✅ |
| 3.3 | Use `CountDocuments()` with a filter | Step 12 · `CountLowStockAsync` | `Step12_…_CountsToysBelowTheThreshold`, `…_IsExclusive`, `…_ReflectsTheCurrentCollection` | ✅ |

**Seed data check** — every objective above returns a non-empty, non-trivial result
against `src/Shared/SeedData/toys.json` (34 toys). Verified 2026-11-03:

| Query | Matches |
|---|---|
| `Rocking Horse` by name | 1 |
| Discontinued | 4 |
| Price < £20 | 20 |
| `minAge >= 5` | 17 |
| In stock **and** ages 5–8 | 5 |
| Category puzzle **or** board game | 9 |
| Tagged `dinosaur` | 8 |
| In stock (for "5 cheapest") | 25 |
| Low stock (0 < count < 5) | 5 |

### Stretch — Indexing Design Fundamentals (not assessed by the anchor badge)
| Objective | Step | Status |
|---|---|---|
| Index types and their costs | — | ⬜ |
| Workload analysis for a minimal index set | — | ⬜ |
| Index prefixes aligned to query patterns | — | ⬜ |
| The ESR guideline | — | ⬜ |
| Compound index considerations (sort order, cardinality, selectivity) | — | ⬜ |
| `explain()` to verify index use | — | ⬜ |

### Stretch — Relational to Document Model
| Objective | Step | Status |
|---|---|---|
| Document vs. relational model | — | ⬜ |
| Mapping reads/writes to flows and quantifying them | — | ⬜ |
| Embedding vs. referencing for 1:1, 1:N, N:N | — | ⬜ |
| Schema validation | — | ⬜ |

---

## Part 2 — The Naughty/Nice Ledger
**Anchor badge:** Fundamentals of Data Transformation · **Level:** Beginner · **Release:** Dec 5

**Status: ✅ complete.** 5/5 objectives covered, 24 tests, all red on the starter code and green
on the published solutions. Verified 2026-11-18. The one 🖐️ is the Compass half of the last
objective, which a test cannot click through — see the note below the table.

| Objective | Step | Test | Status |
|---|---|---|---|
| Define the aggregation framework and explain why stage order matters | Steps 1–2 · `CountOpenOrdersAsync`, `ScoresThisSeasonAsync` | `Step01_…_CountsThisSeasonsOpenOrders`, `…_ReturnsZeroWhenNothingMatches`, `Step02_…_ReturnsEveryChildWithDeedsThisSeason`, `…_TotalsEachChildsPoints`, `…_IgnoresLastSeasonsDeeds` | ✅ |
| Explain and build `$match` → `$unwind` → `$group` → `$project` | Steps 3–5 · `ToysToMakeAsync`, `DemandByRegionAsync`, `VerdictsAsync` | `Step03_…_TotalsTheQuantityOfEachToy`, `…_ListsEachToyOnce`, `…_LeavesOutPackedAndLastSeasonsOrders`, `Step04_…_TotalsOneToyInEachRegion`, `…_CountsOnlyThatToysQuantity`, `…_NoOpenDemandReturnsNothing`, `Step05_…_SaysNiceOrNaughtyForEveryChild`, `…_AScoreOfZeroIsNice`, `…_IgnoresLastSeason` | ✅ |
| Explain and build `$match` → `$sort` → `$limit` | Step 6 · `KindestDeedsAsync` | `Step06_…_ReturnsTheBestFive`, `…_IsBestFirstAndIgnoresLastSeason`, `…_RespectsTheCount` | ✅ |
| Use `$lookup` and `$set` | Step 7 · `LineCostsAsync` | `Step07_…_PricesEveryLineAtTheCataloguePrice`, `…_LeavesOutToysWeDoNotMake`, `…_UsesTodaysPrice` | ✅ |
| Access `explain()` plans in the MongoDB Shell and Compass | Steps 8–9 · `ExplainAsync`, `CreateSeasonStatusIndexAsync` | `Step08_…_ShowsACollectionScan`, `…_ReportsDocumentsExaminedAndReturned`, `Step09_…_IsCreatedOnSeasonThenStatus`, `…_MakesTheServerUseIt` | ✅ 🖐️ |

> **The Compass half of objective 5** is 🖐️: xUnit can't prove a learner clicked through Compass. The
> shell and driver halves are tested automatically (the explain command, `COLLSCAN` → `IXSCAN`,
> documents examined 46 → 22). The Compass half is covered by exact click-by-click instructions in
> Step 8 and a self-tick row on the README's badge checklist. The Compass steps (Aggregations tab →
> **Explain** → Explain Plan modal → Visual Tree / Raw Output; Indexes tab → **Create Index**) were
> checked against the current Compass documentation, not remembered. They have **not** been clicked
> through by a person on this machine — worth doing once before release.

**Seed data check** — verified 2026-11-18 against `orders.json` and `ledger.json` (and asserted by
`SeedDataContractTests`, so a data edit that breaks one of these fails a test instead of the README):

| Fact the steps rely on | Value |
|---|---|
| Orders | 46: 36 current (22 open, 9 packed, 5 delivered) + 10 previous, **2 of them still open** |
| Ledger entries | 79: 67 current + 12 previous |
| Open orders this season (Step 1) | 22 — and 24 if the season is forgotten |
| Dinosaur puzzle still to make (Step 3) | 11 — and 14 if the season is forgotten |
| Regions wanting the dinosaur puzzle (Step 4) | 5; Europe wants 1 (2 if the second `$match` is missing) |
| Children with deeds this season (Steps 2, 5) | 26: 20 nice, 6 naughty, **one on exactly 0** |
| Verdicts that flip if the season is ignored (Step 5) | Sofia, Hemi, Oliver |
| Top five kind deeds (Step 6) | 10, 9, 8, 7, 6 — all different, so no ties; a +12 from last season beats them all |
| Open lines / lines surviving `$lookup` (Step 7) | 50 / 48 — two lines ask for a toy not in the catalogue |
| Documents examined, no index / with index (Steps 8–9) | 46 / 22 (22 returned both times) |

### Stretch — none

The brief lists no stretch badge for Part 2. Its Level up still pairs Step 9 with Part 1's Indexing
Design Fundamentals stretch, and adds the "badly ordered vs well ordered" comparison from the
brief's Santa-mapping ideas. The brief's optional extra (a change stream powering a live order
board) is **not** built: it isn't assessed, and every other snippet in this part was run before it
was written down.

---|---|---|---|
| Define the aggregation framework and explain why stage order matters | — | — | ⬜ |
| Explain and build `$match` → `$unwind` → `$group` → `$project` | — | — | ⬜ |
| Explain and build `$match` → `$sort` → `$limit` | — | — | ⬜ |
| Use `$lookup` and `$set` | — | — | ⬜ |
| Access `explain()` plans in the MongoDB Shell and Compass | — | — | ⬜ 🖐️ |

> ⚠️ The `explain()` objective is partly a **Compass UI** task. xUnit cannot prove a
> learner clicked through Compass. Plan: test the shell/driver equivalent
> automatically, and cover the Compass half with exact click-by-click instructions
> plus a checklist item. Marked 🖐️ so the gap is visible rather than pretended away.

---

## Part 3A — Finding the Perfect Gift: Search
**Anchor badge:** Search Fundamentals · **Level:** Intermediate · **Release:** Dec 9

**Status: ✅ complete.** 5/5 objectives covered, 28 tests, all red on the starter code and green
on the published solutions. Verified 2026-11-24. Two things are 🖐️ rather than tested: the Atlas UI half of
objective 4, and the *planning* half of objective 2 — see the notes below the table.

| Objective | Step | Test | Status |
|---|---|---|---|
| Define Atlas Search, its use cases, and the basic steps | Steps 1–2 · `CreateDynamicIndexAsync`, `SearchDescriptionsAsync` (and the "What is Atlas Search?" section) | `Step01_CreatesADynamicIndex`, `…_WaitsUntilTheIndexIsQueryable`, `Step02_FindsTheWordInDescriptions`, `…_ReturnsResultsBestFirstWithAScore`, `…_NoMatchIsEmpty` | ✅ |
| Understand ranking; plan Search features before building | "Plan first" section, Step 8 · `RankedAsync` | `Step08_ANameMatchOutranksADescriptionMatch`, `…_FindsTheSameToysAsAPlainSearch`, `…_ScoresShowHowGoodTheMatchIs`; the plan is proven through Step 5's tests | ✅ 🖐️ |
| Know when to use dynamic mappings | Steps 3–4 · `DescribeIndexAsync`, `FindInCategoryAsync` | `Step03_ADynamicIndexHasNoListedFields`, `…_AStaticIndexListsItsFields`, `Step04_EqualsWorksOnAStaticIndex`, `…_TheSameQueryFindsNothingOnADynamicIndex`, `…_UnknownCategoryIsEmpty` | ✅ |
| Create indexes with static mappings, and mappings for multiple data types | Steps 5–6 · `CreateStaticIndexAsync`, `ToysUnderPriceAsync` | `Step05_CreatesAStaticIndex`, `…_MapsEachFieldWithTheRightType`, `…_TheIndexDoesWhatTheMappingPromises`, `Step06_FindsToysUnderThePriceDespiteTheTrap`, `…_ALargeLimitKeepsEveryMatchBestFirst`, `…_TheLimitReallyCuts` | ✅ 🖐️ |
| Create a Search query | Steps 2, 7, 9, 10 · `SearchDescriptionsAsync`, `GiftIdeasAsync`, `FuzzySearchAsync`, `SuggestAsync` | `Step07_SpaceToysForASixYearOld`, `…_LeavesOutDiscontinuedToys`, `…_FiltersStockAndAge`, `Step09_ForgivesATypo`, `…_FindsAMisspeltName`, `…_StillFindsRealWordsAndStillRejectsNonsense`, `Step10_CompletesAHalfTypedWord`, `…_CompletesAnyWordInTheName`, `…_HonoursTheLimitAndIgnoresCase` | ✅ |

> **The Atlas UI half of objective 4** is 🖐️: xUnit can't prove a learner clicked through Atlas. Index creation
> in the **driver** is tested (Step 5 builds the index and the tests query it). The UI half is covered by
> click-by-click instructions in Step 5 (Atlas: **Search & Vector Search** → **Create Search Index** → Visual or
> JSON Editor → **Next** → **Create Search Index**; Compass: **Indexes** → **Search Indexes** → **Create
> Index**) and a self-tick row on the README's badge checklist. Those labels come from the current MongoDB
> documentation. They have **not** been clicked through by a person on this machine — worth doing once
> before release, and the Compass route against *Atlas Local* in particular.
>
> **The planning half of objective 2** is taught by the "Plan first" table and **proven only
> indirectly**: Step 5's tests check that the index has every field and type the plan names. A test
> can't judge whether a learner *thought* first.
>
> **The Decimal128 trap** is not a badge objective, but it is the thing most likely to cost a learner an
> afternoon, so it is taught in five places (the top-of-page warning, a dedicated section, Step 5, Step 6,
> and a checklist row) and **guarded by a test**: Step 6's first test asserts the trap still exists on this
> server (a `decimal` in a range query throws, a `double` finds nothing) *before* it tests the learner's
> code, so a server that learns to index Decimal128 turns it red instead of letting the README go stale.

**Seed data check** — verified 2026-11-24 against `toys.json` (the same 34 toys as Part 1; nothing was
added for this part). The facts the steps rely on:

| Fact the steps rely on | Value |
|---|---|
| Toys whose **description** has the word "dinosaur" | 1 on a dynamic index (the singular), **2** with the English analyzer (the Brachiosaurus game says "dinosaurs") |
| Toys tagged `dinosaur` | 8: six have it *only* as a tag |
| Puzzles by `equals` on `category` (Step 4) | 3 on the static index, **0** on the dynamic one |
| Toys mentioning "space" | 7; 2 of them cost £15 or less (Step 6) |
| Gift ideas for "space", age 6 (Step 7) | 4 of the 7 (others: wrong age, discontinued or out of stock) |
| Magnetic Fishing Game | in stock **and** discontinued **and** suits a five-year-old: the toy that proves `mustNot` matters |
| "wooden", unboosted vs name ×5 (Step 8) | Skipping Rope, Wooden Handles is **5th**, then **2nd** |
| "family" (Step 8) | six toys tie on exactly the same score |
| Autocomplete "wo" (Step 10) | 4 names, one of them on the *last* word ("Workshop") |

Measured while writing this part, and relied on by the README:

| Behaviour | Result |
|---|---|
| `range` on `price` (Decimal128), query value a C# `decimal` | server error: `"range.lte" Type is not supported: DECIMAL128` |
| …query value cast to `double` | no error, **no results** — the stored values are not in the index |
| `range` on a field that really is a `double` | works |
| `equals` on a string field under a **dynamic** index | no error, no results |
| `equals` on a `token` field | works; the same `token` field also supports `$searchMeta` facets |
| Time for a new search index to become `queryable`, 34 documents, Atlas Local 8.0.15 | ~0.1–0.3 s; the first query after `queryable` saw all 34 documents in 12 of 12 trials |

### Stretch — none

The brief lists no stretch badge for Part 3A.

---

## Part 3B — Finding the Perfect Gift: Vector Search
**Anchor badge:** Vector Search Fundamentals · **Level:** Intermediate · **Release:** Dec 13

**Status: ✅ complete.** 6/6 objectives covered, 44 tests, all red on the starter code and green on the published
solutions. Verified 2026-11-24 against **real `voyage-3.5` vectors**, generated with `tools/embed`. Two things are
🖐️ rather than tested: the Atlas UI half of objective 3, and the Voyage call (Step 7), which is tested only through
a stand-in for the network.

| Objective | Step | Test | Status |
|---|---|---|---|
| Define vectors, how they work, and the purpose of dimensions | Steps 1–2 · `DescribeEmbeddingAsync`, `Compare` (and "What is vector search?") | `Step01_ReportsTheNumberOfDimensions`, `…_ReportsTheMagnitude`, `…_UnknownToyIsAnError`, `Step02_SameDirectionDifferentLength`, `…_KnownAngles`, `…_RealVectors`, `…_DifferentLengthsAreRejected` | ✅ |
| Explain how Atlas and an embedding model interact; HNSW at a high level | Steps 3, 7, 8 · `ClosestByHandAsync`, `EmbedAsync`, `FindGiftsAsync` (and "Who does what?", "Why a vector index at all?") | `Step03_FindsTheClosestToys`, `…_AToyIsItsOwnNearestNeighbour`, `…_HonoursTheCount`, `Step07_SendsTheRightRequest`, `…_PassesTheInputTypeOn`, `…_ReturnsTheVector`, `…_ReportsAFailureWithoutLeakingTheKey`, `…_RejectsAVectorOfTheWrongLength`, `Step08_EmbedsTheQuestionThenSearches`, `…_AnAgeNarrowsTheSearch`, `…_NeedsAnEmbedder` | ✅ 🖐️ |
| Create a vector search index with the correct number of dimensions | Step 4 · `CreateVectorIndexAsync` | `Step04_DeclaresTheRightNumberOfDimensions`, `…_TheIndexAnswersQueries`, `…_DeclaresTheFilterFields` | ✅ 🖐️ |
| Understand similarity options (euclidean, cosine, dot product) | Steps 2, 4 · `Compare`, `CreateVectorIndexAsync` | `Step02_…`, `Step04_DefaultsToCosine`, `Step04_ScoresWithTheChosenSimilarity` (× 3) | ✅ |
| Write a `$vectorSearch` aggregation query | Steps 5, 6, 8 · `FindSimilarAsync`, `GiftsForAgeAsync`, `FindGiftsAsync` | `Step05_MatchesTheBruteForceAnswer`, `…_AToyIsItsOwnNearestNeighbour`, `…_TheScoreIsTheRescaledCosine`, `…_HonoursTheCount`, `Step06_OnlyReturnsSuitableToys`, `…_FilteringDoesNotChangeAScore` | ✅ |
| Understand how prefiltering affects performance | Step 6 · `GiftsForAgeAsync` | `Step06_APreFilterKeepsLookingUntilItHasEnough`, `…_OnlyReturnsSuitableToys`, `…_NothingMatchesTheFilter` | ✅ |

> **Meaning is tested on the real vectors.** `Step08_MeaningTests` asks each example question and checks the
> right toy comes first: bedtime → Triceratops Night Light, palaeontologist → Dinosaur Dig Kit, rainy-day game →
> Snakes and Ladders, mess and art → Finger Painting Set, night sky → Constellation Projector, small baby →
> Stegosaurus Soft Toy, build with my hands → Wooden Tool Bench. (An eighth question, *a present for someone who
> loves the outdoors*, finds a torch, a glider and a telescope: no clear winner, so it's used in the README as an
> honest example of vector search being good, not magic, and has no test.) They assert the **order**, never the exact
> scores, so a different model changes numbers, not the idea.
>
> **The Atlas UI half of objective 3** is 🖐️, as in Part 3A: the path is the same (**Search & Vector Search** →
> **Create Search Index**, choosing the **Vector Search** type), but the *Vector Search* labels were not
> separately checked against the docs, and nobody has clicked through it.
>
> **The Voyage call (Step 7)** is tested through a stand-in for the network: it checks what `EmbedAsync`
> sends (endpoint, `Authorization: Bearer`, model, `input_type`, `output_dimension`) and how it handles a
> reply, an error and a wrong-length vector. It has never talked to the real API.
>
> **The dimensions trap** is not a badge objective but is the likeliest silent failure, so it's taught in the
> top-of-page warning, a dedicated section, Step 4 (a fix-the-bug with a shell recipe) and a checklist row,
> and **guarded**: Step 4's test failure message spells out both layers.

**What was measured** (Atlas Local 8.0.15; the first rows were measured on placeholder vectors, and re-checked on the real ones):

| Behaviour | Result |
|---|---|
| Index declares 1536 dimensions, vectors are 1024, query has 1024 | status **READY**, `queryable`; error: `vector field is indexed with 1536 dimensions but queried with 1024` |
| …same index, query padded to 1536 | **no error, no results** |
| Stored vectors of the wrong length, or missing, or null | skipped silently |
| Wrong index name | no results, no error |
| `filter` on a field not declared `filter` | error: `Path 'cat' needs to be indexed as filter` |
| `numCandidates` < `limit` | error: `limit should be less than or equal to numCandidates` |
| `numCandidates` > 10000 | error: `"numCandidates" must be within bounds [1..10000]` |
| No `numCandidates` and no `exact` | error: `numCandidates is required for approximate vector search` |
| `exact: true` | works, no `numCandidates` |
| Score, cosine / euclidean / dotProduct | `(1+cos)/2` / `1/(1+d²)` / `(1+dot)/2` (checked with 2-D vectors and, in Step 4's tests, on every hit) |
| Pre-filter vs search-then-`$match` (2-D example) | pre-filter returned `limit` results; post-filter returned fewer |
| `quantization: "scalar"` and `"binary"` (1024 dims) | both build and answer queries; scores differ slightly under scalar |
| Time for a 1024-dim vector index over 34 documents to be `queryable` | well under a second |

**Tool facts relied on (Voyage docs):** `POST https://api.voyageai.com/v1/embeddings`, `Authorization: Bearer`,
`input_type` ∈ {`null`, `query`, `document`}, `voyage-3.5` default **1024** with `output_dimension` ∈ {2048, 1024,
512, 256}, response `data[].embedding` / `data[].index`.

**Measured on the real vectors (2026-11-24):** every vector from `tools/embed` has a norm of **1.0000** (min and max),
so cosine and dot product rank identically, and the README says so as a measurement, not a promise. Exact and approximate
search agree for the bedtime question over 34 toys. Scalar and binary quantization keep the order (night light 0.7715 plain
and binary, 0.7711 scalar). Pre-filter vs post-filter with the Pocket Telescope's vector for age 3, five wanted: **5 vs 1**.

**Not stated in the docs, so not claimed:** that Voyage vectors are normalised (measured, not promised), which similarity
Voyage recommends. **Documented inconsistently:** free tokens for `voyage-3.5` (the rate-limits page says 200 million per
model; the billing page lists `voyage-3.5` under older models with none). **Atlas-created keys use a different host:**
`ai.mongodb.com`, not `api.voyageai.com` (`tools/embed` reads `VOYAGE_BASE_URL`; found when the first real run got a 403).

### Stretch — Vector Search Performance (not assessed by the anchor badge)

Taught only in Part 3B's **Level up**, and only in part. Marked honestly: the stretch badge is not covered
by tests, and several of its objectives are a paragraph of thinking, not a built step.

| Objective | Where | Status |
|---|---|---|
| How embeddings are created (generate with Voyage AI) | Step 7 (`EmbedAsync`) and `tools/embed`; the call is tested through a stand-in only | 🚧 taught, never run against the live API |
| Compare database options for vector search | — | ⬜ not covered |
| Reranking, and when to use it | Level up, "Rank again": a paragraph, not built | 🚧 concept only |
| Choosing embedding models by workload / data type / quality / latency / cost | — | ⬜ not covered |
| `voyage-context-3` for chunking challenges | — | ⬜ not covered (chunking arrives with Part 4A) |
| Effects of dimensionality on quality, latency and cost | Level up: "Fewer dimensions" (needs a Voyage key) and "Compress the vectors" (scalar/binary quantization, run locally) | 🚧 quantization run; fewer-dimensions needs a key and was not run |
| API-generated vs. database-managed auto-embeddings | Level up, "Who makes the vectors?": questions to think through | 🚧 concept only |
| Using Voyage reranking models | — | ⬜ not built: the rerank API wasn't read or run |

---|---|---|
| How embeddings are created (generate with Voyage AI) | — | ⬜ |
| Compare database options for vector search | — | ⬜ |
| Reranking, and when to use it | — | ⬜ |
| Choosing embedding models by workload / data type / quality / latency / cost | — | ⬜ |
| `voyage-context-3` for chunking challenges | — | ⬜ |
| Effects of dimensionality on quality, latency and cost | — | ⬜ |
| API-generated vs. database-managed auto-embeddings | — | ⬜ |
| Using Voyage reranking models | — | ⬜ |

---

## Part 4A — Santa's Elf Assistant: RAG
**Anchor badge:** RAG with MongoDB · **Release:** Dec 17

| Objective | Step | Test | Status |
|---|---|---|---|
| Define RAG and its use cases | — | — | ⬜ |
| LLM limitations and how RAG addresses them | — | — | ⬜ |
| Atlas Vector Search RAG architecture components | — | — | ⬜ |
| Suitable structured and unstructured sources | — | — | ⬜ |
| Implement data collection, preparation and ingestion | — | — | ⬜ |
| Chunk overlap | — | — | ⬜ |
| A prefiltered retriever | — | — | ⬜ |
| A generator separate from the embedding model | — | — | ⬜ |

> The last one is free: the generator is Gemini and the embedding model is Voyage.
> Part 4A's README names this explicitly rather than leaving it implicit.

---

## Part 4B — Santa's Elf Assistant: Agents
**Anchor badge:** AI Agents with MongoDB · **Release:** Dec 17

| Objective | Step | Test | Status |
|---|---|---|---|
| Define an agent's reasoning, tools and memory | — | — | ⬜ |
| Plan an agent's tasks and required data | — | — | ⬜ |
| Retrieval tools | — | — | ⬜ |
| Tools in general, and LLM function calling | — | — | ⬜ |
| Cyclical graphs | — | — | ⬜ |
| Short- vs. long-term memory | — | — | ⬜ |
| Implement short-term agent memory with MongoDB | — | — | ⬜ |

> The badges may be framed around other frameworks (Python, LangGraph). Part 4B needs
> a short conceptual bridge explaining how Microsoft Agent Framework handles the agent
> loop. **Long-term memory is conceptual only in the objectives** — it stays an
> optional bonus step.
