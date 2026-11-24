using Part3B.Starter;

namespace Part3B.Tests;

/// <summary>Step 8, the payoff: questions in plain words find the right toy, though they share none of its words.
/// Badge objectives 2 and 5.</summary>
/// <remarks>
/// These ask the real shipped vectors, so they test <i>meaning</i>, and they ask for the best answer to be
/// first (or among the first three) rather than for exact scores: a different embedding model would change
/// the numbers, not the idea.
/// </remarks>
public sealed class Step08_MeaningTests(VectorFixture fixture) : VectorTestBase(fixture)
{
    private GiftFinder FinderAnswering(string question)
    {
        var voyage = new FakeVoyage(_ => FakeVoyage.Ok(VectorForQuestion(question)));
        return new GiftFinder(Toys, new VoyageEmbedder(new HttpClient(voyage), "test-key"), VectorFixture.ReferenceIndex);
    }

    [Theory(DisplayName = "Step 8 (obj 2, 5) — a question in plain words finds the right toy first")]
    [InlineData("something calming for a restless child at bedtime", "Triceratops Night Light")]
    [InlineData("a gift for a budding palaeontologist", "Dinosaur Dig Kit")]
    [InlineData("a rainy-day game for the whole family", "Snakes and Ladders")]
    [InlineData("a toy that makes a little mess and lots of art", "Finger Painting Set")]
    [InlineData("look at the night sky", "Constellation Projector")]
    [InlineData("a toy for a very small baby", "Stegosaurus Soft Toy")]
    [InlineData("something to build with my hands", "Wooden Tool Bench")]
    public async Task Step08_TheRightToyComesFirst(string question, string expectedFirst)
    {
        var found = await FinderAnswering(question).FindGiftsAsync(question, age: null, count: 3);

        Assert.True(found[0].Name == expectedFirst,
            $"For \"{question}\" the best match was {found[0].Name}, not {expectedFirst}. " +
            $"The top three were: {string.Join(", ", found.Select(h => h.Name))}.");
    }

    [Fact(DisplayName = "Step 8 (obj 2, 5) — the bedtime question shares no word with the toy it finds. That's the point")]
    public async Task Step08_FindsByMeaningNotByWords()
    {
        const string question = "something calming for a restless child at bedtime";

        // Part 3A's word search has nothing to grab: none of these words is anywhere in the catalogue's
        // names or descriptions except 'bedtime', and that only because someone typed it into a tag.
        var words = new[] { "calming", "restless" };
        Assert.All(Catalogue, t =>
        {
            var text = $"{t.Name} {t.Description}".ToLowerInvariant();
            Assert.All(words, w => Assert.DoesNotContain(w, text));
        });

        var found = await FinderAnswering(question).FindGiftsAsync(question, age: null, count: 3);

        Assert.Equal("Triceratops Night Light", found[0].Name);
        Assert.DoesNotContain("calming", Catalogue.Single(t => t.Name == found[0].Name).Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Step 8 (obj 2, 5) — the same question for a five-year-old drops the toys that are too old")]
    public async Task Step08_AnAgeChangesTheAnswer()
    {
        const string question = "something calming for a restless child at bedtime";
        var finder = FinderAnswering(question);

        var anyone = await finder.FindGiftsAsync(question, age: null, count: 3);
        var five = await finder.FindGiftsAsync(question, age: 5, count: 3);

        // The projector is for ages 6 to 12: second best for anyone, and not for a five-year-old.
        Assert.Contains("Constellation Projector", anyone.Select(h => h.Name));
        Assert.DoesNotContain("Constellation Projector", five.Select(h => h.Name));
        Assert.Equal("Triceratops Night Light", five[0].Name);
        Assert.Equal(3, five.Count);
    }

    [Fact(DisplayName = "Step 8 (obj 2, 5) — a baby gets baby toys: an age of one leaves only toys that start at nought or one")]
    public async Task Step08_ABabyGetsBabyToys()
    {
        const string question = "a toy for a very small baby";

        var found = await FinderAnswering(question).FindGiftsAsync(question, age: 1, count: 10);

        Assert.Equal("Stegosaurus Soft Toy", found[0].Name);
        Assert.All(found, h => Assert.True(Catalogue.Single(t => t.Name == h.Name).MinAge <= 1));
    }
}
