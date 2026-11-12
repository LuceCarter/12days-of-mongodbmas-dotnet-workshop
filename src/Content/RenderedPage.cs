namespace SantasWorkshop.Content;

/// <summary>A heading that appears in the page's table of contents.</summary>
public sealed record TocEntry(int Level, string Id, string Text);

public enum LinkKind
{
    /// <summary>A link to another page the site renders itself.</summary>
    Site,
    /// <summary>A link that jumps to a heading on the current page.</summary>
    Fragment,
    /// <summary>A repository file or folder, sent to GitHub.</summary>
    GitHub,
    /// <summary>Any other http(s) or mailto link.</summary>
    External,
    /// <summary>A link with a scheme we refuse (javascript:, data:, …). Rendered inert.</summary>
    Blocked,
}

/// <summary>What happened to one link while rendering. Mainly for tests and audits.</summary>
/// <param name="Original">The link as written in the markdown.</param>
/// <param name="Resolved">The URL that was actually rendered.</param>
/// <param name="RepoPath">For <see cref="LinkKind.GitHub"/> and <see cref="LinkKind.Site"/>: the repo-relative path it points at.</param>
public sealed record LinkInfo(string Original, string Resolved, LinkKind Kind, string? RepoPath);

public sealed record RenderedPage(
    string Html,
    string? Title,
    IReadOnlyList<TocEntry> Toc,
    IReadOnlyList<LinkInfo> Links,
    IReadOnlyList<string> HeadingIds);
