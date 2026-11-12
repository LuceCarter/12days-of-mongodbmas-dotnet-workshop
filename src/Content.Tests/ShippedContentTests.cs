using System.Text.RegularExpressions;

namespace SantasWorkshop.Content.Tests;

/// <summary>
/// Runs the renderer over the repository's real markdown files — the ones the landing
/// page will actually serve. These are the tests that catch a broken link or a dropped
/// section in our own documentation, not just in the renderer.
/// </summary>
public sealed class ShippedContentTests
{
    private static readonly ContentCatalog Catalog = RepoFiles.Catalog();
    private static readonly MarkdownRenderer Renderer = RepoFiles.Renderer();

    // Rendered once; many tests read the same pages.
    private static readonly Dictionary<string, RenderedPage> Rendered =
        Catalog.Pages.ToDictionary(p => p.Route, p => Renderer.Render(RepoFiles.Read(p.SourcePath), p.SourcePath));

    public static TheoryData<string> Routes() => new(Catalog.Pages.Select(p => p.Route));

    private static ContentPage Page(string route)
    {
        Assert.True(Catalog.TryGetByRoute(route, out var page));
        return page;
    }

    // ---- the files exist ------------------------------------------------------------

    [Theory, MemberData(nameof(Routes))]
    public void Every_page_the_site_serves_has_a_source_file(string route)
    {
        var page = Page(route);

        Assert.True(RepoFiles.Exists(page.SourcePath), $"{route} is served from {page.SourcePath}, which does not exist.");
    }

    [Fact]
    public void Every_released_part_ships_both_a_readme_and_solutions()
    {
        var released = RepoFiles.Parts().Where(p => p.Available).ToList();

        Assert.NotEmpty(released);

        foreach (var part in released)
        {
            Assert.True(RepoFiles.Exists($"{part.Folder}/README.md"), $"Part {part.Id} is Available but has no README.md.");
            Assert.True(RepoFiles.Exists($"{part.Folder}/SOLUTIONS.md"), $"Part {part.Id} is Available but has no SOLUTIONS.md.");
        }
    }

    // ---- links ----------------------------------------------------------------------

    [Theory, MemberData(nameof(Routes))]
    public void No_link_is_blocked(string route)
    {
        var blocked = Rendered[route].Links.Where(l => l.Kind == LinkKind.Blocked).Select(l => l.Original);

        Assert.Empty(blocked);
    }

    [Theory, MemberData(nameof(Routes))]
    public void Every_link_into_the_repository_points_at_something_that_exists(string route)
    {
        var missing = Rendered[route].Links
            .Where(l => l.Kind is LinkKind.GitHub or LinkKind.Site && l.RepoPath is not null)
            .Where(l => !RepoFiles.Exists(l.RepoPath!))
            .Select(l => $"{l.Original}  →  {l.RepoPath}");

        Assert.Empty(missing);
    }

    [Theory, MemberData(nameof(Routes))]
    public void Every_link_to_a_heading_lands_on_a_heading_that_exists(string route)
    {
        var broken = new List<string>();

        foreach (var link in Rendered[route].Links.Where(l => l.Kind is LinkKind.Fragment or LinkKind.Site))
        {
            var hash = link.Resolved.IndexOf('#');
            if (hash < 0) continue;

            var targetRoute = link.Resolved[..hash];
            var fragment = link.Resolved[(hash + 1)..];

            if (!Rendered.TryGetValue(targetRoute, out var target)) continue;
            if (!target.HeadingIds.Contains(fragment))
                broken.Add($"{link.Original}  →  no heading '{fragment}' on {targetRoute}");
        }

        Assert.Empty(broken);
    }

    // ---- structure ------------------------------------------------------------------

    [Theory, MemberData(nameof(Routes))]
    public void Heading_ids_are_unique(string route)
    {
        var ids = Rendered[route].HeadingIds;

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Theory, MemberData(nameof(Routes))]
    public void Every_page_has_a_title(string route)
    {
        Assert.False(string.IsNullOrWhiteSpace(Rendered[route].Title));
    }

    [Theory, MemberData(nameof(Routes))]
    public void Rendering_keeps_every_details_block(string route)
    {
        // The collapsible blocks are how solutions and "legacy SQL" asides are written.
        // If the pipeline ever swallowed one, learners would silently lose content.
        var source = RepoFiles.Read(Page(route).SourcePath);
        var expected = Regex.Matches(source, @"^\s*<details[ >]", RegexOptions.Multiline).Count;
        var actual = Regex.Matches(Rendered[route].Html, "<details[ >]").Count;

        Assert.Equal(expected, actual);
    }

    [Theory, MemberData(nameof(Routes))]
    public void Rendering_keeps_every_code_block(string route)
    {
        var source = RepoFiles.Read(Page(route).SourcePath);
        // A fence can open straight after a list marker ("6. ```bash"), so allow for one.
        var expected = Regex.Matches(source, @"^\s*(?:\d+[.)]\s+|[-*+]\s+)?```", RegexOptions.Multiline).Count / 2;
        var actual = Regex.Matches(Rendered[route].Html, "<pre><code").Count;

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Part_1_has_all_twelve_steps_in_its_table_of_contents()
    {
        var steps = Rendered["parts/1"].Toc
            .Where(t => t.Level == 3)
            .Select(t => Regex.Match(t.Text, @"^Step (\d+) —"))
            .Where(m => m.Success)
            .Select(m => int.Parse(m.Groups[1].Value))
            .ToList();

        Assert.Equal(Enumerable.Range(1, 12), steps);
    }

    [Fact]
    public void Part_1_solutions_has_all_twelve_solutions()
    {
        // The same file CI extracts the solutions from, so it also guards that extraction.
        var summaries = Regex.Matches(Rendered["parts/1/solutions"].Html, @"<summary><b>Step (\d+) —");

        Assert.Equal(12, summaries.Count);
    }

    [Theory, MemberData(nameof(Routes))]
    public void No_code_span_shows_a_stray_table_escape(string route)
    {
        // A "\\|" visible inside <code> means a table escape leaked through to the reader.
        var leaks = Regex.Matches(Rendered[route].Html, @"<code>[^<]*\\\|[^<]*</code>").Select(m => m.Value);

        Assert.Empty(leaks);
    }

    // ---- nothing hostile has crept into our own files -------------------------------

    [Theory, MemberData(nameof(Routes))]
    public void No_page_contains_live_script_or_event_handlers(string route)
    {
        var html = Rendered[route].Html;

        Assert.DoesNotMatch(new Regex("<script", RegexOptions.IgnoreCase), html);
        Assert.DoesNotMatch(new Regex(@"<[^>]*\son\w+\s*=", RegexOptions.IgnoreCase), html);
        Assert.DoesNotMatch(new Regex(@"(href|src)=""\s*javascript:", RegexOptions.IgnoreCase), html);
    }
}
