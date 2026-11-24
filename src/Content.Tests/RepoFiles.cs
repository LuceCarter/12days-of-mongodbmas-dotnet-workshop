using System.Text.Json;

namespace SantasWorkshop.Content.Tests;

/// <summary>Finds the repository on disk, and reads the landing page's own configuration from it.</summary>
internal static class RepoFiles
{
    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SantasWorkshop.slnx")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not find the repository root (SantasWorkshop.slnx).");
    }

    public static string Read(string repoPath) => File.ReadAllText(Path.Combine(Root, repoPath));

    public static bool Exists(string repoPath) =>
        File.Exists(Path.Combine(Root, repoPath)) || Directory.Exists(Path.Combine(Root, repoPath.TrimEnd('/')));

    private static JsonElement Settings() =>
        JsonDocument.Parse(Read("LandingPage/wwwroot/appsettings.json")).RootElement;

    public static string RepoUrl => Settings().GetProperty("Repo").GetString()!;

    public static IReadOnlyList<PartSource> Parts() =>
        Settings().GetProperty("Parts").EnumerateArray()
            .Select(p => new PartSource(
                p.GetProperty("Id").GetString()!,
                p.GetProperty("Folder").GetString()!,
                p.GetProperty("Title").GetString()!,
                p.GetProperty("Available").GetBoolean()))
            .ToList();

    /// <summary>
    /// Parts that exist in the repository: released ones, plus any with a README already written.
    /// A part being written is audited exactly like a released one, so when it is released the
    /// only change is the <c>"Available"</c> flag.
    /// </summary>
    public static IReadOnlyList<PartSource> WrittenParts() =>
        Parts().Where(p => p.Available || Exists($"{p.Folder}/README.md")).ToList();

    /// <summary>
    /// The catalog as the landing page will build it once every <em>written</em> part is released.
    /// (The live site only includes parts with <c>"Available": true</c>; this is deliberately wider
    /// so unreleased parts' pages are checked too.)
    /// </summary>
    public static ContentCatalog Catalog() =>
        ContentCatalog.Create(WrittenParts().Select(p => p with { Available = true }));

    public static MarkdownRenderer Renderer() => new(Catalog(), RepoUrl);
}
