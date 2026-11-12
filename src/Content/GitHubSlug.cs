using System.Globalization;
using System.Text;

namespace SantasWorkshop.Content;

/// <summary>
/// Heading ids that match the ones GitHub generates, so a link such as
/// <c>README.md#step-3--change-a-price--objective-13</c> works on both GitHub and the site.
///
/// GitHub's rule: lower-case; drop everything that is not a letter, number, space,
/// hyphen or underscore; turn each space into a hyphen. Notably it does <em>not</em>
/// collapse runs of hyphens or trim them, so "Step 1 — Add" becomes <c>step-1--add</c>
/// and "✅ Done" becomes <c>-done</c>. Repeats get <c>-1</c>, <c>-2</c> suffixes.
/// </summary>
public static class GitHubSlug
{
    public static string Create(string text)
    {
        var slug = new StringBuilder(text.Length);

        foreach (var rune in text.ToLowerInvariant().EnumerateRunes())
        {
            if (rune.Value == ' ') { slug.Append('-'); continue; }
            if (rune.Value is '-' or '_') { slug.Append((char)rune.Value); continue; }

            // Emoji variation selectors are combining marks, but GitHub drops them with the emoji.
            if (rune.Value is 0xFE0E or 0xFE0F) continue;

            switch (Rune.GetUnicodeCategory(rune))
            {
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.LetterNumber:
                case UnicodeCategory.OtherNumber:
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.SpacingCombiningMark:
                    slug.Append(rune.ToString());
                    break;
            }
        }

        return slug.ToString();
    }

    /// <summary>Makes <paramref name="slug"/> unique within <paramref name="used"/>, GitHub-style.</summary>
    public static string Unique(string slug, ISet<string> used)
    {
        var candidate = slug;
        for (var n = 1; !used.Add(candidate); n++) candidate = $"{slug}-{n}";
        return candidate;
    }
}
