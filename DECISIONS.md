# Decisions

A running log of calls made while building this, and why. Newest section last.
Anything still unverified is listed at the bottom.

---

## 2026-11-03 — Foundations

### Local environment: Atlas Local in docker-compose, not Aspire

`mongodb/mongodb-atlas-local:8.0.15`, pinned.

- **Why this image**: it bundles `mongod` and `mongot` in one container, which is
  what makes `$search` and `$vectorSearch` work locally. A plain `mongo` container
  cannot do either, so Parts 3A, 3B and 4 would be impossible without an Atlas account.
- **Why 8.0.15 and not 8.3.11** (both current as of today): 8.0 is what Atlas M0
  free-tier clusters run. Learners who switch to the Atlas fallback should get the
  same behaviour, not a newer one.
- **Why pinned at all**: these challenges have to still work in February. Verified
  available tags on 2026-11-03: `8.3.11`, `8.0.15`, `7.0.28`.
- **`hostname: mongodb` is required.** The single-node replica set advertises itself
  under this name. Remove it and the replica set never comes up.
- **`/data/mongot` must be mounted.** Search and Vector Search *index definitions*
  live there, not in `/data/db`. Verified by experiment: created a vector index, ran
  `docker compose down && docker compose up -d`, and the index came back `READY` and
  `queryable: true`. Without the mount it would have been silently lost.

Atlas free tier remains the documented fallback everywhere, via `MONGODB_URI`.

### Search indexes take ~20 seconds to become queryable

Measured locally. Creating a search index returns immediately, but querying it
before `queryable: true` returns nothing — with no error. This is a trap for
learners and for tests.

Consequences:
- `tools/doctor --deep` polls for `queryable` rather than sleeping a fixed time.
- Part 3A/3B test fixtures must do the same. A fixed `Task.Delay` will be flaky in CI.
- The Part 3A README has to call this out explicitly.

### LLM: Google Gemini free tier. Embeddings: Voyage AI

**GitHub Models was the original plan and is gone** — fully retired on 2026-07-30,
including the playground, catalog, inference API and BYOK.

Gemini chosen because the free tier needs no credit card, works worldwide, documents
tool/function calling, and exposes an OpenAI-compatible endpoint
(`https://generativelanguage.googleapis.com/v1beta/openai/`) so it can be reached
through `Microsoft.Extensions.AI.OpenAI` and used as an `IChatClient`.

Everything in Part 4 is written against `IChatClient`, so swapping provider is a
config change. A "Level up" step makes the learner actually do it.

Using **different providers for the generator and the embeddings is deliberate** —
it is a RAG badge objective ("a generator that is separate from the embedding model"),
and Part 4A's README says so.

Note: Voyage's free tier excludes the older `voyage-3.x` text models. Use `voyage-3.5`.

### Vector access is deliberately two-layered

- **Part 3B uses the raw driver** and hand-writes the `$vectorSearch` stage. The badge
  assesses exactly that, and it is the only way to do the age-range prefilter
  (`$gte`/`$lte`) the story needs.
- **Part 4 uses the abstraction**: `MongoVectorStore` from
  `Microsoft.SemanticKernel.Connectors.MongoDB`, wrapped in an adapter delegate and
  handed to Microsoft Agent Framework's `TextSearchProvider`.

Two caveats recorded honestly in the Part 4A README:
- That connector is **preview only** (`1.74.0-preview`, published 2026-03-20; there is
  no stable release) and its filter support is **`EqualTo` only**.
- The `EqualTo` limit does not bite us, because `TextSearchProvider` takes a *delegate*,
  not a collection. Handbook prefilters are equality anyway, and where a range is needed
  the delegate can drop to the raw driver.

MongoDB's own `MongoDB.AgentFramework` connector was considered and rejected: it has
never been published to NuGet, has no release tags, last saw a commit on 2026-09-10,
and fails closed outside Agent Framework `[1.13.0, 1.17.0)` while the current release
is 1.27.0. It remains useful as a reference implementation.

### Extra field on the Toy model: `stockCount`

The brief's model lists `InStock` (a bool). Part 1 also needs "count of low-stock toys",
and the bonus section needs `$inc`. A bool cannot express either. Added `stockCount`
(int) alongside `inStock`. Flagging it because it is an addition to the brief.

