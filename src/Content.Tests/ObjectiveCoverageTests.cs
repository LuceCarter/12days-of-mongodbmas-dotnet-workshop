using System.Text.RegularExpressions;

namespace SantasWorkshop.Content.Tests;

/// <summary>
/// The project's contract: every objective a badge assesses must have a step that teaches it and
/// a checklist line that points at it. <c>docs/objective-map.md</c> is where the objectives are
/// written down exactly; these tests make sure each part's README repeats every one of them.
/// </summary>
public sealed class ObjectiveCoverageTests
{
    /// <summary>The objectives in the first table under "## Part {id} —", exactly as written.</summary>
    private static List<string> ObjectivesFor(string partId)
    {
        var map = RepoFiles.Read("docs/objective-map.md");
        var section = Regex.Match(map, $@"^## Part {Regex.Escape(partId)} —.*?(?=^## |\z)", RegexOptions.Singleline | RegexOptions.Multiline).Value;

        // First table only: later tables under "###" headings are stretch badges, taught in "Level up".
        var firstTable = Regex.Match(section, @"(^\|.*\n)+", RegexOptions.Multiline).Value;

        return firstTable.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(row => row.Split('|').Select(c => c.Trim()).ToArray())
            .Where(cells => cells.Length > 2 && !cells[1].StartsWith("---") && cells[1] is not ("Objective" or "#"))
            // Part 1's table has a "#" column (1.1, 1.2 …) before the objective; Part 2's does not.
            .Select(cells => Regex.IsMatch(cells[1], @"^\d+\.\d+$") ? cells[2] : cells[1])
            .ToList();
    }

    private static string Normalise(string text) =>
        Regex.Replace(text.Replace("→", "->").Replace("`", "").Replace("**", ""), @"\s+", " ").Trim().ToLowerInvariant();

    [Fact]
    public void The_objective_map_lists_objectives_for_every_written_part()
    {
        foreach (var part in RepoFiles.WrittenParts())
            Assert.True(ObjectivesFor(part.Id).Count > 0, $"docs/objective-map.md has no objectives for Part {part.Id}.");
    }

    [Fact]
    public void Every_readme_checklist_repeats_every_objective_from_the_objective_map()
    {
        var missing = new List<string>();

        foreach (var part in RepoFiles.WrittenParts())
        {
            var readme = Normalise(RepoFiles.Read($"{part.Folder}/README.md"));

            foreach (var objective in ObjectivesFor(part.Id))
                if (!readme.Contains(Normalise(objective)))
                    missing.Add($"Part {part.Id}: \"{objective}\"");
        }

        Assert.True(missing.Count == 0,
            "These objectives are in docs/objective-map.md but not in the part's README checklist:\n  " + string.Join("\n  ", missing));
    }

    [Fact]
    public void Every_objective_is_marked_covered_in_the_map_once_its_part_is_written()
    {
        // A written part whose rows still say "not started" means the map is lying about what exists.
        var stale = new List<string>();

        foreach (var part in RepoFiles.WrittenParts())
        {
            var map = RepoFiles.Read("docs/objective-map.md");
            var section = Regex.Match(map, $@"^## Part {Regex.Escape(part.Id)} —.*?(?=^### |^## |\z)", RegexOptions.Singleline | RegexOptions.Multiline).Value;
            if (section.Contains("⬜")) stale.Add($"Part {part.Id} is written but docs/objective-map.md still shows ⬜ (not started) in its table.");
        }

        Assert.Empty(stale);
    }
}
