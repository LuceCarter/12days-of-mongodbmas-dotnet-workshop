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
**Anchor badge:** Search Fundamentals · **Release:** Dec 9

| Objective | Step | Test | Status |
|---|---|---|---|
| Define Atlas Search, its use cases, and the basic steps | — | — | ⬜ |
| Understand ranking; plan Search features before building | — | — | ⬜ |
| Know when to use dynamic mappings | — | — | ⬜ |
| Create indexes with static mappings, and mappings for multiple data types | — | — | ⬜ |
| Create a Search query | — | — | ⬜ |

> Index creation is shown **both** in the C# driver and in the Atlas UI, because the
> assessment may use the UI. The UI half is 🖐️.
> Remember the ~20s index build delay — fixtures must poll for `queryable`.

---

## Part 3B — Finding the Perfect Gift: Vector Search
**Anchor badge:** Vector Search Fundamentals · **Release:** Dec 13

| Objective | Step | Test | Status |
|---|---|---|---|
| Define vectors, how they work, and the purpose of dimensions | — | — | ⬜ |
| Explain how Atlas and an embedding model interact; HNSW at a high level | — | — | ⬜ |
| Create a vector search index with the correct number of dimensions | — | — | ⬜ |
| Understand similarity options (euclidean, cosine, dot product) | — | — | ⬜ |
| Write a `$vectorSearch` aggregation query | — | — | ⬜ |
| Understand how prefiltering affects performance | — | — | ⬜ |

### Stretch — Vector Search Performance
| Objective | Step | Status |
|---|---|---|
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
