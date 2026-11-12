using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SantasWorkshop.Shared.Models;

/// <summary>
/// One item in Santa's workshop catalogue.
/// </summary>
/// <remarks>
/// This single model carries the whole series. Part 1 queries it, Part 2 joins
/// orders to it, Part 3A searches its text, and Part 3B embeds
/// <see cref="Description"/> into <see cref="DescriptionEmbedding"/>.
/// </remarks>
public sealed class Toy
{
    [BsonId]
    [BsonIgnoreIfDefault]
    public ObjectId Id { get; set; }

    [BsonElement("name")]
    public required string Name { get; set; }

    /// <summary>Price in pounds. Stored as a BSON decimal, not a double —
    /// money and binary floating point do not mix.</summary>
    [BsonElement("price")]
    [BsonRepresentation(BsonType.Decimal128)]
    public required decimal Price { get; set; }

    /// <summary>Youngest age this toy is suitable for.</summary>
    [BsonElement("minAge")]
    public required int MinAge { get; set; }

    /// <summary>Oldest age this toy is aimed at.</summary>
    [BsonElement("maxAge")]
    public required int MaxAge { get; set; }

    /// <summary>One of: puzzle, board game, soft toy, wooden, electronic, craft, outdoor.</summary>
    [BsonElement("category")]
    public required string Category { get; set; }

    [BsonElement("inStock")]
    public bool InStock { get; set; }

    /// <summary>How many are on the shelf. Drives the "low stock" count in Part 1
    /// and the $inc bonus exercise.</summary>
    [BsonElement("stockCount")]
    public int StockCount { get; set; }

    /// <summary>Elves have stopped making this one.</summary>
    [BsonElement("discontinued")]
    public bool Discontinued { get; set; }

    /// <summary>Free-form labels such as "dinosaur", "space", "classic".
    /// Queried with a plain equality match in Part 1 — MongoDB matches a scalar
    /// against any element of an array.</summary>
    [BsonElement("tags")]
    public List<string> Tags { get; set; } = [];

    /// <summary>A sentence or two of sales copy. Part 3A searches this text and
    /// Part 3B turns it into a vector.</summary>
    [BsonElement("description")]
    public required string Description { get; set; }

    /// <summary>Embedding of <see cref="Description"/>. Null until Part 3B.
    /// Ignored when null so Parts 1 and 2 never see this field at all.</summary>
    [BsonElement("descriptionEmbedding")]
    [BsonIgnoreIfNull]
    public float[]? DescriptionEmbedding { get; set; }
}
