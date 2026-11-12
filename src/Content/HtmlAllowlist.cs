using System.Net;
using System.Text.RegularExpressions;

namespace SantasWorkshop.Content;

/// <summary>
/// Decides which raw HTML inside a markdown file is allowed through to the page.
///
/// Why this exists: the site renders markdown from the repository, and GitHub Pages
/// project sites share one origin (<c>username.github.io</c>) with every other Pages
/// site the same account owns. If raw HTML in markdown were injected unchecked, a single
/// careless pull request could run script on that origin. So the rule is an allowlist,
/// not a blocklist: a handful of harmless tags, with <b>no attributes</b> apart from
/// <c>open</c> on <c>&lt;details&gt;</c>. Anything else is shown as literal text.
///
/// A tag-and-attribute allowlist is deliberately small because the content only ever
/// uses <c>details</c>, <c>summary</c> and <c>b</c>. A general-purpose sanitiser would
/// do the same job at the cost of a large dependency in the WebAssembly download.
/// </summary>
public static partial class HtmlAllowlist
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "details", "summary", "b", "i", "em", "strong", "kbd", "code", "br", "sup", "sub",
    };

    // <tag>, </tag>, <tag attrs>, <tag/>. Attributes are captured only so they can be refused.
    [GeneratedRegex(@"<(?<close>/?)(?<name>[A-Za-z][A-Za-z0-9]*)(?<attrs>[^<>]*?)(?<self>/?)>")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex CommentPattern();

    /// <summary>Returns HTML that is safe to place on the page.</summary>
    public static string Sanitize(string html)
    {
        html = CommentPattern().Replace(html, "");

        var output = new System.Text.StringBuilder(html.Length);
        var cursor = 0;

        foreach (Match tag in TagPattern().Matches(html))
        {
            output.Append(EscapeStrayAngleBrackets(html[cursor..tag.Index]));
            output.Append(Allowed(tag) ?? WebUtility.HtmlEncode(tag.Value));
            cursor = tag.Index + tag.Length;
        }

        output.Append(EscapeStrayAngleBrackets(html[cursor..]));
        return output.ToString();
    }

    /// <summary>A normalised, attribute-free tag, or null if this one isn't permitted.</summary>
    private static string? Allowed(Match tag)
    {
        var name = tag.Groups["name"].Value.ToLowerInvariant();
        if (!AllowedTags.Contains(name)) return null;

        var attributes = tag.Groups["attrs"].Value.Trim();
        var closing = tag.Groups["close"].Value == "/";

        if (closing) return attributes.Length == 0 ? $"</{name}>" : null;

        if (attributes.Length == 0)
        {
            if (name == "br") return "<br>";
            return tag.Groups["self"].Value == "/" ? $"<{name}></{name}>" : $"<{name}>";
        }

        // The single attribute we permit: <details open>.
        if (name == "details" && attributes.Equals("open", StringComparison.OrdinalIgnoreCase))
            return "<details open>";

        return null;
    }

    // A '<' that did not form a tag is text. Say so explicitly rather than hoping the
    // browser agrees.
    private static string EscapeStrayAngleBrackets(string text) => text.Replace("<", "&lt;");
}
