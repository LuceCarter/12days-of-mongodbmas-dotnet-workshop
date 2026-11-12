# Landing page

The challenge's front door: a standalone **Blazor WebAssembly** app that builds to
static files and is published to GitHub Pages.

No server, no database, no authentication, no progress tracking. It is a signpost.

```bash
dotnet run --project LandingPage
```

## Where the content lives

| Thing | File |
|---|---|
| **Every external link, badge URL, part description and release date** | `wwwroot/appsettings.json` |
| Page copy | `Pages/*.razor` |
| Styles | `wwwroot/css/app.css` |

`appsettings.json` is the single config file the brief asked for. Badge URLs are
placeholders — search it for `REPLACE_ME`. Until a URL is filled in, the UI shows a
"link coming soon" chip rather than a dead link, so the page is safe to publish before
the badge links arrive.

To mark a part as released, set its `"Available": true`.

## Deployment

`.github/workflows/pages.yml` publishes on push to `main`. It does two things that
aren't obvious:

- **Rewrites `<base href>`** from `/` to `/<repo-name>/`, because Pages serves project
  sites from a subpath. The checked-in value stays `/` so `dotnet run` works locally.
  The workflow matches that line with `sed` — if you reformat it in `index.html`,
  update the `sed` too.
- **Copies `index.html` to `404.html`**, so client-side routes like `/setup` survive a
  hard refresh. Pages serves `404.html` for paths it can't find, which hands the URL
  back to the Blazor router.

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
override exactly three keys: `Repo`, `Discussions` and `BadgeHub`. Everything else —
the parts, the badges, the resources — stays in `appsettings.json`, so there's only
ever one copy of the real content.

Badge links aren't affected: they're still placeholders, so they render as
"link coming soon" chips rather than broken links.
