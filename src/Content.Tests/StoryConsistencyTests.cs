using System.Text.RegularExpressions;

namespace SantasWorkshop.Content.Tests;

/// <summary>
/// The home page and Part 1 both tell the story of the workshop's ledger. A reader who clicks
/// from one to the other should meet the same workshop. These facts are the ones the two
/// versions share; if someone rewrites one story, this fails until the other is looked at too.
/// </summary>
public sealed class StoryConsistencyTests
{
    // Compared with markup, line breaks and curly quotes removed, so layout doesn't matter.
    private static string Words(string text) =>
        Regex.Replace(Regex.Replace(text, @"<[^>]+>|@\*.*?\*@", " ", RegexOptions.Singleline), @"\s+", " ").ToLowerInvariant();

    private static readonly string Home = Words(RepoFiles.Read("LandingPage/Pages/Home.razor"));
    private static readonly string Part1Story = Words(
        Regex.Match(RepoFiles.Read("part1-inventory/README.md"), @"## The story(.*?)\n---", RegexOptions.Singleline).Groups[1].Value);

    [Theory]
    [InlineData("leather ledger")]
    [InlineData("fountain pen")]
    [InlineData("four thousand pages")]
    [InlineData("repetitive strain injury")]
    [InlineData("cocoa")]
    [InlineData("letter m")]
    public void The_home_page_and_Part_1_tell_the_same_story(string fact)
    {
        Assert.True(Part1Story.Contains(fact), $"Part 1's story no longer says \"{fact}\". Update the home page hero to match, or change this list.");
        Assert.True(Home.Contains(fact), $"The home page no longer says \"{fact}\", which Part 1's story does. Keep the two in step.");
    }

    [Fact]
    public void The_home_page_story_has_no_date_that_will_go_stale()
    {
        // These challenges are meant to still work in February. Part 1 may say "the third of
        // November"; the home page, which everyone sees first, must not pin the story to a day.
        var hero = Home[..Home.IndexOf("how it works", StringComparison.Ordinal)];

        Assert.DoesNotMatch(new Regex(@"\b(january|february|march|april|may|june|july|august|september|october|november|december)\b"), hero);
        Assert.DoesNotMatch(new Regex(@"\b20\d\d\b"), hero);
    }
}
