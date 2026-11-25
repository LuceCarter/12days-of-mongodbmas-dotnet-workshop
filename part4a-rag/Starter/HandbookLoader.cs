using SantasWorkshop.Shared;

namespace Part4A.Starter;

/// <summary>One section of the Elf Handbook, ready to be cut into chunks.</summary>
/// <param name="Section">The heading, such as <c>Toy Safety</c>.</param>
/// <param name="Audience">Who it is written for, such as <c>toy-makers</c> or <c>all-elves</c>.</param>
/// <param name="Body">The section's text, paragraphs and all.</param>
public sealed record HandbookSection(string Section, string Audience, string Body);

/// <summary>
/// Step 1: collect the <b>unstructured</b> source, the Elf Handbook, and prepare it.
/// </summary>
public static class HandbookLoader
{
    /// <summary>The handbook as shipped with the repository, already parsed by <see cref="ParseHandbook"/>.</summary>
    public static List<HandbookSection> Load() => ParseHandbook(SeedLoader.LoadHandbookText());

    /// <summary>
    /// Step 1 (badge objectives: collect and prepare data; unstructured sources) — split the handbook's markdown
    /// into its sections.
    /// </summary>
    /// <remarks>
    /// The file looks like this. Anything before the first <c>## </c> heading is a title and a note, not a section.
    /// <code>
    /// # The Elf Handbook
    ///
    /// ## Toy Safety
    /// Audience: toy-makers
    ///
    /// Every toy leaves this workshop safe ...
    /// </code>
    /// </remarks>
public static List<HandbookSection> ParseHandbook(string markdown)
    {
        // TODO (Step 1): for each "## " heading, return a HandbookSection with
        //   Section   the heading text, without the "## "
        //   Audience  the text after "Audience:" on the line under the heading, trimmed
        //   Body      everything after that line, trimmed: the paragraphs, with their blank lines left alone
        //
        // Things to get right:
        //   - Anything before the first "## " heading is a title and a note, not a section.
        //   - The file may have Windows line endings (\r\n). Don't let a stray \r end up in a name or an audience.
        //   - Throw an InvalidOperationException that names the section if one has no "Audience:" line:
        //     a chunk with no audience can't be filtered later, and you want to find out HERE, not in Step 6.
        throw new NotImplementedException("Step 1: split the handbook into sections.");
    }
}
