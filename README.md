# 🎄 12 Days of MongoDBmas — C#

**A festive, self-paced MongoDB challenge for C# developers.**

Five hands-on challenges that take you from your first `InsertOne()` to a working AI
agent. You'll be building **Santa's Workshop**: one story, one growing dataset, and a
MongoDB skill badge at the end of every part.

No prior MongoDB experience needed. Work through it whenever you like — this isn't a
daily puzzle, and nothing expires.

---

## The challenges

| Part | Released | What you'll build | Badge | Level |
|---|---|---|---|---|
| **1 — The Workshop Inventory** | Dec 1 | The toy catalogue: inserting, finding, updating, deleting, querying and counting | CRUD Overview (C# edition) | Beginner |
| **2 — The Naughty/Nice Ledger** | Dec 5 | Aggregation pipelines over this year's letters and orders | Fundamentals of Data Transformation | Beginner |
| **3A — Finding the Perfect Gift** | Dec 9 | Full-text search over the catalogue, with fuzzy matching and autocomplete | Search Fundamentals | Intermediate |
| **3B — Finding the Perfect Gift** | Dec 13 | Semantic search: "something for a 7-year-old who loves dinosaurs and space" | Vector Search Fundamentals | Intermediate |
| **4 — Santa's Elf Assistant** | Dec 17 | A RAG pipeline over the Elf Handbook, then an AI agent that uses it | RAG with MongoDB **and** AI Agents with MongoDB | Intermediate |

Each part takes **30–60 minutes** on the core path. Every part also has a **Level up**
section if you want to go further.

---

## How it works

1. **Get set up** — see [docs/setup.md](docs/setup.md). Codespaces is the quickest route.
2. **Read the part's README.** It tells the story and walks you through the steps.
3. **Fill in the `// TODO`s** in that part's `Starter/` project.
4. **Run the tests.** They start red. When they're green, you've got it.

```bash
dotnet test part1-inventory/Tests
```

Stuck? Each part has a `SOLUTIONS.md` with the answers. Using it isn't cheating.

---

## About the badges

Each part lines up with a free **MongoDB skill badge**. The badges assess MongoDB
*concepts* — they aren't a C# exam, and they don't check your code.

**Do you have to do the exercises to claim a badge?** No. The badge links are open and
you can take any assessment whenever you like. But the badges test the concepts these
exercises teach, so we'd suggest doing the part first.

Each part's README ends with a **badge checklist** mapping every objective the badge
assesses to the step that teaches it, and a **"From C# to the badge"** table
translating the C# you just wrote into the shell, Compass and Atlas UI equivalents
the assessment is likely to use.

---

## What you need

- The [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/products/docker-desktop/), **or** a free
  [MongoDB Atlas](https://www.mongodb.com/cloud/atlas/register) account
- Parts 4A and 4B need **no keys** for the exercises. To try the real thing at the end, free keys for Voyage AI and Google Gemini (see [docs/setup.md](docs/setup.md))

Quickest check that everything works:

```bash
docker compose up -d
dotnet run --project tools/doctor
```

---

## Repo layout

```
docker-compose.yml       MongoDB with Search and Vector Search, for local work
tools/doctor/            Environment check — run this first, and whenever things break
tools/seed/              Loads practice data into the database, for exploring in Compass and the shell
tools/ask/               Asks the Part 4A Elf Assistant a question for real, with your code and your own keys (a Level up)
tools/elf-chat/          Chats with the Part 4B agent for real, with your code, your own keys, and a memory kept in MongoDB (a Level up)
tools/embed/             Generates the Part 3B and 4A vectors with a Voyage key (maintainers only: they are already committed)
tools/voyage-check/      Checks the Voyage claims Part 3B makes against the real API (maintainers only)
tools/llm-check/         Checks the Gemini claims Part 4 makes against the real API (maintainers only)
tools/site-routes/       Lists every URL the landing page serves (used by the deploy)
src/Shared/              The models (Toy, Order, LedgerEntry), seed data and connection helpers
src/Content/             Renders this repo's markdown for the landing page (and its tests)
part1-inventory/         Part 1: README, Starter/, Tests/, SOLUTIONS.md
part2-ledger/            Part 2: README, Starter/, Tests/, SOLUTIONS.md
part3a-search/           Part 3A: README, Starter/, Tests/, SOLUTIONS.md
part3b-vector-search/    Part 3B: README, Starter/, Tests/, SOLUTIONS.md
part4a-rag/              Part 4A: README, Starter/, Tests/, SOLUTIONS.md
part4b-agent/            Part 4B: README, Starter/, Tests/, SOLUTIONS.md
LandingPage/             The challenge's front door (Blazor WebAssembly)
docs/
  setup.md               Full setup instructions and troubleshooting
  objective-map.md       Every badge objective mapped to the step that teaches it
  csharp-to-badge.md     C# ↔ shell / Compass / Atlas UI translation tables
DECISIONS.md             Why things are built the way they are
.github/workflows/       CI (the red/green proof) and the GitHub Pages deploy
```

---

## For contributors

CI proves two things on every pull request, and they're the two that matter:

- **The starter code fails every test.** An exercise whose tests pass before you
  write anything is broken.
- **The code printed in `SOLUTIONS.md` passes every test.** It's extracted from the
  markdown at build time by `.github/scripts/apply-solutions.py` and pasted into the
  starter project, so the documentation can't drift away from working code.

If you change a solution, change it in `SOLUTIONS.md` — that file is the source.

**The landing page renders these same markdown files**, so there is one copy of each README, not
two. Write plain GitHub-flavoured markdown; relative links to other rendered files become links
within the site, and the only raw HTML that survives is `details`, `summary`, `b` and a few other
harmless tags. `dotnet test src/Content.Tests` checks every link and heading in the shipped files.
The full rules are in [LandingPage/README.md](LandingPage/README.md#writing-content).

---

## Having trouble?

Start with `dotnet run --project tools/doctor` — it checks the common failures and
tells you how to fix each one. If that doesn't help, the troubleshooting section of
[docs/setup.md](docs/setup.md) covers the rest.
