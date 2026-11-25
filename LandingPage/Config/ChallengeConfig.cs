using System.Text.Json;
using System.Text.Json.Serialization;

namespace SantasWorkshop.LandingPage.Config;

/// <summary>
/// Everything the landing page knows about the challenge, loaded once at startup
/// from <c>wwwroot/appsettings.json</c>.
///
/// There is deliberately no other source of links in this project. When the badge
/// URLs arrive from MongoDB EDU, that one file changes and nothing else does.
/// </summary>
public sealed class ChallengeConfig
{
    public string Repo { get; init; } = "";
    public string BadgeHub { get; init; } = "";

    public IReadOnlyList<Part> Parts { get; init; } = [];
    public IReadOnlyList<Badge> LevelUpBadges { get; init; } = [];
    public IReadOnlyList<Resource> Resources { get; init; } = [];

    /// <summary>
    /// The repository's name — also the folder <c>git clone</c> creates. Derived from
    /// <see cref="Repo"/> so there is only one place it can be wrong.
    /// </summary>
    public string RepoName => Repo.TrimEnd('/').Split('/')[^1];

    /// <summary>A direct link to a part's README in the repo.</summary>
    public string ReadmeUrl(Part part) => $"{Repo}/blob/main/{part.Folder}/README.md";

    /// <summary>
    /// Applies a local <c>appsettings.Development.json</c>, if there is one. It can point a
    /// local run at a repo that actually exists (<c>Repo</c>, <c>BadgeHub</c>) and release
    /// parts for preview (<c>ReleaseParts</c>), without duplicating the whole file — and,
    /// because that file is git-ignored, without any risk of it reaching <c>main</c>.
    /// </summary>
    public ChallengeConfig With(DevelopmentOverrides? overrides)
    {
        if (overrides is null) return this;

        return new ChallengeConfig
        {
            Repo = overrides.Repo ?? Repo,
            BadgeHub = overrides.BadgeHub ?? BadgeHub,
            Parts = ReleasedForPreview(overrides.ReleaseParts),
            LevelUpBadges = LevelUpBadges,
            Resources = Resources,
        };
    }

    /// <summary>Ids from <paramref name="requested"/> that match no part (a typo, usually).</summary>
    public IReadOnlyList<string> UnknownPartIds(IEnumerable<string>? requested) =>
        (requested ?? []).Where(id => id != "*" && !Parts.Any(p => Matches(p, id))).ToList();

    private IReadOnlyList<Part> ReleasedForPreview(IEnumerable<string>? requested)
    {
        var ids = requested?.ToList();
        if (ids is null or { Count: 0 }) return Parts;

        return Parts.Select(p => ids.Any(id => id == "*" || Matches(p, id)) ? p.WithAvailable(true) : p).ToList();
    }

    private static bool Matches(Part part, string id) => part.Id.Equals(id, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The shape of <c>wwwroot/appsettings.Development.json</c>, which is git-ignored
/// and exists only on a developer's machine.
/// </summary>
public sealed class DevelopmentOverrides
{
    public string? Repo { get; init; }
    public string? BadgeHub { get; init; }

    /// <summary>
    /// Part ids to treat as released on this machine only: <c>["2", "3A"]</c>, or <c>["*"]</c> for
    /// all of them. Lets you preview a part you are still writing without editing the committed
    /// <c>"Available"</c> flag — which, once pushed, is the release.
    /// </summary>
    public string[]? ReleaseParts { get; init; }
}

public sealed class Part
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Released { get; init; } = "";
    public string Level { get; init; } = "";

    /// <summary>How long the core path takes, as the part's own README says (an estimate), e.g. "About an hour".</summary>
    public string Time { get; init; } = "30–60 minutes";

    public bool Available { get; init; }
    public string Summary { get; init; } = "";
    public IReadOnlyList<string> Teaches { get; init; } = [];
    public string Folder { get; init; } = "";
    public IReadOnlyList<Badge> Badges { get; init; } = [];

    public string Heading => $"Part {Id} — {Title}";

    public Part WithAvailable(bool available) => new()
    {
        Id = Id, Title = Title, Released = Released, Level = Level, Time = Time, Available = available,
        Summary = Summary, Teaches = Teaches, Folder = Folder, Badges = Badges,
    };
}

public sealed class Badge
{
    public string Name { get; init; } = "";
    public string Url { get; init; } = "";
    public string? Part { get; init; }

    /// <summary>
    /// True until the real badge URLs land. Lets the UI show an honest
    /// "link coming soon" instead of a dead link.
    /// </summary>
    public bool IsPlaceholder => Url.Contains("REPLACE_ME", StringComparison.Ordinal);
}

public sealed class Resource
{
    public string Name { get; init; } = "";
    public string Url { get; init; } = "";
    public string Note { get; init; } = "";
}

/// <summary>
/// Source-generated serialisation. Reflection-based System.Text.Json is unreliable
/// once the published WebAssembly output is trimmed, so the context is explicit.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ChallengeConfig))]
[JsonSerializable(typeof(DevelopmentOverrides))]
public sealed partial class ChallengeConfigContext : JsonSerializerContext;
