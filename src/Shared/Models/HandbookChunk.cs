using MongoDB.Bson.Serialization.Attributes;

namespace SantasWorkshop.Shared.Models;

/// <summary>
/// One piece of the Elf Handbook (Part 4): a few dozen words of a section, with a vector that stands for what it means.
/// </summary>
/// <remarks>
/// The handbook is cut into chunks because a vector can only stand for so much text. A whole section blurs into one
/// average; a chunk is about one thing. The <see cref="Section"/> and <see cref="Audience"/> are what the retriever
/// can filter on <i>before</i> it searches.
/// </remarks>
public sealed class HandbookChunk
{
    /// <summary>The section and the chunk's position in it: <c>toy-safety-0</c>, <c>toy-safety-1</c>, ...</summary>
    [BsonId]
    public required string Id { get; set; }

    /// <summary>The handbook section this came from, such as <c>Toy Safety</c>.</summary>
    [BsonElement("section")]
    public required string Section { get; set; }

    /// <summary>Who the section is written for, such as <c>toy-makers</c> or <c>all-elves</c>.</summary>
    [BsonElement("audience")]
    public required string Audience { get; set; }

    /// <summary>Where a reader can find it again, for citing: <c>The Elf Handbook › Toy Safety</c>.</summary>
    [BsonElement("source")]
    public required string Source { get; set; }

    /// <summary>Which chunk of its section this is, from 0.</summary>
    [BsonElement("chunkIndex")]
    public required int ChunkIndex { get; set; }

    [BsonElement("text")]
    public required string Text { get; set; }

    /// <summary>What <see cref="Text"/> means, as a vector. Absent until the chunk is embedded.</summary>
    [BsonElement("embedding")]
    [BsonIgnoreIfNull]
    public float[]? Embedding { get; set; }
}