### Array equality needs `AnyEq`, not `Eq` — and this matters for the badge

Badge objective 2.3 is "query an array field using an equality constraint with a
scalar value". In the shell that is simply `{ tags: "dinosaur" }`.

In C# with a typed `Builders<Toy>` filter, the obvious translation **does not compile**:

```csharp
Builders<Toy>.Filter.Eq(t => t.Tags, "dinosaur")   // CS1660 — Tags is List<string>
```

All three of these work and send the same query (verified: 8 matches each):

```csharp
Builders<Toy>.Filter.AnyEq(t => t.Tags, "dinosaur")   // typed Builders
new BsonDocument("tags", "dinosaur")                  // exactly what the shell sends
toys.Find(t => t.Tags.Contains("dinosaur"))           // LINQ
```

This is exactly the kind of gap `docs/csharp-to-badge.md` exists for, so Part 1's step
for 2.3 shows the raw BSON alongside `AnyEq`. Otherwise a learner meets
`{ tags: "dinosaur" }` in the assessment having only ever typed `AnyEq`, and may think
the shell needs `$elemMatch`.

### Tests: xUnit v3 on the Microsoft Testing Platform

`dotnet new xunit` still scaffolds xUnit **v2**, which on the .NET 10 SDK fails with:

> Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on
> .NET 10 SDK and later.

The .NET 10 SDK dropped VSTest for this platform. The opt-in is a `test` block in
**`global.json`** (not `dotnet.config`, which is a dead end — it is silently ignored):

```json
{ "test": { "runner": "Microsoft.Testing.Platform" } }
```

Test projects therefore need `<OutputType>Exe</OutputType>` and a plain `xunit.v3`
reference — **no** `Microsoft.NET.Test.Sdk` and no `xunit.runner.visualstudio`, both of
which are VSTest-era packages. The MSBuild property
`TestingPlatformDotnetTestSupport` is a .NET 9-era mechanism and does nothing here.

Two smaller calls:
- All test classes share one `[CollectionDefinition(..., DisableParallelization = true)]`
  collection. They reseed the same `toys` collection before each test, so parallel
  classes would tread on each other. `CollectionBehaviorAttribute.DisableTestParallelization`
  is obsolete in this version.
- `xUnit1051` (thread `CancellationToken` through every driver call) is suppressed via
  `NoWarn` in the test csproj. It fired 16 times, and the tests double as worked
  examples that learners read — the clutter costs more than the cancellation
  responsiveness buys on a one-second local test run.

### Versions pinned (verified on nuget.org, 2026-11-03)

| Package | Version | Note |
|---|---|---|
| .NET SDK | 10.0.401 | `global.json`, `rollForward: latestFeature` |
| MongoDB.Driver | 3.13.0 | latest stable |
| xunit.v3 | 4.0.2 | current xUnit line |
| Microsoft.NET.Test.Sdk | 18.11.0 | |
| Microsoft.Extensions.AI / .OpenAI | 10.11.0 | Parts 3B/4 |
| Microsoft.Extensions.VectorData.Abstractions | 10.11.0 | stable |
| Microsoft.Agents.AI | 1.27.0 | stable, 2026-10-30 |
| Microsoft.SemanticKernel.Connectors.MongoDB | 1.74.0-preview | **no stable release exists** |

All pinned centrally in `Directory.Packages.props`.

### Solution format

.NET 10's `dotnet new sln` produces `SantasWorkshop.slnx` (the XML format), not `.sln`.

---

## Still unverified

Things to confirm before the part that depends on them ships.

- [ ] Gemini free-tier RPM/RPD figures, and which model ID is on the free tier. *(Part 4)*
- [ ] Whether `voyage-3.5` is free-tier eligible today, and its dimension count. *(Part 3B)*
- [ ] Whether `MongoVectorStore` creates the Atlas vector index itself or needs a
      pre-existing one — the docs do not say. *(Part 4A)*
- [ ] Whether a newer `Microsoft.SemanticKernel.Connectors.MongoDB` preview has shipped
      past `1.74.0-preview`. *(Part 4A)*
- [ ] That VectorData `10.11.0` resolves cleanly for both the SK connector (`>= 10.1.0`)
      and Agent Framework (`>= 10.10.0`). *(Part 4)*
