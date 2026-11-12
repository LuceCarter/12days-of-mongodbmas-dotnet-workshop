using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace SantasWorkshop.Content;

/// <summary>
/// Renders one of the repository's markdown files as HTML for the landing page.
///
/// The files are written for GitHub first. This class adapts them to the site rather
/// than the other way round, so a README reads correctly in both places and there is
/// only ever one copy of it.
/// </summary>
/// <param name="catalog">Which files the site renders itself.</param>
/// <param name="repoUrl">e.g. <c>https://github.com/owner/repo</c> — where everything else goes.</param>
/// <param name="branch">The branch GitHub links point at.</param>
public sealed partial class MarkdownRenderer(ContentCatalog catalog, string repoUrl, string branch = "main")
{
    private readonly string repoUrl = repoUrl.TrimEnd('/');

    // Extensions are chosen one by one. UseAdvancedExtensions() is deliberately NOT used:
    // it includes generic attributes ("{onclick=...}"), which would let markdown set
    // arbitrary attributes on elements.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseAutoLinks()
        .UseEmphasisExtras()
        .UseTaskLists()
        .Build();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.\-]*:")]
    private static partial Regex SchemePattern();

    /// <param name="markdown">The file's text.</param>
    /// <param name="sourcePath">Its repo-relative path (forward slashes), used to resolve relative links.</param>
    public RenderedPage Render(string markdown, string sourcePath)
    {
        var document = Markdown.Parse(markdown, Pipeline);

        UnescapeTablePipes(document);

        var (toc, headingIds) = AssignHeadingIds(document);
        var links = RewriteLinks(document, sourcePath);

        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.ObjectRenderers.Replace<HtmlBlockRenderer>(new SafeHtmlBlockRenderer());
        renderer.ObjectRenderers.Replace<HtmlInlineRenderer>(new SafeHtmlInlineRenderer());
        renderer.Render(document);
        writer.Flush();

        var title = document.Descendants<HeadingBlock>().FirstOrDefault(h => h.Level == 1);
        return new RenderedPage(writer.ToString(), title is null ? null : PlainText(title), toc, links, headingIds);
    }

    // ---- tables ---------------------------------------------------------------------

    // In a table cell a bare | ends the cell, so a pipe inside a code span has to be written
    // \| — and GitHub then shows it as |. Markdig keeps the backslash, so `a \|\| b` would
    // render as a \|\| b. Undo the escape the way GitHub does.
    private static void UnescapeTablePipes(MarkdownDocument document)
    {
        foreach (var table in document.Descendants<Table>())
            foreach (var code in table.Descendants<CodeInline>())
                code.Content = code.Content.Replace("\\|", "|");
    }

    // ---- headings -------------------------------------------------------------------

    private static (List<TocEntry> Toc, List<string> AllIds) AssignHeadingIds(MarkdownDocument document)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var toc = new List<TocEntry>();
        var ids = new List<string>();

        foreach (var heading in document.Descendants<HeadingBlock>())
        {
            var text = PlainText(heading);
            var id = GitHubSlug.Unique(GitHubSlug.Create(text), used);

            heading.GetAttributes().Id = id;
            ids.Add(id);

            if (heading.Level is 2 or 3) toc.Add(new TocEntry(heading.Level, id, text));
        }

        return (toc, ids);
    }

    private static string PlainText(LeafBlock block)
    {
        var text = new StringBuilder();

        foreach (var inline in block.Inline?.Descendants() ?? [])
        {
            switch (inline)
            {
                case LiteralInline literal: text.Append(literal.Content.ToString()); break;
                case CodeInline code: text.Append(code.Content); break;
                case LineBreakInline: text.Append(' '); break;
            }
        }

        return text.ToString().Trim();
    }

    // ---- links ----------------------------------------------------------------------

    private List<LinkInfo> RewriteLinks(MarkdownDocument document, string sourcePath)
    {
        var results = new List<LinkInfo>();

        foreach (var link in document.Descendants<LinkInline>())
        {
            var original = link.Url ?? "";
            var info = Resolve(original, sourcePath, link.IsImage);

            link.Url = info.Resolved;
            results.Add(info);

            if (!link.IsImage && info.Kind is LinkKind.External or LinkKind.GitHub)
            {
                var attributes = link.GetAttributes();
                attributes.AddPropertyIfNotExist("target", "_blank");
                attributes.AddPropertyIfNotExist("rel", "noopener");
            }
        }

        return results;
    }

    private LinkInfo Resolve(string url, string sourcePath, bool isImage)
    {
        // Browsers ignore tabs and newlines inside a scheme ("java\tscript:"), so check without them.
        var probe = new string(url.Where(c => !char.IsControl(c) && !char.IsWhiteSpace(c)).ToArray());

        if (probe.Length == 0) return new(url, url, LinkKind.Fragment, null);

        if (probe.StartsWith("//", StringComparison.Ordinal))
            return new(url, "#", LinkKind.Blocked, null);

        if (SchemePattern().IsMatch(probe))
        {
            var allowed = probe.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                       || probe.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                       || (!isImage && probe.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase));

            return allowed ? new(url, url, LinkKind.External, null) : new(url, "#", LinkKind.Blocked, null);
        }

        // From here the link is relative. Split off the fragment and query.
        var hash = url.IndexOf('#');
        var fragment = hash >= 0 ? url[hash..] : "";
        var path = hash >= 0 ? url[..hash] : url;
        var query = path.IndexOf('?');
        if (query >= 0) path = path[..query];

        // "#heading" — the same page. A bare "#x" would resolve against <base href>, i.e.
        // the site root, so anchor it to this page's own route.
        if (path.Length == 0)
        {
            return catalog.TryGetBySource(sourcePath, out var self)
                ? new(url, $"{self.Route}{fragment}", LinkKind.Fragment, sourcePath)
                : new(url, $"{GitHubUrl("blob", sourcePath)}{fragment}", LinkKind.GitHub, sourcePath);
        }

        var repoPath = ResolveRepoPath(sourcePath, Uri.UnescapeDataString(path));
        if (repoPath is null) return new(url, "#", LinkKind.Blocked, null);

        if (isImage)
            return new(url, $"{RawUrl(repoPath)}", LinkKind.GitHub, repoPath);

        if (catalog.TryGetBySource(repoPath, out var page))
            return new(url, $"{page.Route}{fragment}", LinkKind.Site, repoPath);

        // A trailing slash, or a last segment with no extension, is a folder.
        var isFolder = repoPath.EndsWith('/') || !Path.GetFileName(repoPath.TrimEnd('/')).Contains('.');
        var kind = isFolder ? "tree" : "blob";
        var canonical = repoPath.TrimEnd('/');
        return new(url, $"{GitHubUrl(kind, canonical)}{fragment}", LinkKind.GitHub, canonical);
    }

    /// <summary>
    /// Resolves <paramref name="relative"/> against the folder containing <paramref name="sourcePath"/>,
    /// returning a repo-relative path — or null if it climbs out of the repository.
    /// </summary>
    public static string? ResolveRepoPath(string sourcePath, string relative)
    {
        var segments = new List<string>(sourcePath.Split('/')[..^1]);

        if (relative.StartsWith('/')) segments.Clear();

        foreach (var part in relative.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;
            if (part == "..")
            {
                if (segments.Count == 0) return null;
                segments.RemoveAt(segments.Count - 1);
                continue;
            }
            segments.Add(part);
        }

        var path = string.Join('/', segments);
        return relative.EndsWith('/') ? path + "/" : path;
    }

    private string GitHubUrl(string kind, string repoPath) => $"{repoUrl}/{kind}/{branch}/{repoPath}";

    private string RawUrl(string repoPath)
    {
        // https://github.com/owner/repo -> https://raw.githubusercontent.com/owner/repo/main/path
        var slug = repoUrl.Replace("https://github.com/", "", StringComparison.OrdinalIgnoreCase);
        return $"https://raw.githubusercontent.com/{slug}/{branch}/{repoPath}";
    }

    // ---- raw HTML -------------------------------------------------------------------

    private sealed class SafeHtmlBlockRenderer : HtmlObjectRenderer<HtmlBlock>
    {
        protected override void Write(HtmlRenderer renderer, HtmlBlock block)
        {
            if (block.Type == HtmlBlockType.Comment) return;

            renderer.EnsureLine();
            renderer.Write(HtmlAllowlist.Sanitize(block.Lines.ToString()));
            renderer.WriteLine();
        }
    }

    private sealed class SafeHtmlInlineRenderer : HtmlObjectRenderer<HtmlInline>
    {
        protected override void Write(HtmlRenderer renderer, HtmlInline inline) =>
            renderer.Write(HtmlAllowlist.Sanitize(inline.Tag));
    }
}
