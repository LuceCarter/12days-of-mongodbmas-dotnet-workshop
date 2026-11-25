using System.Text.RegularExpressions;

namespace SantasWorkshop.Content.Tests;

/// <summary>
/// A learner arriving from the website has no repository yet. These tests make sure every
/// page that tells them to run a command or edit a file also says where the code is.
///
/// The repo URL has to be written literally in the markdown (GitHub renders these files
/// too, and it can't substitute a setting), so it exists in a few places. That is what the
/// first test is for: they must all agree with appsettings.json.
/// </summary>
public sealed class RepoReferenceTests
{
    private static readonly string RepoUrl = RepoFiles.RepoUrl;
    private static readonly string[] Shipped = RepoFiles.Catalog().Pages.Select(p => p.SourcePath).ToArray();

    [Fact]
    public void Every_repo_url_written_in_the_markdown_matches_the_configured_repo()
    {
        var configured = new Uri(RepoUrl).AbsolutePath.Trim('/').Split('/');   // owner/repo
        var wrong = new List<string>();

        foreach (var file in Shipped)
            foreach (Match m in Regex.Matches(RepoFiles.Read(file), @"https://github\.com/(?<owner>[A-Za-z0-9-]+)/(?<repo>[A-Za-z0-9._-]+)"))
            {
                var owner = m.Groups["owner"].Value;
                var repo = m.Groups["repo"].Value.RemoveSuffix(".git");

                // Links to other people's repositories are fine; ours must say the right name.
                if (owner.Equals(configured[0], StringComparison.OrdinalIgnoreCase) && repo != configured[1])
                    wrong.Add($"{file}: {m.Value}  (configured repo is {configured[1]})");
            }

        Assert.Empty(wrong);
    }

    [Fact]
    public void The_setup_guide_has_a_real_clone_command_and_no_placeholder()
    {
        var setup = RepoFiles.Read("docs/setup.md");

        Assert.DoesNotContain("<this repo>", setup);
        Assert.Contains($"git clone {RepoUrl}.git", setup);
        Assert.Contains($"cd {RepoUrl.Split('/')[^1]}", setup);
    }

    [Fact]
    public void Setup_options_that_need_the_code_all_say_how_to_get_it()
    {
        // Option 3 (Atlas, no Docker) used to jump straight to running the doctor, which only
        // works from a clone. Every option has to either clone or open a Codespace.
        var setup = RepoFiles.Read("docs/setup.md");
        var options = Regex.Split(setup, @"^## Option ", RegexOptions.Multiline).Skip(1).ToList();

        Assert.Equal(3, options.Count);
        Assert.All(options, o => Assert.True(
            o.Contains("git clone") || o.Contains("Codespace"),
            $"This setup option never says how to get the code:\n{o[..Math.Min(80, o.Length)]}…"));
    }

    [Fact]
    public void Each_parts_card_time_matches_the_time_in_its_own_readme()
    {
        // The card on the challenges page and the README's "Time:" line are two copies of one estimate. Keep them one.
        var settings = System.Text.Json.JsonDocument.Parse(RepoFiles.Read("LandingPage/wwwroot/appsettings.json")).RootElement;

        foreach (var part in settings.GetProperty("Parts").EnumerateArray())
        {
            var folder = part.GetProperty("Folder").GetString()!;
            if (!RepoFiles.Exists($"{folder}/README.md")) continue;

            var time = part.GetProperty("Time").GetString()!;
            var line = RepoFiles.Read($"{folder}/README.md").Split('\n').First(l => l.Contains("**Time:**"));

            Assert.True(line.Contains(time, StringComparison.OrdinalIgnoreCase),
                $"Part {part.GetProperty("Id").GetString()}'s card says \"{time}\", but its README says: {line.Trim()}");
        }
    }

    [Fact]
    public void Every_written_part_says_where_to_get_the_code_and_which_file_to_edit()
    {
        foreach (var part in RepoFiles.WrittenParts())
        {
            var readme = RepoFiles.Read($"{part.Folder}/README.md");

            Assert.True(readme.Contains(RepoUrl), $"Part {part.Id}'s README never links to the repository ({RepoUrl}).");
            Assert.True(readme.Contains($"git clone {RepoUrl}.git"), $"Part {part.Id}'s README has no clone command.");
            Assert.True(readme.Contains("Codespaces"), $"Part {part.Id}'s README doesn't mention Codespaces.");
            Assert.True(readme.Contains($"{part.Folder}/Starter/"), $"Part {part.Id}'s README never says where the starter file is ({part.Folder}/Starter/…).");
        }
    }
}

internal static class StringExtensions
{
    public static string RemoveSuffix(this string value, string suffix) =>
        value.EndsWith(suffix, StringComparison.Ordinal) ? value[..^suffix.Length] : value;
}