- [ ] **Does the CRUD Overview (C# edition) assessment show Builders or LINQ in its
      questions?** We lead with Builders because it maps 1:1 onto the shell syntax the
      other nine badges use. Both styles appear in Part 1 either way — only the ordering
      changes — so this is a find-and-replace while Part 1 is unreleased, and a rewrite
      afterwards. Worth asking the EDU team before Dec 1. *(Part 1)*

- [ ] **The Pages `paths:` filter really triggers on a README edit.** `pages.yml` lists
      `part*/*.md` and `part*/**/*.md`; GitHub's glob rules for `**` matching zero directories
      are not something I could test locally. Edit a README sentence, push, and check the deploy
      starts. *(In-app content)*
- [ ] **Per-route pages answer 200 on real GitHub Pages.** Verified against a local static server
      under the same subpath, not against Pages itself. *(In-app content)*
- [ ] **Smooth/instant scrolling and the sleigh/snow motion feel right in a real browser.** Headless
      Chrome can measure scroll positions but not animation. *(Landing page)*

---

## Resolved

### Naming (2026-11-03)
The **repo** is "12 Days of MongoDBmas — C#". **Santa's Workshop** is the name of the
*app* the learner builds, so it is what appears in the landing page title, the doctor
banner, the devcontainer name and the story text. Both names are correct, in their own
places.

### Stretch badges stay in "Level up" (2026-11-03)
Indexing Design Fundamentals, Relational to Document Model and Vector Search Performance
remain optional "Level up" sections rather than first-class parts. They keep their rows
in `docs/objective-map.md` so coverage is still tracked, but they do not count against
the 30–60 minute core path.

---

## 2026-11-09 — Landing page

### Content lives in `appsettings.json`, not in markup

Every external link, badge URL, part summary, release date and resource on the landing
page is loaded from `LandingPage/wwwroot/appsettings.json`. This is what makes "badge
links stay as placeholders in one config file" literally true rather than approximately
true: no `.razor` file contains a badge URL, a repo URL or a release date.

The exception, deliberately: five vendor links in the Setup page's prose (the .NET SDK,
Docker Desktop, Atlas signup, Voyage, Google AI Studio). They are stable, they read as
part of the sentence around them, and hoisting them into config would make the copy
harder to edit for no benefit.

Badge URLs are `REPLACE_ME/...`. The `Badge.IsPlaceholder` check drives the UI, so an
unfilled badge renders as a dashed "link coming soon" chip instead of a dead link —
the page can go live before MongoDB EDU hands over the real URLs.

Releasing a part is a one-line change: `"Available": true`.

### Config is loaded with source-generated JSON, not `IConfiguration`

Published WebAssembly output is trimmed, and reflection-based `System.Text.Json` is
unreliable once it is. `ChallengeConfigContext` is a `JsonSerializerContext`, so
binding survives trimming. The config is fetched in `Program.cs` *before* the host
runs, so no component ever has to render a half-configured page.

### No Bootstrap

The template ships it; this page is four routes and `wwwroot/css/app.css` is smaller
than Bootstrap's grid alone. Hand-written CSS with custom properties, a
`prefers-color-scheme: dark` block, a skip link, visible focus rings and a
`prefers-reduced-motion` guard.

### `InvariantGlobalization` is on

The page is English-only and formats nothing by culture, so the ICU data was pure
download weight. Publish output went from **9.0 MB to 6.5 MB** uncompressed.

For context: that is still a heavy first load for a static signpost — it is the floor
for Blazor WASM. The Pages workflow installs the `wasm-tools` workload, which relinks
the runtime and trims it further. Noting the trade-off rather than hiding it; Blazor
was a deliberate choice and showing it off is part of the point.

### GitHub Pages: two non-obvious steps

- **`<base href>`** is `/` in the repo so `dotnet run` works, and the workflow rewrites
  it to `/<repo-name>/` at publish time. If `index.html`'s base tag is reformatted, the
  `sed` in `pages.yml` must change with it.
- **`404.html` is a copy of `index.html`.** Pages serves it for unknown paths, which
  hands routes like `/setup` back to the Blazor router instead of 404ing on refresh.

`.nojekyll` is committed so nothing strips the `_framework` directory.

---

## 2026-11-09 — CI

### The solutions are extracted from `SOLUTIONS.md`, not kept on a branch

**This replaces the `solutions` branch in the plan.** Flagging it as a change rather
than quietly swapping it, because the plan said otherwise.

