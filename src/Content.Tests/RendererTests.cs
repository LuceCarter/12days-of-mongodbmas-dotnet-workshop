using System.Text.RegularExpressions;

namespace SantasWorkshop.Content.Tests;

/// <summary>How individual markdown constructs are rendered, using small inline documents.</summary>
public sealed class RendererTests
{
    private const string Repo = "https://github.com/someone/some-repo";
    private const string Readme = "part1-inventory/README.md";

    private static MarkdownRenderer Renderer() => new(
        ContentCatalog.Create([new PartSource("1", "part1-inventory", "The Workshop Inventory", Available: true)]),
        Repo);

    private static RenderedPage Render(string markdown) => Renderer().Render(markdown, Readme);

    private static LinkInfo OnlyLink(string markdown) => Assert.Single(Render(markdown).Links);

    // ---- links ----------------------------------------------------------------------

    [Fact]
    public void A_link_to_a_sibling_markdown_file_becomes_a_site_link()
    {
        var link = OnlyLink("[answers](SOLUTIONS.md)");

        Assert.Equal(LinkKind.Site, link.Kind);
        Assert.Equal("parts/1/solutions", link.Resolved);
    }

    [Fact]
    public void A_link_up_a_folder_to_docs_becomes_a_site_link_and_keeps_its_fragment()
    {
        var link = OnlyLink("[setup](../docs/setup.md#option-2--local-with-docker)");

        Assert.Equal(LinkKind.Site, link.Kind);
        Assert.Equal("setup#option-2--local-with-docker", link.Resolved);
    }

    [Fact]
    public void A_link_to_a_source_file_goes_to_GitHub()
    {
        var link = OnlyLink("[starter](Starter/WorkshopInventory.cs)");

        Assert.Equal(LinkKind.GitHub, link.Kind);
        Assert.Equal($"{Repo}/blob/main/part1-inventory/Starter/WorkshopInventory.cs", link.Resolved);
    }

    [Fact]
    public void A_link_to_a_folder_goes_to_the_GitHub_tree()
    {
        Assert.Equal($"{Repo}/tree/main/part1-inventory/Starter", OnlyLink("[folder](Starter/)").Resolved);
        Assert.Equal($"{Repo}/tree/main/part1-inventory/Starter", OnlyLink("[folder](Starter)").Resolved);
    }

    [Fact]
    public void A_link_to_a_part_that_is_not_released_yet_goes_to_GitHub()
    {
        var link = OnlyLink("[next](../part2-ledger/README.md)");

        Assert.Equal(LinkKind.GitHub, link.Kind);
        Assert.Equal($"{Repo}/blob/main/part2-ledger/README.md", link.Resolved);
    }

    [Fact]
    public void A_link_to_a_heading_on_the_same_page_is_anchored_to_this_pages_route()
    {
        // A bare "#x" would resolve against <base href>, i.e. the site root, and leave the page.
        var link = OnlyLink("[jump](#step-3)");

        Assert.Equal(LinkKind.Fragment, link.Kind);
        Assert.Equal("parts/1#step-3", link.Resolved);
    }

    [Fact]
    public void External_links_open_in_a_new_tab_without_an_opener()
    {
        var page = Render("[docs](https://www.mongodb.com/docs/)");

        Assert.Equal(LinkKind.External, Assert.Single(page.Links).Kind);
        Assert.Contains("target=\"_blank\"", page.Html);
        Assert.Contains("rel=\"noopener\"", page.Html);
    }

    [Fact]
    public void Site_links_do_not_open_a_new_tab()
    {
        Assert.DoesNotContain("target=", Render("[answers](SOLUTIONS.md)").Html);
    }

