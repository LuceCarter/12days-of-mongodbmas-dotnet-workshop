using System.Text.Json;
using System.Text.RegularExpressions;
using SantasWorkshop.Content;

// Prints every URL route the landing page can serve, one per line, with no leading slash.
//
//   dotnet run --project tools/site-routes
//
// The Pages deploy uses this to give each route its own index.html. GitHub Pages answers
// an unknown path with a 404 status and only *then* serves 404.html — fine for people, but
// crawlers and link previews see a 404. A real file per route returns 200.
//
// The list comes from the same places the app itself uses, so it cannot drift:
//   - the ContentCatalog built from appsettings.json (parts, setup, docs), and
//   - every parameter-free @page route in the Razor files (home, parts, resources, ...).
// Parts that are not released yet are included: /parts/{id} shows a "coming soon" card.

var root = args.Length > 0 ? args[0] : FindRoot();

var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "LandingPage", "wwwroot", "appsettings.json"))).RootElement;

var parts = settings.GetProperty("Parts").EnumerateArray()
    .Select(p => new PartSource(
        p.GetProperty("Id").GetString()!,
        p.GetProperty("Folder").GetString()!,
        p.GetProperty("Title").GetString()!,
        p.GetProperty("Available").GetBoolean()))
    .ToList();

var routes = new SortedSet<string>(StringComparer.Ordinal);

foreach (var page in ContentCatalog.Create(parts).Pages) routes.Add(page.Route);
foreach (var part in parts) routes.Add($"parts/{part.Id}");

var pagePattern = new Regex(@"^@page\s+""/(?<route>[^""{}]*)""", RegexOptions.Multiline);
foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "LandingPage", "Pages"), "*.razor"))
    foreach (Match match in pagePattern.Matches(File.ReadAllText(file)))
        routes.Add(match.Groups["route"].Value.Trim('/'));

// The root is index.html itself.
routes.Remove("");

foreach (var route in routes) Console.WriteLine(route);

static string FindRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SantasWorkshop.slnx")))
        directory = directory.Parent;
    return directory?.FullName ?? throw new InvalidOperationException("Run this from inside the repository.");
}
