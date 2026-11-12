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

    /// <summary>The catalog exactly as the landing page will build it from appsettings.json.</summary>
    public static ContentCatalog Catalog() => ContentCatalog.Create(Parts());

    public static MarkdownRenderer Renderer() => new(Catalog(), RepoUrl);
}
