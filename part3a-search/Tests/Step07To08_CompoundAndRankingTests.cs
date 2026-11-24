using MongoDB.Driver;
using MongoDB.Driver.Search;
using SantasWorkshop.Shared.Models;

namespace Part3A.Tests;

/// <summary>Steps 7–8: <c>compound</c> queries, and ranking with a boost. Badge objectives 5 and 2.</summary>
public sealed class Step07To08_CompoundAndRankingTests(SearchFixture fixture) : SearchTestBase(fixture)
{
    // ---- Step 7 ----------------------------------------------------------------------------

    private static List<string> Suitable(string word, int age) =>
        Catalogue.Where(t => t.InStock && !t.Discontinued && t.MinAge <= age && age <= t.MaxAge)
                 .Select(t => t.Name).Intersect(ToysMentioning(word)).ToList();

    [Fact(DisplayName = "Step 7 (obj 5) — GiftIdeasAsync finds space toys that are in stock and right for a six-year-old")]
    public async Task Step07_SpaceToysForASixYearOld()
    {
        var found = await Search.GiftIdeasAsync("space", 6);

        Assert.Equal(4, Suitable("space", 6).Count);
        AssertSameToys(Suitable("space", 6), found);
        AssertBestFirst(found);
    }

    [Fact(DisplayName = "Step 7 (obj 5) — a toy can be in stock and still be discontinued, and that counts it out")]
    public async Task Step07_LeavesOutDiscontinuedToys()
    {
        // The Magnetic Fishing Game is in stock, suits a five-year-old, says 'classic', and is being retired.
        var fishing = Catalogue.Single(t => t.Name == "Magnetic Fishing Game");
        Assert.True(fishing.InStock && fishing.Discontinued && fishing.MinAge <= 5 && 5 <= fishing.MaxAge);

        var found = await Search.GiftIdeasAsync("classic", 5);

        Assert.DoesNotContain("Magnetic Fishing Game", found.Select(h => h.Name));
        AssertSameToys(Suitable("classic", 5), found);
    }

    [Fact(DisplayName = "Step 7 (obj 5) — out-of-stock toys, and toys the wrong age, are filtered out")]
    public async Task Step07_FiltersStockAndAge()
    {
        var found = await Search.GiftIdeasAsync("classic", 7);
        var byName = Catalogue.ToDictionary(t => t.Name);

        Assert.NotEmpty(found);
        Assert.All(found, h =>
        {
            var toy = byName[h.Name];
            Assert.True(toy.InStock, $"{h.Name} is out of stock.");
            Assert.True(toy.MinAge <= 7 && 7 <= toy.MaxAge, $"{h.Name} is for ages {toy.MinAge}–{toy.MaxAge}, not 7.");
        });
        Assert.DoesNotContain("Clockwork Tin Robot", found.Select(h => h.Name));   // classic, right age, out of stock
    }

    // ---- Step 8 ----------------------------------------------------------------------------

    [Fact(DisplayName = "Step 8 (obj 2) — RankedAsync puts a toy called 'wooden' ahead of toys that are only described as wooden")]
    public async Task Step08_ANameMatchOutranksADescriptionMatch()
    {
        var found = await Search.RankedAsync("wooden");

        AssertBestFirst(found);
        Assert.Equal("Wooden Tool Bench", found[0].Name);

        // Without the boost this rope comes fifth: its name has the word, but it's only said once and it
        // is a very common word in this catalogue. With a name boost it jumps to second.
        var rank = found.FindIndex(h => h.Name == "Skipping Rope, Wooden Handles");
        Assert.True(rank is >= 0 and <= 1,
            $"'Skipping Rope, Wooden Handles' came {rank + 1}th. A match in the NAME should count five times as much.");
    }

    [Fact(DisplayName = "Step 8 (obj 2) — boosting changes the order, not who is found")]
    public async Task Step08_FindsTheSameToysAsAPlainSearch()
    {
        var plain = await Toys.Aggregate()
            .Search(Builders<Toy>.Search.Text(new[] { "name", "description", "tags" }, "wooden"), indexName: SearchFixture.ReferenceStatic)
            .ToListAsync();

        var ranked = await Search.RankedAsync("wooden");

        AssertSameToys(plain.Select(t => t.Name), ranked,
            "RankedAsync should find exactly the toys a plain text search does, and only reorder them.");
    }

    [Fact(DisplayName = "Step 8 (obj 2) — a clear winner scores far above the rest, and ties are scored equally")]
    public async Task Step08_ScoresShowHowGoodTheMatchIs()
    {
        var space = await Search.RankedAsync("space");
        Assert.Equal("Space Explorer Board Game", space[0].Name);
        Assert.True(space[0].Score > 3 * space[1].Score,
            "Space Explorer has the word in its name AND its tags; every other toy has it once.");

        // Six toys have 'family' once, in a tag. Same word, same place, same score: a tie.
        var family = await Search.RankedAsync("family");
        Assert.True(family.Count >= 6);
        Assert.Single(family.Select(h => Math.Round(h.Score, 4)).Distinct());
    }
}
