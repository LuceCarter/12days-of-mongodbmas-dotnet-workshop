namespace SantasWorkshop.Content;

/// <summary>One markdown file the site renders, and the route it is served from.</summary>
/// <param name="Route">Site route with no leading slash, e.g. <c>parts/1</c>.</param>
/// <param name="SourcePath">Repo-relative path using forward slashes, e.g. <c>part1-inventory/README.md</c>.</param>
public sealed record ContentPage(string Route, string SourcePath, string Title);

/// <summary>The facts about a part that decide which pages it contributes.</summary>
public sealed record PartSource(string Id, string Folder, string Title, bool Available);

/// <summary>
/// Which repository files are rendered inside the site, and where.
///
/// This is what lets a link such as <c>../docs/setup.md</c> inside a README become an
/// in-app link on the site while remaining a perfectly good relative link on GitHub.
/// Anything not in the catalog is sent to GitHub instead.
/// </summary>
public sealed class ContentCatalog
{
    private readonly Dictionary<string, ContentPage> bySource;
    private readonly Dictionary<string, ContentPage> byRoute;

    public ContentCatalog(IEnumerable<ContentPage> pages)
    {
        Pages = pages.ToList();
        bySource = Pages.ToDictionary(p => p.SourcePath, StringComparer.OrdinalIgnoreCase);
        byRoute = Pages.ToDictionary(p => p.Route, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ContentPage> Pages { get; }

    /// <summary>Docs that are always rendered, whatever parts have been released.</summary>
    public static IReadOnlyList<ContentPage> FixedDocs { get; } =
    [
        new("setup", "docs/setup.md", "Get set up"),
        new("docs/csharp-to-badge", "docs/csharp-to-badge.md", "From C# to the badge"),
        new("docs/objective-map", "docs/objective-map.md", "Objective map"),
    ];

    /// <summary>
    /// The fixed docs plus a README and a SOLUTIONS page for every <em>released</em> part.
    /// Releasing a part is therefore a one-line change in appsettings.json.
    /// </summary>
    public static ContentCatalog Create(IEnumerable<PartSource> parts)
    {
        var pages = new List<ContentPage>(FixedDocs);

        foreach (var part in parts.Where(p => p.Available))
        {
            var title = $"Part {part.Id} — {part.Title}";
            pages.Add(new($"parts/{part.Id}", $"{part.Folder}/README.md", title));
            pages.Add(new($"parts/{part.Id}/solutions", $"{part.Folder}/SOLUTIONS.md", $"{title} — Solutions"));
        }

        return new ContentCatalog(pages);
    }

    public bool TryGetBySource(string sourcePath, out ContentPage page) =>
        bySource.TryGetValue(sourcePath, out page!);

    public bool TryGetByRoute(string route, out ContentPage page) =>
        byRoute.TryGetValue(route.Trim('/'), out page!);
}
