using System.Text.RegularExpressions;

namespace SantasWorkshop.Content.Tests;

/// <summary>
/// index.html links to icons, stylesheets and scripts by relative path. A link to a file that
/// doesn't exist is a silent 404 on every page load — nothing fails, a console just fills with
/// errors (this once happened with a leftover scoped-styles link). These tests make it loud.
/// </summary>
public sealed class IndexHtmlTests
{
    private static readonly string Html = RepoFiles.Read("LandingPage/wwwroot/index.html");

    // Files the Blazor SDK produces at publish time, or that aren't files at all.
    private static bool IsGenerated(string reference) =>
        reference.StartsWith("_framework/", StringComparison.Ordinal)   // fingerprinted at publish
        || reference.Contains("#[")                                     // fingerprint placeholder
        || reference.StartsWith("http", StringComparison.OrdinalIgnoreCase)
        || reference is "" or "." or "/" or "#";

    private static IEnumerable<string> References() =>
        Regex.Matches(Html, @"<(?:link|script|img)\b[^>]*?\b(?:href|src)=""(?<ref>[^""]*)""")
             .Select(m => m.Groups["ref"].Value)
             .Where(r => !IsGenerated(r));

    [Fact]
    public void Every_file_index_html_links_to_exists_in_wwwroot()
    {
        var missing = References().Where(r => !RepoFiles.Exists($"LandingPage/wwwroot/{r}")).ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void The_page_actually_links_a_favicon_set()
    {
        // Guards the test above from passing vacuously, and keeps the icon set complete.
        var links = References().ToList();

        Assert.Contains("favicon.ico", links);
        Assert.Contains("favicon-32.png", links);
        Assert.Contains("favicon-16.png", links);
        Assert.Contains("apple-touch-icon.png", links);
        Assert.Contains("css/app.css", links);
    }

    private static string Meta(string attribute, string name) =>
        Regex.Match(Html, $@"<meta\s+{attribute}=""{name}""\s+content=""(?<c>[^""]*)""").Groups["c"].Value;

    [Fact]
    public void The_description_fits_in_a_search_result()
    {
        // Search engines show roughly 155-160 characters and cut the rest mid-sentence.
        var description = Meta("name", "description");

        Assert.False(string.IsNullOrWhiteSpace(description), "index.html has no meta description.");
        Assert.True(description.Length <= 160, $"The description is {description.Length} characters; search results cut it off at about 160.");
    }

    [Fact]
    public void Link_previews_have_a_title_and_description()
    {
        Assert.False(string.IsNullOrWhiteSpace(Meta("property", "og:title")));
        Assert.False(string.IsNullOrWhiteSpace(Meta("property", "og:description")));
    }

    [Theory]
    [InlineData("favicon-16.png", 16)]
    [InlineData("favicon-32.png", 32)]
    [InlineData("apple-touch-icon.png", 180)]
    public void Icons_are_really_the_size_their_name_says(string file, int expected)
    {
        // PNG: 8-byte signature, then the IHDR chunk, whose first two fields are width and height.
        var bytes = File.ReadAllBytes(Path.Combine(RepoFiles.Root, "LandingPage", "wwwroot", file));

        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes[..8]);

        var width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
        var height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));

        Assert.Equal((expected, expected), (width, height));
    }

    [Fact]
    public void The_ico_holds_16_32_and_48_pixel_images()
    {
        var bytes = File.ReadAllBytes(Path.Combine(RepoFiles.Root, "LandingPage", "wwwroot", "favicon.ico"));

        Assert.Equal((0, 1, 3), (BitConverter.ToUInt16(bytes, 0), BitConverter.ToUInt16(bytes, 2), BitConverter.ToUInt16(bytes, 4)));

        var sizes = Enumerable.Range(0, 3).Select(i => (int)bytes[6 + 16 * i]).ToList();   // 0 would mean 256

        Assert.Equal([16, 32, 48], sizes);
    }
}
