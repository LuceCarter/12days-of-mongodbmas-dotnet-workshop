namespace SantasWorkshop.Content.Tests;

/// <summary>
/// Heading ids must match GitHub's, so a link written for GitHub works on the site too.
/// The expected values below follow GitHub's published algorithm.
/// </summary>
public sealed class GitHubSlugTests
{
    [Theory]
    [InlineData("Step 1 — Add a toy · objective 1.1", "step-1--add-a-toy--objective-11")]
    [InlineData("Part A — Stocking the shelves", "part-a--stocking-the-shelves")]
    [InlineData("Option 1 — GitHub Codespaces (easiest)", "option-1--github-codespaces-easiest")]
    [InlineData("✅ You're done", "-youre-done")]
    [InlineData("📋 From C# to the badge", "-from-c-to-the-badge")]
    [InlineData("🚀 Level up", "-level-up")]
    [InlineData("snake_case stays", "snake_case-stays")]
    public void Matches_what_GitHub_generates(string heading, string expected)
    {
        Assert.Equal(expected, GitHubSlug.Create(heading));
    }

    [Fact]
    public void Repeated_headings_get_numbered_suffixes_like_GitHub()
    {
        var used = new HashSet<string>();

        var ids = new[] { "Setup", "Setup", "Setup" }
            .Select(h => GitHubSlug.Unique(GitHubSlug.Create(h), used))
            .ToArray();

        Assert.Equal(["setup", "setup-1", "setup-2"], ids);
    }
}