    [Theory]
    [InlineData("[x](javascript:alert(1))")]
    [InlineData("[x](JaVaScRiPt:alert(1))")]
    [InlineData("[x](  javascript:alert(1))")]
    [InlineData("[x](data:text/html,hello)")]
    [InlineData("[x](vbscript:msgbox(1))")]
    [InlineData("[x](//evil.example/path)")]
    [InlineData("[x](../../outside-the-repo.md)")]
    [InlineData("![x](javascript:alert(1))")]
    [InlineData("![x](mailto:someone@example.com)")]
    public void Dangerous_links_are_made_inert(string markdown)
    {
        var page = Render(markdown);

        Assert.Equal(LinkKind.Blocked, Assert.Single(page.Links).Kind);
        Assert.DoesNotContain("javascript:", page.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evil.example", page.Html);
        // Inert, not removed: the element stays but points nowhere.
        Assert.Contains(markdown.StartsWith('!') ? "src=\"#\"" : "href=\"#\"", page.Html);
    }

    [Fact]
    public void Relative_images_load_from_GitHub()
    {
        var link = OnlyLink("![diagram](images/toys.png)");

        Assert.Equal("https://raw.githubusercontent.com/someone/some-repo/main/part1-inventory/images/toys.png", link.Resolved);
    }

    // ---- raw HTML and attributes ----------------------------------------------------

    [Fact]
    public void Script_in_markdown_is_shown_as_text_not_run()
    {
        var html = Render("Hello\n\n<script>alert(1)</script>\n\nand <img src=x onerror=alert(1)> inline").Html;

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generic_attributes_are_not_enabled()
    {
        // With Markdig's advanced extensions this would set onclick on the heading.
        var html = Render("# Title {onclick=alert(1)}").Html;

        // The heading text legitimately survives as visible text, and as a slug in the id
        // ("title-onclickalert1"). What must not exist is an onclick *attribute*.
        Assert.DoesNotMatch(new Regex(@"<[^>]*\sonclick\s*=", RegexOptions.IgnoreCase), html);
    }

    [Fact]
    public void Details_and_summary_survive_with_markdown_inside_them()
    {
        var html = Render("<details>\n<summary><b>Step 1</b></summary>\n\n```csharp\nvar x = 1;\n```\n\n</details>").Html;

        Assert.Contains("<details>", html);
        Assert.Contains("<summary><b>Step 1</b></summary>", html);
        Assert.Contains("language-csharp", html);
        Assert.Contains("</details>", html);
    }

    [Fact]
    public void Generic_types_inside_code_spans_are_text()
    {
        Assert.Contains("<code>Task&lt;string&gt;</code>", Render("Use `Task<string>` here.").Html);
    }

    [Fact]
    public void Tables_render()
    {
        var html = Render("| a | b |\n|---|---|\n| 1 | 2 |").Html;

        Assert.Contains("<table>", html);
        Assert.Contains("<td>1</td>", html);
    }

    [Fact]
    public void An_escaped_pipe_in_a_table_code_span_renders_as_a_pipe()
    {
        // Written \| so it doesn't end the cell; GitHub shows `a || b`, and so must we.
        var html = Render("| expr |\n|---|\n| `a \\|\\| b` |").Html;

        Assert.Contains("<code>a || b</code>", html);
        Assert.DoesNotContain("\\|", html);
    }

    [Fact]
    public void Pipes_outside_tables_are_left_alone()
    {
        Assert.Contains("<code>a \\|\\| b</code>", Render("Inline: `a \\|\\| b`").Html);
    }

    // ---- headings -------------------------------------------------------------------

    [Fact]
    public void Headings_get_GitHub_style_ids_and_feed_the_table_of_contents()
    {
        var page = Render("# Part 1 — Title\n\n## The story\n\n### Step 1 — Add a toy · *objective 1.1*\n\n#### too deep");

        Assert.Equal("Part 1 — Title", page.Title);
        Assert.Equal(
            [("the-story", 2), ("step-1--add-a-toy--objective-11", 3)],
            page.Toc.Select(t => (t.Id, t.Level)));
        Assert.Contains("id=\"step-1--add-a-toy--objective-11\"", page.Html);
    }

    [Fact]
    public void Repeated_headings_get_distinct_ids()
    {
        var page = Render("## Setup\n\n## Setup\n");

        Assert.Equal(["setup", "setup-1"], page.HeadingIds);
    }
}
