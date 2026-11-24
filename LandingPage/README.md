# Landing page

The challenge's front door: a standalone **Blazor WebAssembly** app that builds to
static files and is published to GitHub Pages.

No server, no database, no authentication, no progress tracking. It is a signpost.

```bash
dotnet run --project LandingPage
```

## Where the content lives

| Thing | Where |
|---|---|
| **Every external link, badge URL, part description, release date and the repo URL** | `wwwroot/appsettings.json` |
| **The parts, setup guide and docs themselves** | the repository's own markdown — `part*/README.md`, `part*/SOLUTIONS.md`, `docs/*.md` |
| Page copy that is the site's own (home, challenges, resources & FAQ) | `Pages/*.razor` |
| Styles, including the rendered-markdown styles (`.prose`) | `wwwroot/css/app.css` |

`appsettings.json` is the single config file. Badge URLs are placeholders — search it for
`REPLACE_ME`. Until a URL is filled in, the UI shows a "link coming soon" chip rather than a dead
link, so the page is safe to publish before the badge links arrive.

## How a part appears on the site

The site doesn't contain the parts' text. It **renders the same markdown files GitHub shows**, so
there is only ever one copy of each README:

1. Before each build, the `CopySiteContent` target in the `.csproj` copies those files into
   `wwwroot/content/` (generated and git-ignored).
2. `MarkdownView` fetches the file and `src/Content` turns it into HTML — deciding which HTML
   survives, where each link goes, and what each heading's `id` is.
3. `Pages/ContentPage.razor` serves `/setup`, `/docs/{name}`, `/parts/{id}` and
   `/parts/{id}/solutions`. Which of those exist is decided by the `ContentCatalog`, built from
   `appsettings.json`.

**To release a part, set its `"Available": true`.** Its README and SOLUTIONS pages appear (before
that, `/parts/{id}` shows a "releases on …" card), and no code changes.

### Previewing a README edit

The site shows a **copy** of each markdown file, made at build time. So a plain `dotnet run` keeps
showing the old text after you edit a README. To preview edits as you write, run:

```bash
dotnet watch --project LandingPage --no-hot-reload
```

- **Editing a file that already exists** rebuilds and refreshes in about five seconds. The
  `--no-hot-reload` matters: without it `dotnet watch` notices the change, decides there is "no
  managed code change to apply", and does nothing.
- **Creating a new file** (the first `README.md` of a part, say) is **not** picked up. `dotnet watch`
  only watches directories that already hold a watched file, so a new file in an empty folder is
  invisible to it. **Stop it and start it again.** (A `.gitkeep` to anchor the folder and the polling
  watcher were both tried; neither helped.)

### Previewing a part before it's released

To see a part's pages without releasing it, use the local override below. **Don't** flip
`"Available"` in `appsettings.json` for this: that file is committed, and pushing it *is* the release.

### Writing content

Write plain GitHub-flavoured markdown — the files are written for GitHub first and the site adapts
to them. The rules the renderer applies:

- **Links.** A relative link to a rendered file (`SOLUTIONS.md`, `../docs/setup.md#option-2`)
  becomes a link within the site. Any other relative path (`Starter/WorkshopInventory.cs`) goes to
  GitHub. `javascript:`, `data:` and similar are made inert. Both forms work on GitHub as written.
- **Headings.** Ids match GitHub's, so `#step-9--everything-with-dinosaurs--objective-23` works in
  both places. The "On this page" list is built from the `##` and `###` headings.
- **HTML.** Raw HTML is allowed only for `details` (with `open`), `summary`, `b`, `i`, `em`,
  `strong`, `kbd`, `code`, `br`, `sup` and `sub`, **with no other attributes**. Anything else is shown
  as literal text. This is a security rule, not a style one: GitHub Pages sites share an origin with
  every other Pages site the same account owns.
- **Tables** are fine. Write a pipe inside a code span in a table as `\|`, exactly as GitHub needs.
- **Code blocks** are highlighted for `csharp`, `bash`, `javascript`, `sql`, `json` and
  `powershell`. To add a language, see `wwwroot/lib/highlight/README.md`.

## Tests and checks

```bash
dotnet test src/Content.Tests      # no MongoDB needed; about a second
```

They run the renderer over the real files and fail if a relative link points at nothing, a `#heading`
link has no heading, a `<details>` or code block is dropped, or hostile HTML gets through. CI runs
them on every pull request.

```bash
dotnet run --project tools/site-routes   # every URL the site serves
```

The Pages deploy uses that list to write a real `index.html` for each route, so deep links answer
**200** instead of leaning on GitHub's 404 fallback.

## Deployment

`.github/workflows/pages.yml` publishes on push to `main` whenever the site **or the files it
renders** change (`LandingPage/**`, `part*/**/*.md`, `docs/**`, `src/Content/**`). It does three
things that aren't obvious:

- **Rewrites `<base href>`** from `/` to `/<repo-name>/`, because Pages serves project sites from a
  subpath. The checked-in value stays `/` so `dotnet run` works locally. The workflow matches that
  line with `sed` — if you reformat it in `index.html`, update the `sed` too (CI checks this).
- **Writes an `index.html` per route** (`.github/scripts/make-route-pages.sh`), so `/parts/1` answers
  200.
- **Copies `index.html` to `404.html`** as the catch-all for anything not listed.

One-time repo setup: **Settings → Pages → Source → GitHub Actions**.

## Local-only settings

`wwwroot/appsettings.Development.json` holds settings for **your machine only**. It is git-ignored, and
loaded only under `dotnet run` (never on the live site), so nothing in it can reach `main`.

```bash
cp LandingPage/wwwroot/appsettings.Development.json.example \
   LandingPage/wwwroot/appsettings.Development.json
```

Everything in it is optional, and it can override exactly these:

| Key | What it does |
|---|---|
| `ReleaseParts` | Treat these parts as released, locally: `["2", "3A"]`, or `["*"]` for all. Ids aren't case-sensitive. The parts' **Start** buttons, README and Solutions pages appear and the "Releases …" tags go. |
| `Repo` | Point the GitHub links at a repo that exists (a fork, before the real one is published). |
| `BadgeHub` | The badge hub link. |

Everything else — the parts, the badges, the resources — stays in `appsettings.json`, so there's only
ever one copy of the real content.

`ReleaseParts` logs `[local preview] Releasing parts …` to the browser console so a preview can't be
mistaken for the live site, and names any id that matches no part (a typo). A released part with no
`README.md` yet shows *"There's no file at part2-ledger/README.md yet"*. The content tests read the
committed `appsettings.json`, so a local override never changes what they check.

Badge links aren't affected: they're still placeholders, so they render as "link coming soon" chips.
