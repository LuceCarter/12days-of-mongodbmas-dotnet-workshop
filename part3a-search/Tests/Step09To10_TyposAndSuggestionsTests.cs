using MongoDB.Driver;
using MongoDB.Driver.Search;
using SantasWorkshop.Shared.Models;

namespace Part3A.Tests;

/// <summary>Steps 9–10: fuzzy matching and autocomplete. Badge objective 5.</summary>
public sealed class Step09To10_TyposAndSuggestionsTests(SearchFixture fixture) : SearchTestBase(fixture)
{
    // ---- Step 9 ----------------------------------------------------------------------------

    [Fact(DisplayName = "Step 9 (obj 5) — FuzzySearchAsync finds the dinosaurs even though 'dinasaur' is misspelt")]
    public async Task Step09_ForgivesATypo()
    {
        // Proof the typo matters: the very same words, with fuzzy matching off, find nothing.
        var plain = await Toys.Aggregate()
            .Search(Builders<Toy>.Search.Text(new[] { "name", "description", "tags" }, "dinasaur"), indexName: SearchFixture.ReferenceStatic)
            .ToListAsync();
        Assert.Empty(plain);

        var found = await Search.FuzzySearchAsync("dinasaur");

        var names = found.Select(h => h.Name).ToList();
        Assert.Contains("Dinosaur Dig Kit", names);
        Assert.Contains("Brachiosaurus Balance Game", names);
        AssertBestFirst(found);
        Assert.Equal("Dinosaur Dig Kit", names[0]);
    }

    [Fact(DisplayName = "Step 9 (obj 5) — a misspelt name still finds the toy")]
    public async Task Step09_FindsAMisspeltName()
    {
        var found = await Search.FuzzySearchAsync("tyranosaurus");

        Assert.Contains("Tyrannosaurus Floor Puzzle", found.Select(h => h.Name));
    }

    [Fact(DisplayName = "Step 9 (obj 5) — correct spelling still works, and nonsense still finds nothing")]
    public async Task Step09_StillFindsRealWordsAndStillRejectsNonsense()
    {
        Assert.Contains("Dinosaur Dig Kit", (await Search.FuzzySearchAsync("dinosaur")).Select(h => h.Name));
        Assert.Empty(await Search.FuzzySearchAsync("qqqqqqqq"));
    }

    // ---- Step 10 ---------------------------------------------------------------------------

    private static List<string> NamesWithAWordStartingWith(string prefix) =>
        Catalogue.Where(t => t.Name.Split([' ', ',', ':'], StringSplitOptions.RemoveEmptyEntries)
                                   .Any(w => w.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                 .Select(t => t.Name).ToList();

    [Fact(DisplayName = "Step 10 (obj 5) — SuggestAsync completes a half-typed word")]
    public async Task Step10_CompletesAHalfTypedWord()
    {
        Assert.Equal(["Dinosaur Dig Kit"], await Search.SuggestAsync("dino"));
        Assert.Equal(["Rocket Launch Playset"], await Search.SuggestAsync("rocke"));
    }

    [Fact(DisplayName = "Step 10 (obj 5) — it completes any word in the name, not just the first")]
    public async Task Step10_CompletesAnyWordInTheName()
    {
        var found = await Search.SuggestAsync("wo");

        Assert.Equal(4, NamesWithAWordStartingWith("wo").Count);
        Assert.Equal(NamesWithAWordStartingWith("wo").OrderBy(n => n), found.OrderBy(n => n));
        Assert.Contains("Jigsaw: Santa's Workshop", found);   // 'Workshop' is the LAST word
    }

    [Fact(DisplayName = "Step 10 (obj 5) — SuggestAsync honours the limit, and ignores letter case")]
    public async Task Step10_HonoursTheLimitAndIgnoresCase()
    {
        Assert.Equal(2, (await Search.SuggestAsync("wo", limit: 2)).Count);
        Assert.Equal(["Rocket Launch Playset"], await Search.SuggestAsync("ROCKE"));
        Assert.Empty(await Search.SuggestAsync("zzz"));
    }
}
