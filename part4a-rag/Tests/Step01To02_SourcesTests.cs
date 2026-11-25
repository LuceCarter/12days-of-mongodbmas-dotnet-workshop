using System.Globalization;
using Part4A.Starter;
using SantasWorkshop.Shared;

namespace Part4A.Tests;

/// <summary>Steps 1–2: collecting and preparing the data, structured and unstructured.
/// Badge objectives: suitable data sources; data collection, preparation and ingestion. No database needed.</summary>
public sealed class Step01To02_SourcesTests(HandbookFixture fixture) : HandbookTestBase(fixture)
{
    // ---- Step 1 ---------------------------------------------------------------------------

    [Fact(DisplayName = "Step 1 (unstructured source) — the handbook splits into its ten sections, in order")]
    public void Step01_FindsEverySection()
    {
        var sections = HandbookLoader.ParseHandbook(SeedLoader.LoadHandbookText());

        Assert.Equal(
            ["Toy Safety", "Quality Control", "Wrapping and Labelling", "Loading the Sleigh", "Reindeer Care",
             "Reading and Answering Letters", "The Naughty and Nice Ledger", "Workshop Rules", "Elf Wellbeing", "Emergencies"],
            sections.Select(s => s.Section));
    }

    [Fact(DisplayName = "Step 1 (unstructured source) — each section knows who it is for")]
    public void Step01_ReadsTheAudience()
    {
        var byName = HandbookLoader.ParseHandbook(SeedLoader.LoadHandbookText()).ToDictionary(s => s.Section, s => s.Audience);

        Assert.Equal("toy-makers", byName["Toy Safety"]);
        Assert.Equal("stable-elves", byName["Reindeer Care"]);
        Assert.Equal("all-elves", byName["Emergencies"]);
    }

    [Fact(DisplayName = "Step 1 (unstructured source) — a section's body is its text, with no heading and no Audience line")]
    public void Step01_TheBodyIsJustTheText()
    {
        var sections = HandbookLoader.ParseHandbook(SeedLoader.LoadHandbookText());
        var safety = sections.Single(s => s.Section == "Toy Safety");

        Assert.StartsWith("Every toy leaves this workshop safe", safety.Body);
        Assert.Contains("brass test tube", safety.Body);
        Assert.All(sections, s =>
        {
            Assert.DoesNotContain("Audience:", s.Body);
            Assert.DoesNotContain("## ", s.Body);
            Assert.Equal(s.Body.Trim(), s.Body);
        });
        Assert.DoesNotContain("Quality Bench", safety.Body);   // the next section's text didn't leak in
    }

    [Fact(DisplayName = "Step 1 (unstructured source) — the paragraphs are kept apart, and the title above the first section is ignored")]
    public void Step01_KeepsParagraphsAndIgnoresThePreface()
    {
        const string markdown = "# A Title\n\nSome note.\n\n## First\nAudience: a-team\n\nOne.\n\nTwo.\n\n## Second\nAudience: b-team\n\nThree.\n";

        var sections = HandbookLoader.ParseHandbook(markdown);

        Assert.Equal(2, sections.Count);
        Assert.Equal(new HandbookSection("First", "a-team", "One.\n\nTwo."), sections[0]);
        Assert.Equal(new HandbookSection("Second", "b-team", "Three."), sections[1]);
    }

    [Fact(DisplayName = "Step 1 (unstructured source) — Windows line endings parse the same")]
    public void Step01_HandlesWindowsLineEndings()
    {
        const string markdown = "# T\r\n\r\n## First\r\nAudience: a-team\r\n\r\nOne.\r\n\r\nTwo.\r\n";

        var section = Assert.Single(HandbookLoader.ParseHandbook(markdown));

        Assert.Equal("First", section.Section);
        Assert.Equal("a-team", section.Audience);
        Assert.DoesNotContain('\r', section.Section + section.Audience);
        Assert.StartsWith("One.", section.Body);
        Assert.EndsWith("Two.", section.Body);
    }

    [Fact(DisplayName = "Step 1 (unstructured source) — a section with no audience is refused, by name")]
    public void Step01_RefusesASectionWithNoAudience()
    {
        var error = Assert.Throws<InvalidOperationException>(() => HandbookLoader.ParseHandbook("## Lonely Section\n\nText with no audience."));

        Assert.Contains("Lonely Section", error.Message);
    }

    // ---- Step 2 ---------------------------------------------------------------------------

    private static string TextOf(string toyName) => ToyFacts.ToyToText(SeedLoader.LoadToys().Single(t => t.Name == toyName));

    [Fact(DisplayName = "Step 2 (structured source) — a toy's text carries its name, kind, ages, price and description")]
    public void Step02_CarriesTheFacts()
    {
        var horse = SeedLoader.LoadToys().Single(t => t.Name == "Rocking Horse");

        var text = ToyFacts.ToyToText(horse);

        Assert.Contains("Rocking Horse", text);
        Assert.Contains("wooden", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2", text);
        Assert.Contains("6", text);
        Assert.Contains(horse.Price.ToString("0.00", CultureInfo.InvariantCulture), text);
        Assert.Contains(horse.Description, text);
    }

    [Fact(DisplayName = "Step 2 (structured source) — it says so when a toy is in stock, and plainly when it isn't")]
    public void Step02_SaysWhetherItIsInStock()
    {
        var inStock = SeedLoader.LoadToys().First(t => t.InStock && !t.Discontinued);
        var outOfStock = SeedLoader.LoadToys().First(t => !t.InStock);

        Assert.Contains("in stock", ToyFacts.ToyToText(inStock), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("out of stock", ToyFacts.ToyToText(inStock), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("out of stock", ToyFacts.ToyToText(outOfStock), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Step 2 (structured source) — a discontinued toy says so, and a current one doesn't")]
    public void Step02_SaysWhetherItIsDiscontinued()
    {
        var discontinued = SeedLoader.LoadToys().First(t => t.Discontinued);
        var current = SeedLoader.LoadToys().First(t => !t.Discontinued);

        Assert.Contains("discontinued", ToyFacts.ToyToText(discontinued), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("discontinued", ToyFacts.ToyToText(current), StringComparison.OrdinalIgnoreCase);
    }
}