A branch carrying completed `Starter/` projects has one failure mode and it is a
guaranteed one: someone fixes a bug in the branch and not in the markdown, or vice
versa, and the file learners are told to read stops being the file that passes.
Nothing catches it, because nothing compares them.

So CI does this instead, in `.github/scripts/apply-solutions.py`:

1. Parse `part1-inventory/SOLUTIONS.md`.
2. For each `<details>` block, take the **first** ```csharp fence — the canonical
   answer. Later fences are alternatives and deliberate anti-examples (Step 3's
   `ReplaceOneAsync`, Step 9's raw BSON and LINQ forms) and are skipped.
3. Brace-match the matching method in `Starter/` and swap the whole declaration.
   Regex can't do this: the bodies contain parentheses, braces and comments.
4. Fail loudly if any solution has no matching method, or any method is left over.

The published documentation is therefore executable. If the markdown is wrong, CI is
red. A `solutions` branch can still be added later for people who want to clone one,
but it would be generated from this, not maintained by hand.

### The red/green assertion is the point of the job

CI asserts **both** directions for Part 1:

- The starter code must fail. A part whose tests pass before the learner writes
  anything is broken, and without this check nobody finds out until someone is
  halfway through it.
- The extracted solutions must pass.

Exit code precision matters here. Microsoft Testing Platform returns **0** for all
passed and **2** for "at least one test failed". The job requires exactly 2 — a crash,
a build break or zero tests discovered all exit differently, and none of them are a
healthy red. `dotnet build` runs as its own step first, so by the time the test steps
run, a non-zero exit can only mean failing tests.

A final step checks `git diff` touched nothing outside `Starter/`, so the applier can't
wander into the tests and make the green run meaningless.

### Atlas Local as a GitHub Actions service container

Same pinned image as `docker-compose.yml`, so CI and a learner's machine run the same
MongoDB. `directConnection=true` in the connection string is what makes it work: the
replica set advertises itself as `mongodb`, which doesn't resolve from the runner host,
and direct connection skips topology discovery and talks to the mapped port.

CI waits for readiness by running **`tools/doctor`** in a retry loop rather than
sleeping. It is what learners are told to run first, it checks exactly what the tests
are about to need (connectivity, replica set, mongot), and running it on every build
means the doctor itself can't rot. Missing API keys are warnings, not failures, so a
keyless CI run is still green.

### The landing page gets its own CI job

A publish plus four assertions on the output, including that `index.html` still
contains the exact `<base href="/" />` line that `pages.yml` rewrites with `sed`. That
coupling is the kind of thing that breaks silently six weeks later; now it breaks on
the pull request instead.

### Snow, sleigh and the upside-down tree

All of it is CSS. No JavaScript animation library, nothing that touches page content.

- **Snow**: 48 flakes, generated once with a fixed seed so every load looks the same.
  Each has a negative `animation-delay`, so the snow is already mid-fall when the page
  appears rather than all starting at the top together. Flakes carry a hairline ring so
  white-on-parchment stays visible in light mode. Fewer flakes under 600px.
- **Sleigh**: one pass, 1.5s after load, ending off the right edge. It lives in the layout,
  so it flies once per *visit*, not once per click between pages. Toggling snow off and on
  again brings it back for another pass.
- **Tree**: upside down, hanging from the top-right corner of the page by its trunk, with a
  koala in a Santa hat clinging to the trunk among the top branches and a star on the bottom
  tip. It overlaps the header and a little of the content below. `pointer-events: none`, so
  it never blocks a click. On screens 1280px and wider it sits in the empty margin; below
  that the header reserves room for it, so the nav wraps rather than running underneath.
  (History: it started in the footer, then moved to the header; the koala started perched
  *above* the trunk and was moved into the foliage — "in the tree" was the brief.)
  Unlike the snow it is content, with an accessible name, not decoration.
- **Motion is optional.** Auto-playing movement that lasts more than five seconds needs a
  way to stop it (WCAG 2.2.2), so the header has a "❄ Snow" toggle, remembered in
  `localStorage`. Anyone with `prefers-reduced-motion` gets no snow, no sleigh and no
  toggle at all. The decorative layer is `aria-hidden` and `pointer-events: none`.

### Two bugs found by looking at it

The first real screenshots (headless Chrome — see below) caught two bugs in the earlier
landing page work, neither visible in a build:

- **Card grids stopped at ~730px** instead of filling the 1080px column. A blanket
  `p, ul, ol { max-width: 68ch }` reading-width rule was also catching `ul.cards` and
  `ul.parts`, which are layout, not prose.
- **A focus box around every page's `<h1>`.** Blazor's `FocusOnNavigate` moves focus to the
  heading so screen readers announce the new page, which is right — but the Blazor
  template's `h1:focus { outline: none }` was lost when its CSS was replaced.

### Verifying animation without a browser extension

Headless Chrome (`--headless=new --screenshot`) works for layout but **freezes CSS
animations at their first frame** under `--virtual-time-budget`, so the sleigh can't be
caught mid-flight that way. The sleigh artwork was checked by temporarily pinning it
on-screen; the *motion* itself (keyframes, timing, the 18s crossing) has not been seen
running and should be eyeballed in a real browser.

## 2026-11-12 — First deploy

### Resolved: the repo URL

The landing page's `Repo` was a guess; the real repository is
`LuceCarter/12days-of-mongodbmas-dotnet-workshop`. Corrected in
`LandingPage/wwwroot/appsettings.json`, which is still the only place it lives.

Related fixes made while checking every link:

- **The unused `Discussions` setting was removed.** No page referenced it, and Discussions
  is switched off on the repo, so it was a dead link waiting to be wired up.
- **The `cd` in the Setup page's clone instructions is derived** (`ChallengeConfig.RepoName`,
  from the `Repo` URL) instead of typed out a second time. It had been hard-coded to a
  folder name that did not match the repo.
- **`index.html` no longer links `SantasWorkshop.LandingPage.styles.css`.** That is the
  template's slot for scoped component CSS, which this page does not use, so the file
  never existed and every page load logged a 404. If scoped `.razor.css` files are ever
  added, put the link back.

### Why the first deploy showed no styling

It wasn't the CSS. The first run of `pages.yml` built correctly, then failed at
`deploy-pages` with a 404 because GitHub Pages had not been enabled. Pages was then
switched on with its source set to *Deploy from a branch*, so GitHub ran its own Jekyll
build of the repo — the live site was the README rendered by Jekyll, which has no
`css/app.css` and no `_framework/`. The Pages source must be **GitHub Actions**
(Settings → Pages → Source), and the workflow re-run, since `pages.yml` only triggers on
changes under `LandingPage/**`.

### Link audit

Every URL the landing page can render was requested: 16 live links, all resolving. The ten
badge links are `REPLACE_ME` placeholders and render as "link coming soon" chips, so they
are not requested. Re-run the audit when the real badge URLs arrive.

---

## 2026-11-12 — The parts render inside the landing page

### The site renders the repo's own markdown

"Start Part 1" used to leave for a plain GitHub page, and each new part would have needed another
hand-built page. Now `src/Content` turns the repository's own `part*/README.md`, `SOLUTIONS.md` and
`docs/*.md` into HTML, so there is **one copy** of each README (GitHub and Codespaces still render it)
and **releasing a part is `"Available": true`** in `appsettings.json`. The hand-written `Setup.razor`
was a condensed copy of `docs/setup.md` that had already drifted; `/setup` now renders the real file.

The renderer is a plain class library (`src/Content`) so it is testable without a WebAssembly host.
Markdown is rendered **at runtime in the browser** (Markdig 1.6.0), not prerendered at build time:
simpler, and the cost was measured — see below. Prerendering stays an option if that ever changes.

### What a markdown file is allowed to do on the site

GitHub Pages project sites share one origin (`username.github.io`) with every other Pages site the
same account owns, so markdown is treated as untrusted input even though we write it:

- **Raw HTML is an allowlist, not a blocklist.** `details` (with `open`), `summary`, `b`, `i`, `em`,
  `strong`, `kbd`, `code`, `br`, `sup`, `sub` — with **no attributes** otherwise. Everything else is
  shown as text. Hand-rolled instead of a sanitiser library to avoid dragging an HTML parser into the
  WebAssembly download; the content only ever uses `details`/`summary`/`b`.
- **`UseAdvancedExtensions()` is not used.** It includes Markdig's generic attributes
  (`{onclick=...}`), which would let markdown set attributes on elements. Extensions are picked one
  at a time: pipe tables, autolinks, strikethrough, task lists.
- **Links are classified.** Relative links to a rendered file become site links; other relative paths
  go to GitHub; `javascript:`, `data:`, `vbscript:`, protocol-relative and repo-escaping links are
  made inert (checked after stripping the tabs/newlines browsers ignore inside a scheme).
- **Heading ids are generated by our own GitHub slug algorithm** (`GitHubSlug`), not Markdig's, so
  `#step-9--everything-with-dinosaurs--objective-23` and `#-level-up` work on GitHub and on the site.
  GitHub does not collapse or trim hyphens, which is why `✅ Done` is `-done`.

### Tests that audit the real files

`src/Content.Tests` (106 tests, no MongoDB) renders every file the site serves and fails on a dead
relative link, a `#heading` link with no heading, duplicate heading ids, a dropped `<details>` or code
block, hostile HTML, or a table escape leaking into the page. They were checked for teeth: a planted
dead file, dead anchor and `javascript:` link each failed an audit test, and disabling the pipe fix
failed the two tests written for it. CI runs them in the `landing-page` job.

### Shipping the content: the `CopySiteContent` target

A `BeforeTargets="BeforeBuild;ResolveProjectStaticWebAssets"` target copies the files into a
generated, git-ignored `wwwroot/content/` before every build, keeping repo-relative paths. Verified
from a **clean checkout** (no `bin`, `obj` or `content`) for both `dotnet run` and `dotnet publish`.

### Deep links return 200: `tools/site-routes` + `make-route-pages.sh`

GitHub Pages answers an unknown path with a 404 *status* and only then serves `404.html`. People see
the page; crawlers and link previews see an error. The deploy now writes a real `index.html` for every
route. The route list comes from `tools/site-routes`, which asks the app's own `ContentCatalog` and
reads the parameter-free `@page` routes out of the Razor files, so it cannot drift from the app.
`404.html` stays as the catch-all. `pages.yml` also now triggers on `part*/*.md`, `docs/**` and
`src/Content/**` — without that, a README fix would merge and the live copy would quietly stay stale.

### Syntax highlighting: highlight.js, vendored

11.12.0 (BSD-3-Clause), core + `csharp`, `bash`, `javascript`, `sql`, `json`, `powershell`, about
48 KB, committed in `wwwroot/lib/highlight/` with its licence and SHA-256 checksums. Not loaded from a
CDN: the site should not have a third-party runtime dependency, and these challenges are meant to
still work in February. Highlighting is decoration — if it fails the code is still readable, so it
fails quietly.

### Measured cost

Publish went from about 6.5 MB to **6.99 MiB uncompressed (2.28 MiB brotli)**: Markdig 251 KB, the
Content library 35 KB, highlight.js about 48 KB, markdown about 50 KB. Inside the 0.5 MB budget.

### Things the browser taught us (none of these were visible in a build)

- **Blazor does not raise `LocationChanged` for a link that only changes the `#hash` of the page
  you're on.** It calls `history.pushState` and its own `scrollIntoView()` and returns. Two handlers I
  wrote for that case were dead code and are gone. Arriving on a page *with* a fragment (a deep link,
  or README → `setup#option-2`) still needs our scroll-after-load, because the headings do not exist
  until the file has downloaded.
- **The skip link was broken on every page** (since the first landing-page milestone). A bare
  `href="#main"` resolves against `<base href>`, i.e. the site root, so "Skip to content" sent a
  keyboard user to the home page. It now targets the current page, and a click handler moves focus
  into `<main tabindex="-1">` — scrolling there is not enough.
- **`html { scroll-behavior: smooth }` is gone.** Blazor's own anchor scrolling honours it, and
  gliding thousands of pixels down a long README is slow and disorienting.
- **Tables: a hidden column is worse than a chopped identifier.** `docs/csharp-to-badge.md` has a
  four-column table whose last column, *Shell*, is the one the badge assessment uses; with unbroken
  code it was scrolled off-screen with no hint it existed. Code in cells now wraps, so every column is
  visible; anything still too wide scrolls inside the table.
- **`\|` inside a code span in a table** is an escape for the cell, and GitHub shows `|`. Markdig keeps
  the backslash, so `a \|\| b` rendered literally. Undone in the renderer; regression-tested.
- **A field named `page` collides with Razor's `@page` directive**, so `@page.Route` parses as a
  directive. Renamed.
- **`FocusOnNavigate` focuses the first `<h1>` once, at navigation.** On a page whose `<h1>` arrives
  after a fetch there is nothing to focus yet; harmless, but worth knowing before trusting "focus is on
  the heading".

### Verifying without a browser extension

Headless Chrome (`--headless=new`) is usable, within limits learned the hard way:

- It **freezes CSS animations and smooth scrolling** under `--virtual-time-budget`, and
  **screenshots of a scrolled page are unreliable** (blank or stitched). Use a short page or a tall
  window for pictures.
- For behaviour, **measure instead of looking**: append a temporary probe to the page's JS that writes
  `scrollY`, `activeElement` or a trace into `document.title`, then read it with `--dump-dom`. Every
  navigation scenario above was verified this way, and the probe was removed afterwards.
- A **stale dev server holding port 5054 silently serves the previous build** while the new one fails
  to bind — twice this caused confusing results. Check `lsof -iTCP:5054 -sTCP:LISTEN` before trusting a
  screenshot. The new server's log must say "Now listening".
- A rendered-page crawl (every route, every `<a>`, every `#fragment`, every external URL) found the
  skip-link bug that no unit test could have: 13 pages, 190 internal links, 22 external URLs.

### Every part says where the code is (and the tests that keep it that way)

A learner arriving from the website has no repository yet, but Part 1 told them to run
`docker compose up -d` and edit `Starter/WorkshopInventory.cs` without saying where either lives.
Part 1 now has a **Get the code** section straight after the story: the repo link, Codespaces, the
clone commands, "run every command from the folder containing `docker-compose.yml`", and a small
file tree marking the one file they write in. `docs/setup.md` lost its `<this repo>` placeholder, and
**Option 3 (Atlas, no Docker) now clones first** — it used to jump straight to the doctor, which only
works from a clone.

The URL has to be written literally in the markdown (GitHub renders these files too and cannot
substitute a setting), so it now exists in several places. `RepoReferenceTests` keeps them honest:

- every `github.com/<owner>/<repo>` in the shipped markdown that names our owner must name the
  configured repo, so a rename fails a test instead of leaving stale links;
- `docs/setup.md` must have a real clone command and no placeholder, and each of its three options
  must say how to get the code;
- **every released part's README must link the repo, show a clone command, mention Codespaces and
  say where its `Starter/` folder is** — so Parts 2–4 cannot ship without it.

Checked for teeth by planting a stale repo name, the old placeholder, an Option 3 with no clone, and
a README with no starter path; each failed the matching test. (One plant silently did nothing the
first time: `sed '0,/re/s//x/'` is GNU-only and macOS sed ignores it. If a planted defect does not
fail a test, first check the plant actually happened.)

### Previewing README edits needs `dotnet watch --no-hot-reload`

The site serves a copy made at build time, so editing a README under a running `dotnet run` showed
nothing until a restart — found when a README edit did not appear in a screenshot. The `.csproj` now
declares the markdown as `<Watch>` items. In `dotnet watch`'s default hot-reload mode that is still not
enough: it logs `File updated` then `No managed code changes to apply` and does nothing. With
`--no-hot-reload` the same edit rebuilt and refreshed the served copy in about five seconds. Documented
in `LandingPage/README.md`.

### The favicon: MongoDB's leaf in a Santa hat

The icon uses MongoDB's **official leaf artwork** (Spring Green, from the brand asset library the
`mongodb-deck` skill carries) rather than a hand-drawn approximation, with the hat drawn as vector
shapes on top. The leaf stays unmodified; only the hat is ours. It sits on a rounded tile in the
site's evergreen so it stays readable on both light and dark browser tabs — checked at the real 16
and 32 pixel sizes, enlarged without smoothing and in a mock tab. The upright leaf is deliberate:
tilting the mark would use more of the square, but it is a logo.

Shipped: `favicon.ico` (16/32/48), `favicon-16.png`, `favicon-32.png`, `apple-touch-icon.png`
(square corners; iOS rounds its own), plus `favicon.png` and `icon-192.png`, which were already there
(the latter was the Blazor template's icon and is now ours). There is deliberately no SVG favicon: the
leaf is a raster image, so an SVG would only wrap a PNG. Source, rebuild script and the editing notes
are in `LandingPage/favicon-src/`.

Things that went wrong while building it, in case they recur:

- **`sips` always crops from the centre.** `--cropOffset` made no difference whichever side of the crop
  I put it on, so the first 192px icon was a slice of the tile's far corner. The tell was files far too
  small (245 bytes for a 64px icon). Fix: draw each icon in the *middle* of Chrome's 512px page, so a
  centre crop returns exactly it.
- **The leaf's tip poked through the hat.** Computed rather than eyeballed: the cone's left edge crossed
  6 units below the tip and 11 units inside the leaf outline. The corrected curve clears the tip by 26
  units and the outline by 37.
- **A `#` in a file path ends a `file://` URL.** The repo lives in a folder called "… with C#", so a
  hand-written `file://$PWD/…` URL silently loaded nothing. `Path.as_uri()` percent-encodes it. This will
  matter again for any local test page.
- **Chrome's headless window cannot go below ~500px**, so small icons are rendered in a big page and cropped.

New tests (`IndexHtmlTests`): every relative file `index.html` links must exist in `wwwroot`; each icon's
real pixel size, read from the PNG header, must match its name; the `.ico` must hold 16, 32 and 48 pixel
images. Checked by planting a dead link and a 16px file named `favicon-32.png`.

### Header order, "Challenges", and no "Available now" pill

- **Setup comes before Challenges** in the header, and the link is just **Challenges** (the page's own
  title and heading match). A developer can't start a part without an environment, so the nav now
  reads in the order they'll need things: Home, Setup, Challenges, Resources & FAQ. Prose that says
  "the challenges" in a sentence is unchanged.
- **Only unreleased parts carry a tag** — "Releases 5 December". A released part used to get a green
  "Available now" pill, which was the same shape and colour as the **Start Part 1** button and
  looked clickable. Absence now means "it's out"; the Start button is the one green thing on a
  released card. The pill's `.tag--live` style had no other use and is gone.
- Releasing a part is still just `"Available": true`: its date tag disappears and its Start button
  appears, with nothing else to edit.

### The home page hook is the story, not a feature list

The hero used to say what the course *is* ("Five hands-on challenges… a free badge at the end of every
part"). It now does what Part 1 does: drops the reader into the scene first, then says what they will
do, then the logistics. Order matters here — the first thing a visitor reads should be a reason to care.

- **Headline:** "Santa's workshop needs a developer" (was "Build Santa's Workshop, learn MongoDB").
- **The scene** (larger type, white): the leather ledger, fountain pen, four thousand pages, three elves
  with repetitive strain injury, cocoa on the entire letter M. Deliberately the *same* facts as Part 1's
  story, so a reader who clicks through meets the same workshop.
- **The promise:** move the catalogue into MongoDB, settle who's been naughty or nice, help the elves find
  the perfect gift for any child, then build an AI assistant that has read the Elf Handbook. Each of those
  maps to a part (1, 2, 3A/3B, 4); nothing is promised that a part doesn't deliver.
- **The logistics, last and short:** free badge, no experience needed, nothing expires. "This isn't a daily
  puzzle" moved out of the hero — the FAQ answers it, and the hero needed to fit above the fold.
- **No date in the story.** Part 1 opens "It is the third of November"; the home page must not, because it
  has to still read correctly in February.

Fitting above the fold: at 1440x780 (roughly a 13-inch laptop's page area) the headline, the story, the
promise and both buttons are visible without scrolling. That took trimming the hero's vertical padding and
shortening the logistics line to two lines.

`StoryConsistencyTests` keeps the home page and Part 1 telling the same story (six shared facts) and fails
if a month or year appears in the home hero. Checked by planting a changed story and a date.

### Share text carries the same hook

`index.html`'s `<meta name="description">` and `og:description` (what search results and link previews
show) said "Five hands-on C# challenges that take you from your first InsertOne…". They now lead with the
same hook as the hero: *"Cocoa on Santa's four-thousand-page ledger! Rescue it in five hands-on C#
challenges, from InsertOne() to an AI agent. Free MongoDB skill badges."* — 145 characters, under the
~160 search engines show before cutting mid-sentence. `IndexHtmlTests` fails if the description grows
past 160 or the Open Graph title/description go missing. Also fixed "a enormous" → "an enormous" in
Part 1's story.
