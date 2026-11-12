using System.Text.RegularExpressions;

namespace SantasWorkshop.Content.Tests;

/// <summary>
/// The site renders markdown from the repository on a shared github.io origin, so a
/// hostile or careless change to a .md file must not be able to run script.
/// </summary>
public sealed class HtmlAllowlistTests
{
    // After removing the tags we permit, no angle bracket should remain: anything else
    // must have been turned into text.
    private static readonly Regex PermittedTag =
        new(@"</?(details|summary|b|i|em|strong|kbd|code|br|sup|sub)( open)?>", RegexOptions.IgnoreCase);

    private static bool HasOnlyPermittedMarkup(string html) =>
        !PermittedTag.Replace(html, "").Contains('<');

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<iframe src=\"//evil.example\"></iframe>")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>")]
    [InlineData("<b onclick=\"alert(1)\">bold</b>")]
    [InlineData("<details onclick=\"alert(1)\"><summary>x</summary></details>")]
    [InlineData("<details style=\"x\">")]
    [InlineData("<svg onload=alert(1)>")]
    [InlineData("<style>body{display:none}</style>")]
    [InlineData("<form action=\"//evil.example\"><input></form>")]
    [InlineData("<SCRIPT>alert(1)</SCRIPT>")]
    [InlineData("<scr<script>ipt>alert(1)</scr</script>ipt>")]
    public void Hostile_markup_becomes_text(string hostile)
    {
        var html = HtmlAllowlist.Sanitize(hostile);

        Assert.True(HasOnlyPermittedMarkup(html), $"Live markup survived: {html}");
    }

    [Fact]
    public void The_html_the_content_actually_uses_is_untouched()
    {
        const string source = "<details>\n<summary><b>Step 1 — AddToyAsync</b> (objective 1.1)</summary>\n</details>";

        Assert.Equal(source, HtmlAllowlist.Sanitize(source));
    }

    [Fact]
    public void Details_can_still_start_open()
    {
        Assert.Equal("<details open>", HtmlAllowlist.Sanitize("<DETAILS OPEN>"));
    }

    [Fact]
    public void Tag_names_are_normalised_to_lower_case()
    {
        Assert.Equal("<b>x</b>", HtmlAllowlist.Sanitize("<B>x</B>"));
    }

    [Fact]
    public void Comments_are_dropped()
    {
        Assert.Equal("ab", HtmlAllowlist.Sanitize("a<!-- hidden <script> -->b"));
    }

    [Fact]
    public void A_stray_angle_bracket_is_text()
    {
        Assert.Equal("1 &lt; 2", HtmlAllowlist.Sanitize("1 < 2"));
    }

    [Fact]
    public void Generic_types_in_prose_are_text()
    {
        // "Task<string>" written outside a code span must not be swallowed as a tag.
        Assert.Equal("Task&lt;string&gt;", HtmlAllowlist.Sanitize("Task<string>"));
    }
}
