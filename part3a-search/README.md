# Part 3A — Finding the Perfect Gift: Search

> **Level:** Intermediate. Parts 1 and 2 help, but all you really need is the catalogue from Part 1.
> **Time:** About an hour for the core path.
> **Badge:** Search Fundamentals

> ⚠️ **Heads up: one step in this part has a trap that catches nearly everyone.** Atlas Search can't
> index **Decimal128**, the type our `price` is stored as, and it will not tell you so. Read
> [the trap](#the-trap-atlas-search-cant-see-decimal128) before Step 5, and you'll meet it for real in Step 6.

---

## The story

The orders are sorted. The elves know what to make, where it's going, and who's been nice. For nearly a
whole afternoon the workshop is peaceful.

Then the letters start asking *questions*.

*"Do you have anything with dinosaurs?"* *"Something for a six-year-old who loves space. Not too
expensive."* *"A **dinosor** for my nephew."* And one, in careful crayon, that just says *"wooden."*

At the help desk, a tired elf is typing each of these into the catalogue, which only knows how to
match a toy's name *exactly*. "Dinosaur" finds no toy called Dinosaur. "dinosor" finds nothing at all.
The elf has started answering from memory, and the memory is wrong about the Stegosaurus.

Mrs Claus pushes her glasses up her nose. "It needs to **search**," she says. "Like a person does. It
should know that dinosaurs are what the Triceratops *is*. It should forgive a typo. It should put the
best answer first. And it should finish the word when the elf has only typed half of it."

You are going to build the Gift Finder.

---

## Get the code

Part 3A lives in the same repository as Parts 1 and 2:

**[github.com/LuceCarter/12days-of-mongodbmas-dotnet-workshop](https://github.com/LuceCarter/12days-of-mongodbmas-dotnet-workshop)**

If you already have it, skip to the next section. If not, get it either way:

- **Codespaces (easiest).** On the repository page choose **Code → Codespaces → Create
  codespace on main**. Everything is installed and the terminal opens in the right folder.
- **On your machine.** You'll need the [.NET 10 SDK](https://dotnet.microsoft.com/download)
  and [Docker](https://www.docker.com/products/docker-desktop/). Then clone it:

```bash
git clone https://github.com/LuceCarter/12days-of-mongodbmas-dotnet-workshop.git
cd 12days-of-mongodbmas-dotnet-workshop
```

**Run every command in this challenge from that folder** — the one containing
`docker-compose.yml`. The files you'll work with:

```
part3a-search/
  README.md                   this page
  Starter/ToySearch.cs        ← you write code here
  Tests/                      the tests that tell you when a step is right
  SOLUTIONS.md                every answer, with the reasoning
```

---

## Before you start

Make sure your environment is ready. From the repository folder:

```bash
docker compose up -d
dotnet run --project tools/doctor
```

This part needs **Atlas Search**, which a plain MongoDB container doesn't have. The Atlas Local
container in our `docker-compose.yml` does, and the doctor checks that it's working. If anything is red,
it tells you how to fix it. Full instructions are in [docs/setup.md](../docs/setup.md).

You'll also want something to explore in the shell, Compass and Atlas. Load the practice data now — it's safe to run again whenever you like:

```bash
dotnet run --project tools/seed
```

The **tests don't need this**. They use their own database, `santas_workshop_part3a_tests`, so
running them never overwrites the data you're exploring.

---

## How this works

Everything you write goes in
[**`part3a-search/Starter/ToySearch.cs`**](Starter/ToySearch.cs).
It has ten methods. Nine are stubs — a `// TODO` and a `throw new NotImplementedException` — and **one is
already written but wrong**. Finding out why is Step 6. A helper, `WaitUntilQueryableAsync`, is also
already written, and you'll use it twice.

Run the tests whenever you like:

```bash
dotnet test part3a-search/Tests
```

They start **red**. Each step turns some of them green. When all 28 pass, you're done.

Stuck? [`SOLUTIONS.md`](SOLUTIONS.md) has every answer with an explanation. Looking isn't
cheating — reading a worked example is how most of us learn this.

---

## What is Atlas Search?

So far you've asked MongoDB for documents that **match** something: `price < 10`, `category == "puzzle"`.
That's **exact**. A match either is or isn't.

**Atlas Search** is **full-text search**, built into the same database. Instead of *"does it match?"* it
asks *"how well does it match?"*, and hands the results back **best first**, each with a **score**. It
understands that words have plurals, that people make typos, and that "dinosaur" appears in a lot of
places. You use it when a person types words into a box.

Typical uses: a shop's search box, a documentation site, "did you mean…", autocomplete, finding
similar things, and filtering a result list by category with counts beside each one (facets).

You don't run a second database for it, and you don't copy your data anywhere. Atlas Search keeps its
own **search index** alongside your collection, and keeps it up to date as the collection changes.

**The basic steps are always the same three:**

1. **Create a search index.** This says what to make searchable, and how.
2. **Wait until it is ready.** An index is built in the background.
3. **Query it** with the `$search` aggregation stage.

That is Steps 1 and 2. Everything after is the same three steps with better choices at each one.

Three things to carry through the rest of this page:

| | |
|---|---|
| `$search` must be the **first stage** of a pipeline. | Anything that narrows or reshapes the results comes after it. |
| A search index is **not** a regular index. | You create it with `createSearchIndex`, and list it with `getSearchIndexes`. The `Indexes` that Part 2 made are a different thing. |
| Without `index: "name"`, `$search` looks for an index called **`default`**. | If you've named yours anything else and forget, you get an **empty result and no error**. |

---

## The trap: Atlas Search can't see Decimal128

Every toy has a `price`, and it's stored as a **Decimal128**, on purpose: money and binary floating point
don't mix. That was a good decision in Part 1. In Part 3A it is a problem, because:

> **Atlas Search's `number` type indexes `int32`, `int64` and `double`. It does not index Decimal128.**

And it fails in **two layers**, the second much nastier than the first:

| What you do | What happens |
|---|---|
| Map `price` as `{ type: "number" }` | ✅ Accepted. The index builds. The status says **ready**. Every price is quietly **left out**. |
| Pass the query a `decimal`: `Range(t => t.Price, Lte(15m))` | ❌ `"range.lte" Type is not supported: DECIMAL128`. This is the helpful one. |
| Cast it to a `double` to make that go away: `Lte((double)15m)` | 🫥 **No error. No results. No clue.** There's nothing in the index for it to match. |

You will hit this in **Step 6**, where an elf has already walked into the second layer for you. The
way out isn't to fight the type system. It's to let each stage do what it's good at:

- `$search` finds and ranks the toys.
- A `$match` **after** it narrows by price, because `$match` reads the stored value, and MongoDB
  compares Decimal128 correctly.

Keep that shape in mind whenever a `range` or `equals` returns nothing you expected: **check the type
of the field in the data against the types the mapping supports.**

---

## Plan first

Before you write an index, decide what people will actually do with it. An index built without a plan
either misses a feature, or is full of fields nobody searches. It's much cheaper to think now than to
rebuild later.

Here is the Gift Finder's plan. For each thing a user can do, it names the field and the type that makes
it work:

| A user can… | Field | Type in the index | Why that type |
|---|---|---|---|
| search words in a toy's **description** | `description` | `string`, **English analyzer** | so "dinosaurs" finds "dinosaur" |
| search a toy's **name**, as words | `name` | `string` | the ordinary one |
| …and see suggestions **as they type** the name | `name` | `autocomplete` | one field, **two** types |
| search a toy's **tags** | `tags` | `string` | each tag is a word to find |
| pick an **exact category** | `category` | `token` | the whole value, as one unit |
| ask for a **price limit** | `price` | `number` | ⚠️ **but see [the trap](#the-trap-atlas-search-cant-see-decimal128)** |
| ask for toys that suit an **age** | `minAge`, `maxAge` | `number` | whole numbers: fine |
| hide **out-of-stock** and **discontinued** toys | `inStock`, `discontinued` | `boolean` | yes or no |

And what the plan **doesn't** include: `stockCount`. Nobody searches by it, so a *static* index won't
carry it. Leaving fields out is what makes a static index smaller and faster than a dynamic one.

That table is Step 5. And **ranking** is the other half of planning: every `$search` gives each result a
**score**, and you can influence it. Step 8 is where. Decide *which* field a match matters most in,
before you write the query.

---

## The data

The `toys` collection is the one from Part 1. Here's what matters for search:

```csharp
public sealed class Toy
{
    public string Name { get; set; }             // "Dinosaur Dig Kit"
    public string Description { get; set; }      // "Chip a plaster block apart … a complete toy dinosaur skeleton."
    public List<string> Tags { get; set; }       // ["dinosaur", "science", "digging"]
    public string Category { get; set; }         // "craft"  (one of seven)
    public decimal Price { get; set; }           // 18.50   ← stored as Decimal128. ⚠️
    public int MinAge { get; set; }              // 6
    public int MaxAge { get; set; }              // 10
    public bool InStock { get; set; }
    public bool Discontinued { get; set; }
    public int StockCount { get; set; }
}
```

There are 34 toys. A few things about them are deliberate:

- **"dinosaur" is everywhere, but in different ways.** One description says "dinosaur". Another says
  "dinosaurs". Six toys have it only as a tag. That's what Steps 2, 5 and 8 pull apart.
- **Some toys are out of stock, and some are *discontinued*.** They are not the same, and one toy is in stock *and* being retired.
- **Toys suit different ages.** Step 7 asks which suit a six-year-old.

---

## Part A — Your first search index

### Step 1 — Create a dynamic index · *objective 1*

Implement `CreateDynamicIndexAsync`. An index **definition** is just a document, and the shortest one
there is says *"index every field you find, and work out its type yourself"*:

```csharp
var definition = new BsonDocument("mappings", new BsonDocument("dynamic", true));

await _toys.SearchIndexes.CreateOneAsync(new CreateSearchIndexModel(_dynamicIndex, definition));
await WaitUntilQueryableAsync(_dynamicIndex);
```

`mappings` is how the index maps your fields. `dynamic: true` means *all of them*, automatically.

> 🧠 **Creating an index returns immediately, long before the index exists.** Query it in that gap and
> you get an **empty result and no error**, which looks exactly like "nothing matched". So: create, **wait**,
> then query. `WaitUntilQueryableAsync` is already written for you. It asks the server whether the index
> is `queryable`, rather than guessing with a delay.
> On the local database this takes a fraction of a second. On an **Atlas free cluster it takes about a
> minute**. Waiting on `queryable` is right for both. `Task.Delay(5000)` is right for neither.

> **Shell:** `db.toys.createSearchIndex("toys-dynamic", { mappings: { dynamic: true } })`

---

### Step 2 — Your first `$search` · *objectives 1 and 5*

Implement `SearchDescriptionsAsync`: toys whose **description** contains the words, best first.

```csharp
return await _toys.Aggregate()
    .Search(Builders<Toy>.Search.Text(t => t.Description, words), indexName: _dynamicIndex)
    .Project<ToyHit>(HitProjection)
    .ToListAsync();
```

`Search(...)` adds the `$search` stage. Print the query and you'll see what the shell would show:

```js
{ $search: { index: "toys-dynamic", text: { query: "dinosaur", path: "description" } } }
```

`text` is the **operator**: it says *what kind of search*. `path` says *where*. `HitProjection` asks for
the toy's name and its **score**, which comes from `{ $meta: "searchScore" }`.

Search for `"dinosaur"` and you get **one** toy back, the Dinosaur Dig Kit. Where are the dinosaurs?
Hold that thought. Steps 3 to 5 are the answer.

> 🧠 **You never sorted anything.** Atlas Search returns results by **score**, best first. That's the
> main difference from a `Find()`: there you filter, then sort. Here the database also tells you *how
> well* each one matched.

> **Shell:** `db.toys.aggregate([ { $search: { index: "toys-dynamic", text: { query: "dinosaur", path: "description" } } } ])`

---

## Part B — Dynamic or static?

### Step 3 — What did the index map? · *objective 3*

Implement `DescribeIndexAsync`. It reads an index's definition back, and reports whether it's dynamic
and which fields it names:

```csharp
var indexes = await (await _toys.SearchIndexes.ListAsync(indexName)).ToListAsync();
var index = indexes.Single();
var mappings = index["latestDefinition"]["mappings"].AsBsonDocument;
```

Run it on `toys-dynamic` and look at the answer: **no fields at all**. Nothing is listed because nothing
was decided. A dynamic index maps whatever it meets, and guesses the type.

That's the whole trade, and it tells you when to use which:

| | Dynamic | Static |
|---|---|---|
| You write | one line | a line per field |
| Good for | exploring, prototypes, documents whose fields vary | anything you ship |
| Types | guessed | chosen by you |
| Analyzers, `token`, `autocomplete` | not available | available |
| Index size | everything, including what you never search | only what you list |

> **Use dynamic** when you're finding your feet, or your documents' shapes aren't known in advance.
> **Use static** once you know what your users search, which is nearly always the moment you ship.

> **Shell:** `db.toys.getSearchIndexes("toys-dynamic")`

---

### Step 4 — Something the dynamic index can't do · *objective 3*

Implement `FindInCategoryAsync(category, indexName)`: every toy in a category, by an **exact** match. The
operator is `equals`:

```csharp
Builders<Toy>.Search.Equals(t => t.Category, category)
```

Now run it twice, with the same query: once against `toys-dynamic`, and (once Step 5 exists) once
against `toys-static`. Ask for `"puzzle"`. The dynamic index finds **nothing**. There are three puzzles.

The query isn't wrong. The *index* can't answer it. A dynamic index maps a string as `string`, which is
cut into words for full-text search. `equals` wants one **exact** value, and that is what the `token` type
stores. Step 5 maps `category` as `token`, and the very same query then works.

> 🧠 **When a `$search` returns nothing you expected, look at the index first.** Same query, same data,
> different answer, and the whole difference was in the mapping. That is why dynamic is for exploring,
> and static is for shipping.

> **Shell:** `{ $search: { index: "toys-static", equals: { path: "category", value: "puzzle" } } }`

---

## Part C — A static mapping, and the types it can hold

### Step 5 — Build the real index · *objective 4*

Implement `CreateStaticIndexAsync`, turning [the plan](#plan-first) into a definition. Same three moves
as Step 1 (definition, create, wait), but `dynamic` is **off** and you list the fields:

```js
{ mappings: { dynamic: false, fields: {
    name:         [ { type: "string" }, { type: "autocomplete" } ],
    description:  { type: "string", analyzer: "lucene.english" },
    ...                                   // the rest are in the plan table
} } }
```

Name the index `_staticIndex` (it's `toys-static`). A few things are worth noticing as you write it:

- **One field can have several types.** `name` is a list, so it can be searched as ordinary words *and*
  completed as you type.
- **An analyzer decides how text becomes searchable words.** `lucene.english` drops "the" and "a" and
  **stems**: "dinosaurs" and "dinosaur" become the same word. Once it's in, search for `"dinosaurs"` and the
  Brachiosaurus game, which says "dinosaurs", turns up.
- **`token` keeps a value whole.** `string` cuts "board game" into two words, and `token` keeps it as one.
- **`price` is `number`.** The plan says so, and the server won't argue.
  Hold on to that, because ⚠️ **`price` is stored as Decimal128, which a `number` can't hold.** The
  index will build and report *ready*. Step 6 is where you find out what it left out.

#### 🖐️ Make the same index in the Atlas UI and in Compass

The badge may ask you to create an index in the UI as well as in code, and a test can't click through
it for you, so this part is on the checklist below. Make a second index, on your **practice** data
(`dotnet run --project tools/seed` first), and call it `toys-ui`.

**In Atlas (cloud).** On an Atlas free cluster the steps are:

1. In the left sidebar choose **Search & Vector Search**, pick your cluster in **Select data source**,
   and click **Go to Atlas Search**.
2. Click **Create Search Index**.
3. Choose the **Search** index type (not Vector Search), name it `toys-ui`, and pick the `santas_workshop`
   database and the `toys` collection.
4. Under **Configuration Method** choose **JSON Editor**, and paste the definition from above. (The
   **Visual Editor** builds the same thing with menus. Try both and compare the JSON.)
5. Click **Next**, then **Create Search Index**.
6. Watch the **Status** column: **Build in Progress**, then **Active**. That is the `queryable` you waited on in code.

**In Compass, against your local database.**

1. Connect to `mongodb://workshop:workshop@localhost:27017/?directConnection=true`.
2. Open `santas_workshop`, click the **`toys`** collection, then the **Indexes** tab.
3. Choose **Search Indexes**, then **Create Index**.
4. Give it a name, paste the definition into the editor, and click **Create Search Index**.

Same document, three doors: your C# code, the Atlas UI, and Compass. The index doesn't know the difference.

> **Shell:** `db.toys.createSearchIndex("toys-static", { mappings: { dynamic: false, fields: { ... } } })`

---

### Step 6 — Fix the elf's price filter · *objective 4*

An elf wrote `ToysUnderPriceAsync` — toys matching some words that cost no more than a limit — and went
home. It's already in the Starter file. It compiles, runs without errors, and returns an **empty list**,
every time.

The elf's first attempt passed the price straight in, and the server answered:

```
"range.lte" Type is not supported: DECIMAL128
```

So the elf wrote `(double)maxPrice`, the error went away, and the empty list took its place.

You've read [the trap](#the-trap-atlas-search-cant-see-decimal128). Now live it. The
text operator is fine and the index is fine. **Don't change either.** Change *where the price is checked*:
take it out of the `$search`, and put it in a `$match` that comes **after** it.

```csharp
.Search(search.Text(new[] { "name", "description", "tags" }, words), indexName: _staticIndex)
.Match(t => t.Price <= maxPrice)
```

Run the tests, and note what the first one's failure message says. It's the same warning, because
this is the one most likely to cost you an afternoon in real life.

> 🧠 **The shape to remember:** `$search` finds and ranks, then ordinary stages narrow. `$match` reads the
> *stored* value, which MongoDB compares correctly, Decimal128 included. Here the cost is nothing, because
> `$search` has already cut 34 toys down to a handful.
> **If you need the price *inside* the search**, store a second field, say `priceInPence` as an `int`, map
> *that*, and `range` works. The cost is a field to keep in sync. See Level up.
> What you shouldn't do is store money as a `double` to please the index. It makes the index happy and the till unhappy.

> **Shell:**
> ```js
> db.toys.aggregate([
>   { $search: { index: "toys-static", text: { query: "space", path: ["name", "description", "tags"] } } },
>   { $match: { price: { $lte: NumberDecimal("15") } } }
> ])
> ```

---

## Part D — Real queries, ranked

### Step 7 — Gift ideas for a particular child · *objective 5*

"Something for a six-year-old who loves space." Implement `GiftIdeasAsync(words, age)`. That is one
fuzzy wish and three hard rules, which is exactly what a **`compound`** operator is for:

```csharp
var query = search.Compound()
    .Must(search.Text(new[] { "name", "description", "tags" }, words))
    .Filter(search.Equals(t => t.InStock, true))
    .Filter(search.Range(t => t.MinAge, SearchRangeBuilder.Lte(age)))
    .Filter(search.Range(t => t.MaxAge, SearchRangeBuilder.Gte(age)))
    .MustNot(search.Equals(t => t.Discontinued, true));
```

| Clause | Must the toy match? | Does it change the score? |
|---|---|---|
| `must` | yes | **yes** |
| `should` | no, unless you ask | **yes**: matching raises it |
| `filter` | yes | **no** |
| `mustNot` | it must **not** | no |

The words go in `must`, because they decide *how well* a toy fits. "In stock", "the right age" and "not
discontinued" are yes-or-no questions that say nothing about how *good* a match is, so they're filters and
don't disturb the ranking.

A toy has an age **span**, so one age takes two ranges: it suits a six-year-old if `minAge <= 6` **and**
`maxAge >= 6`. Both are ints, which Atlas Search handles, unlike `price`.

> **Shell:**
> ```js
> { $search: { index: "toys-static", compound: {
>     must:    [ { text: { query: "space", path: ["name", "description", "tags"] } } ],
>     filter:  [ { equals: { path: "inStock", value: true } },
>                { range:  { path: "minAge", lte: 6 } },
>                { range:  { path: "maxAge", gte: 6 } } ],
>     mustNot: [ { equals: { path: "discontinued", value: true } } ] } } }
> ```

---

### Step 8 — Put the best answer first · *objective 2*

The crayon letter said *"wooden."* Which toy should come first? One of them is **called** the Wooden Tool
Bench. Another has "wooden" in a tag and a description. Implement `RankedAsync` so a match in a toy's
**name** counts **five times** as much as a match anywhere else:

```csharp
var boosted = new SearchTextOptions<Toy> { Score = new SearchScoreDefinitionBuilder<Toy>().Boost(5) };

var query = search.Compound()
    .Should(search.Text(t => t.Name, words, boosted))
    .Should(search.Text(new[] { "description", "tags" }, words))
    .MinimumShouldMatch(1);
```

Each `should` clause **adds** its score when it matches, and `MinimumShouldMatch(1)` says at least one must.
Run it for `"wooden"` and print the names and scores. Without the boost, the Skipping Rope, Wooden Handles is
**fifth**. With it, **second**, behind only the Tool Bench.

**Where does a score come from?** Atlas Search uses a formula called BM25. In plain words, a word counts
**more** when:

- it's **rare** across all your documents,
- it appears **often** in *this* one, and
- it sits in a **short** field.

That's why "dinosaur" scores higher on the Dinosaur Dig Kit, which has it in its name, tags *and*
description, than on the Pterodactyl Glider, which only has it as a tag.

Two things to get used to:

- **A score only means something next to other scores from the same query.** 3.2 isn't "good" and 0.6 isn't
  "bad". The ranking is what you use.
- **Ties are normal.** Search for `"family"`: six toys score exactly the same, because each has the word
  once, in a tag. Don't rely on the order *within* a tie.

> 🧠 **Ranking is a design decision, not a setting.** The search engine doesn't know that a name match
> matters more than a tag match: you do. Boosting is how you tell it. That's why [the plan](#plan-first)
> asks *which field matters most* before any code is written.

> **Shell:**
> ```js
> { $search: { index: "toys-static", compound: {
>     should: [ { text: { query: "wooden", path: "name", score: { boost: { value: 5 } } } },
>               { text: { query: "wooden", path: ["description", "tags"] } } ],
>     minimumShouldMatch: 1 } } }
> ```

---

## Part E — Typos and as-you-type

### Step 9 — Forgive a typo · *objective 5*

*"A dinosor for my nephew."* Implement `FuzzySearchAsync`: the same text search, with **fuzzy** matching on:

```csharp
var forgiving = new SearchTextOptions<Toy> { Fuzzy = new SearchFuzzyOptions { MaxEdits = 2 } };

search.Text(new[] { "name", "description", "tags" }, words, forgiving)
```

`fuzzy` lets a word be a few **single-letter edits** (a changed letter, a missing one, an extra one) away
from a real word. "dinasaur" is one letter from "dinosaur", so it matches. The same search **without**
`fuzzy` finds nothing.

`MaxEdits` is 1 or 2, and it's a trade: more forgiveness catches more typos, and also more accidents.
Short words suffer most: with two edits, "cat" is near "car", "cut" and "hat". The `PrefixLength` option
says the first *n* letters must be right, which is a cheap way to keep it sensible, and it makes the
search faster on a big collection.

> **Shell:** `{ $search: { index: "toys-static", text: { query: "dinasaur", path: ["name", "description", "tags"], fuzzy: { maxEdits: 2 } } } }`

---

### Step 10 — Finish the word · *objective 5*

Last request from Mrs Claus: when the elf has typed only `dino`, the search box should already be offering
*Dinosaur Dig Kit*. Implement `SuggestAsync`, with the **`autocomplete`** operator:

```csharp
var hits = await _toys.Aggregate()
    .Search(Builders<Toy>.Search.Autocomplete(t => t.Name, prefix), indexName: _staticIndex)
    .Limit(limit)
    .Project<ToyHit>(HitProjection)
    .ToListAsync();

return hits.Select(h => h.Name).ToList();
```

It works only because Step 5 gave `name` a second type, `autocomplete`. At *index* time, each word is
cut into every beginning (`ro`, `roc`, `rock`, `rocke`…), so at *query* time a half-typed word is simply
an exact match. The work moves to indexing, and the query is fast enough to run on every keystroke.

Try `"wo"`. It finds *Wooden* Tool Bench, *Woodland* Animal Dominoes, Skipping Rope, *Wooden* Handles and
Jigsaw: Santa's *Workshop*. It completes **any word in the name**, not just the first.

> **Shell:** `{ $search: { index: "toys-static", autocomplete: { query: "dino", path: "name" } } }`, then `{ $limit: 5 }`

---

## ✅ You're done

```bash
dotnet test part3a-search/Tests
```

28 green. The Gift Finder finds the dinosaurs, ranks the wooden toy first, forgives *dinosor*, and finishes
Mrs Claus's sentences.

The elf at the help desk reads the screen for a moment. "It found the Stegosaurus," they say. "It *is* a
dinosaur." Then, quietly, "I was wrong about the Stegosaurus."

---

## Badge checklist — Search Fundamentals

Every objective the badge assesses, and the step that teaches it.

| ✓ | Objective | Step | Taught by |
|---|---|---|---|
| ☐ | **1** Define Atlas Search, its use cases, and the basic steps | Steps 1, 2 | the page above, `CreateDynamicIndexAsync`, `SearchDescriptionsAsync` |
| ☐ | **2** Understand ranking; plan Search features before building | Plan first, Step 8 | the plan table, `RankedAsync` |
| ☐ | **3** Know when to use dynamic mappings | Steps 3, 4 | `DescribeIndexAsync`, `FindInCategoryAsync` |
| ☐ | **4** Create indexes with static mappings, and mappings for multiple data types | Steps 5, 6 | `CreateStaticIndexAsync`, `ToysUnderPriceAsync` |
| ☐ | **5** Create a Search query | Steps 2, 7, 9, 10 | `SearchDescriptionsAsync`, `GiftIdeasAsync`, `FuzzySearchAsync`, `SuggestAsync` |
| ☐ 🖐️ | **4** …and create the index in the **Atlas UI**: a test can't click, so tick this one yourself | Step 5 | the Atlas and Compass walk-through |
| ☐ ⚠️ | **4** …and know the **Decimal128 trap**: `number` doesn't index it, and nothing says so | Step 6 | `ToysUnderPriceAsync` |

**Do you have to finish the exercises to take the badge?** No — the badge is open and you can
take it whenever you like. But it assesses these concepts, so doing the steps first is the
easier route.

---

## 📋 From C# to the badge

The assessment is likely to show MongoDB Shell or Atlas UI syntax rather than C#. The query JSON is the
same either way, and the driver's builders print exactly it. The full set lives in
[docs/csharp-to-badge.md](../docs/csharp-to-badge.md).

| What you want | Shell / Atlas | C# |
|---|---|---|
| Create a dynamic index | `db.toys.createSearchIndex("n", { mappings: { dynamic: true } })` | `SearchIndexes.CreateOneAsync(new CreateSearchIndexModel("n", definition))` |
| Create a static index | `{ mappings: { dynamic: false, fields: { ... } } }` | the same definition as a `BsonDocument` |
| List indexes and their status | `db.toys.getSearchIndexes()` | `SearchIndexes.ListAsync()` |
| Words in a field | `{ $search: { text: { query, path } } }` | `Search.Text(t => t.Description, words)` |
| Several fields | `path: ["name", "description"]` | `Search.Text(new[] { "name", "description" }, words)` |
| Exact value | `{ equals: { path, value } }` (needs a `token`, `boolean` or number field) | `Search.Equals(...)` |
| Number or date range | `{ range: { path, gte, lte } }` | `Search.Range(..., SearchRangeBuilder.Lte(x))` |
| Combine clauses | `{ compound: { must, should, filter, mustNot } }` | `Search.Compound().Must(...).Filter(...)` |
| Boost | `score: { boost: { value: 5 } }` | `SearchTextOptions { Score = ...Boost(5) }` |
| Typos | `fuzzy: { maxEdits: 2 }` | `SearchTextOptions { Fuzzy = new SearchFuzzyOptions { MaxEdits = 2 } }` |
| As you type | `{ autocomplete: { query, path } }` | `Search.Autocomplete(...)` |
| The score | `{ $meta: "searchScore" }` | the same, in a `Project` |
| Use a named index | `index: "toys-static"` | `indexName: "toys-static"` |
| Create an index in the UI | **Search & Vector Search** → **Create Search Index** | — |

**Which type for which operator?** `text` and `autocomplete` need `string` and `autocomplete` mappings.
`equals` needs `token`, `boolean`, or a number. `range` needs `number` or a date. And **`number` is
`int32`, `int64` and `double` only: not Decimal128.**

---

## 🚀 Level up

Optional. None of this is assessed by the Search Fundamentals badge — do it if you're enjoying yourself.

### Count and group the results (facets)

Run these in the shell against the practice data, once you've made the `toys-static` index there too
(Step 5's walk-through makes `toys-ui`; use that name instead if you prefer):

```js
// How many toys mention "space"?
db.toys.aggregate([ { $searchMeta: { index: "toys-static", count: { type: "total" },
    text: { query: "space", path: ["name", "description", "tags"] } } } ])

// …and how are they spread across the categories?
db.toys.aggregate([ { $searchMeta: { index: "toys-static", facet: {
    operator: { text: { query: "space", path: ["name", "description", "tags"] } },
    facets:   { byCategory: { type: "string", path: "category" } } } } } ])
```

That is the "7 results — craft (2), electronic (2)…" sidebar on a shopping site. `$searchMeta` returns
the counts and **no documents**, and it is why `category` is a `token`.

### Fix the price properly

Give every toy a `priceInPence` (an `int`), map it as `number`, and move the price filter back inside
the `$search`:

```js
db.toys.updateMany({}, [ { $set: { priceInPence: { $toInt: { $round: [ { $multiply: ["$price", 100] }, 0 ] } } } } ])
```

Add `priceInPence: { type: "number" }` to the index and `{ range: { path: "priceInPence", lte: 1500 } }` to
a `filter`. It works: that's the second option from Step 6. What do you now have to remember to do when a price changes?

### See where a score comes from

Add `scoreDetails: true` to a `$search`, then project `{ $meta: "searchScoreDetails" }`. You get the whole
BM25 calculation for each result: which word, how rare, how long the field. Find the toy with the highest
score for `"space"` and work out why it beat the others.

### Phrases

`text` matches words anywhere. `phrase` wants them **next to each other**: `{ phrase: { query: "wooden board",
path: "description" } }` finds Snakes and Ladders, and adding `slop: 3` lets a few words come between.
When would you want that, and when is `text` kinder?

### Change an index

Mapped `stockCount` as a `number`? Drop the index (`db.toys.dropSearchIndex("toys-static")`) and make
it again, or **update** it in place (`db.toys.updateSearchIndex("toys-static", newDefinition)`), and watch the
status go back to building. Why does a *static* index make you do this, when a dynamic one doesn't?

---

## What's next

**Part 3B — Finding the Perfect Gift: Vector Search.** Search has found everything *containing the words*.
But a parent who writes *"something calming for a restless five-year-old at bedtime"* has used none
of them, and the Triceratops Night Light is exactly right. Next you'll teach MongoDB to search by
*meaning*.
