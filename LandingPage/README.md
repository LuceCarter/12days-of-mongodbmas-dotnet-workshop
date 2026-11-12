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

The site shows a **copy** of each markdown file, made at build time. So after editing a README,
a plain `dotnet run` keeps showing the old text until you restart it. To preview edits as you
write, run:

```bash
dotnet watch --project LandingPage --no-hot-reload
```

That rebuilds and restarts whenever a README or doc changes (about five seconds). The
`--no-hot-reload` matters: without it `dotnet watch` notices the change but decides there is
"no managed code change to apply" and does nothing.

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

## Testing the GitHub links locally

The links on the page are built from `Repo` in `appsettings.json`, which points at the
published repository. Before that repo exists — or if you're working on a fork — those
links will 404.

To fix that for a local run only:

```bash
cp LandingPage/wwwroot/appsettings.Development.json.example \
   LandingPage/wwwroot/appsettings.Development.json
```

Then put your own URL in it. `appsettings.Development.json` is git-ignored, is loaded
only when `ASPNETCORE_ENVIRONMENT=Development` (which `dotnet run` sets), and can
override exactly two keys: `Repo` and `BadgeHub`. Everything else —
the parts, the badges, the resources — stays in `appsettings.json`, so there's only
ever one copy of the real content.

Badge links aren't affected: they're still placeholders, so they render as
"link coming soon" chips rather than broken links.
