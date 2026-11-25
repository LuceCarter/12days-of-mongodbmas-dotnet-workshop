using Part4A.Starter;

namespace Part4A.Tests;

/// <summary>Step 3: chunking, and chunk overlap. Badge objective: chunk overlap. No database needed.</summary>
public sealed class Step03_ChunkerTests(HandbookFixture fixture) : HandbookTestBase(fixture)
{
    private const string AToJ = "a b c d e f g h i j";

    [Fact(DisplayName = "Step 3 (chunk overlap) — each chunk starts (size − overlap) words after the last one started")]
    public void Step03_TheWorkedExample()
    {
        var chunks = Chunker.SplitIntoChunks(AToJ, size: 5, overlap: 2);

        Assert.Equal(["a b c d e", "d e f g h", "g h i j"], chunks);
    }

    [Fact(DisplayName = "Step 3 (chunk overlap) — neighbouring chunks share exactly the overlap")]
    public void Step03_NeighboursShareTheOverlap()
    {
        var text = string.Join(' ', Enumerable.Range(1, 60).Select(i => $"w{i}"));

        var chunks = Chunker.SplitIntoChunks(text, size: 10, overlap: 3);

        for (var i = 1; i < chunks.Count; i++)
        {
            var before = chunks[i - 1].Split(' ');
            var after = chunks[i].Split(' ');
            Assert.Equal(before.TakeLast(3), after.Take(3));
        }
    }

    [Fact(DisplayName = "Step 3 (chunk overlap) — with no overlap the chunks simply follow each other")]
    public void Step03_NoOverlap()
    {
        Assert.Equal(["a b c", "d e f", "g h i", "j"], Chunker.SplitIntoChunks(AToJ, size: 3, overlap: 0));
    }

    [Fact(DisplayName = "Step 3 (chunk overlap) — no chunk is longer than the size, and nothing is lost")]
    public void Step03_NothingIsLostAndNothingIsTooLong()
    {
        var text = string.Join(' ', Enumerable.Range(1, 97).Select(i => $"w{i}"));

        var chunks = Chunker.SplitIntoChunks(text, size: 12, overlap: 4);

        Assert.All(chunks, c => Assert.True(c.Split(' ').Length <= 12));
        Assert.Equal("w1", chunks[0].Split(' ')[0]);
        Assert.Equal("w97", chunks[^1].Split(' ')[^1]);

        // Put the chunks back together, dropping each one's overlap: you get the original text.
        var rebuilt = chunks[0].Split(' ').Concat(chunks.Skip(1).SelectMany(c => c.Split(' ').Skip(4)));
        Assert.Equal(text.Split(' '), rebuilt);
    }

    [Theory(DisplayName = "Step 3 (chunk overlap) — the last chunk always brings something new, never only words already seen")]
    [InlineData(5, 1)]    // exactly one chunk
    [InlineData(6, 2)]    // a second chunk with one new word
    [InlineData(8, 2)]    // the second chunk reaches the end, so there is no third
    [InlineData(9, 3)]    // a third is needed for the ninth word
    public void Step03_NoRedundantTail(int words, int expectedChunks)
    {
        var text = string.Join(' ', Enumerable.Range(1, words).Select(i => $"w{i}"));

        var chunks = Chunker.SplitIntoChunks(text, size: 5, overlap: 2);

        Assert.Equal(expectedChunks, chunks.Count);
        Assert.Equal(ExpectedChunks(text, 5, 2), chunks);
    }

    [Fact(DisplayName = "Step 3 (chunk overlap) — a text shorter than one chunk is one chunk")]
    public void Step03_ShortTextIsOneChunk()
    {
        Assert.Equal(["just three words"], Chunker.SplitIntoChunks("just three words", size: 80, overlap: 20));
    }

    [Fact(DisplayName = "Step 3 (chunk overlap) — any whitespace between words is one space in the chunk")]
    public void Step03_TidiesWhitespace()
    {
        var chunks = Chunker.SplitIntoChunks("one  two\tthree\n\nfour\r\nfive", size: 10, overlap: 2);

        Assert.Equal(["one two three four five"], chunks);
    }

    [Theory(DisplayName = "Step 3 (chunk overlap) — empty text has no chunks")]
    [InlineData("")]
    [InlineData("   \n\t ")]
    public void Step03_EmptyTextHasNoChunks(string text)
    {
        Assert.Empty(Chunker.SplitIntoChunks(text, size: 5, overlap: 2));
    }

    [Theory(DisplayName = "Step 3 (chunk overlap) — an overlap that can't move forward is refused rather than looping forever")]
    [InlineData(0, 0)]
    [InlineData(5, 5)]
    [InlineData(5, 9)]
    [InlineData(5, -1)]
    [InlineData(-3, 0)]
    public void Step03_RefusesImpossibleSettings(int size, int overlap)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Chunker.SplitIntoChunks(AToJ, size, overlap));
    }

    [Fact(DisplayName = "Step 3 (chunk overlap) — over the real handbook it makes exactly the chunks that were embedded and shipped")]
    public void Step03_MakesTheShippedChunks()
    {
        var shipped = HandbookFixture.Shipped;

        foreach (var section in ReferenceSections())
        {
            var expected = shipped.Chunks.Where(c => c.Section == section.Section).OrderBy(c => c.ChunkIndex).Select(c => c.Text).ToList();

            var yours = Chunker.SplitIntoChunks(section.Body, shipped.ChunkWords, shipped.OverlapWords);

            Assert.True(expected.SequenceEqual(yours),
                $"'{section.Section}': the shipped vectors were made from {expected.Count} chunks of up to {shipped.ChunkWords} words " +
                $"with {shipped.OverlapWords} words of overlap; your Chunker made {yours.Count}.");
        }
    }
}
