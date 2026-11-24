using Part3B.Starter;

namespace Part3B.Tests;

/// <summary>Steps 1–3: what a vector is, three ways to compare two, and doing the search by brute force.
/// Badge objectives 1, 4 and 2.</summary>
public sealed class Step01To03_VectorsByHandTests(VectorFixture fixture) : VectorTestBase(fixture)
{
    // ---- Step 1 ----------------------------------------------------------------------------

    [Fact(DisplayName = "Step 1 (obj 1) — DescribeEmbeddingAsync says how many dimensions a toy's vector has")]
    public async Task Step01_ReportsTheNumberOfDimensions()
    {
        var facts = await Finder.DescribeEmbeddingAsync("Rocking Horse");

        Assert.Equal(VectorOf("Rocking Horse").Length, facts.Dimensions);
        Assert.Equal(VectorFixture.Dimensions, facts.Dimensions);
    }

    [Fact(DisplayName = "Step 1 (obj 1) — …and how long the vector is as an arrow (its magnitude)")]
    public async Task Step01_ReportsTheMagnitude()
    {
        var vector = VectorOf("Dinosaur Dig Kit");
        var expected = Math.Sqrt(vector.Sum(x => (double)x * x));

        var facts = await Finder.DescribeEmbeddingAsync("Dinosaur Dig Kit");

        Assert.Equal(expected, facts.Magnitude, precision: 6);
    }

    [Fact(DisplayName = "Step 1 (obj 1) — a toy that doesn't exist is an error, not a vector of zeros")]
    public async Task Step01_UnknownToyIsAnError()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Finder.DescribeEmbeddingAsync("Time Machine"));
    }

    // ---- Step 2 ----------------------------------------------------------------------------

    [Fact(DisplayName = "Step 2 (obj 1, 4) — same direction, different length: cosine says identical, the other two say otherwise")]
    public void Step02_SameDirectionDifferentLength()
    {
        var scores = GiftFinder.Compare([1f, 0f], [2f, 0f]);

        Assert.Equal(1.0, scores.Cosine, precision: 9);
        Assert.Equal(2.0, scores.DotProduct, precision: 9);
        Assert.Equal(1.0, scores.EuclideanDistance, precision: 9);
    }

    [Fact(DisplayName = "Step 2 (obj 1, 4) — at 45°, at right angles and opposite")]
    public void Step02_KnownAngles()
    {
        var fortyFive = GiftFinder.Compare([1f, 0f], [1f, 1f]);
        Assert.Equal(Math.Sqrt(2) / 2, fortyFive.Cosine, precision: 9);
        Assert.Equal(1.0, fortyFive.DotProduct, precision: 9);
        Assert.Equal(1.0, fortyFive.EuclideanDistance, precision: 9);

        var rightAngle = GiftFinder.Compare([1f, 0f], [0f, 1f]);
        Assert.Equal(0.0, rightAngle.Cosine, precision: 9);
        Assert.Equal(0.0, rightAngle.DotProduct, precision: 9);
        Assert.Equal(Math.Sqrt(2), rightAngle.EuclideanDistance, precision: 9);

        var opposite = GiftFinder.Compare([1f, 0f], [-1f, 0f]);
        Assert.Equal(-1.0, opposite.Cosine, precision: 9);
        Assert.Equal(2.0, opposite.EuclideanDistance, precision: 9);
    }

    [Fact(DisplayName = "Step 2 (obj 1, 4) — a real vector is identical to itself, and cosine is the same both ways round")]
    public void Step02_RealVectors()
    {
        var horse = VectorOf("Rocking Horse");
        var puzzle = VectorOf("Solar System Puzzle");

        var itself = GiftFinder.Compare(horse, horse);
        Assert.Equal(1.0, itself.Cosine, precision: 6);
        Assert.Equal(0.0, itself.EuclideanDistance, precision: 6);

        var there = GiftFinder.Compare(horse, puzzle);
        var back = GiftFinder.Compare(puzzle, horse);
        Assert.Equal(there.Cosine, back.Cosine, precision: 9);
        Assert.Equal(Cosine(horse, puzzle), there.Cosine, precision: 9);
        Assert.True(there.Cosine < 1.0);
    }

    [Fact(DisplayName = "Step 2 (obj 1, 4) — vectors of different lengths can't be compared")]
    public void Step02_DifferentLengthsAreRejected()
    {
        var error = Assert.Throws<ArgumentException>(() => GiftFinder.Compare([1f, 0f], [1f, 0f, 0f]));

        Assert.Contains("2", error.Message);
        Assert.Contains("3", error.Message);
    }

    // ---- Step 3 ----------------------------------------------------------------------------

    [Fact(DisplayName = "Step 3 (obj 2) — ClosestByHandAsync finds the closest toys by comparing with every one")]
    public async Task Step03_FindsTheClosestToys()
    {
        var query = VectorOf("Pocket Telescope");

        var found = await Finder.ClosestByHandAsync(query, 5);

        Assert.Equal(Nearest(query, 5).Select(x => x.Name), found.Select(h => h.Name));
        AssertBestFirst(found);
    }

    [Fact(DisplayName = "Step 3 (obj 2) — a toy's own vector finds that toy first, with a cosine of 1")]
    public async Task Step03_AToyIsItsOwnNearestNeighbour()
    {
        var found = await Finder.ClosestByHandAsync(VectorOf("Rocking Horse"), 3);

        Assert.Equal("Rocking Horse", found[0].Name);
        Assert.Equal(1.0, found[0].Score, precision: 6);
    }

    [Fact(DisplayName = "Step 3 (obj 2) — it returns as many as asked for, and no more than there are")]
    public async Task Step03_HonoursTheCount()
    {
        var query = VectorForQuestion("something calming for a restless child at bedtime");

        Assert.Single(await Finder.ClosestByHandAsync(query, 1));
        Assert.Equal(Catalogue.Count, (await Finder.ClosestByHandAsync(query, 1000)).Count);
    }
}
