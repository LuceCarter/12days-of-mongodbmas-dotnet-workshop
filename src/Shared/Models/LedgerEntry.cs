using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SantasWorkshop.Shared.Models;

/// <summary>
/// One line in Santa's Naughty/Nice Ledger: a single deed, and what it was worth.
/// </summary>
/// <remarks>
/// A child's verdict isn't stored anywhere. It is <em>worked out</em> by adding up their
/// deeds — which is exactly the job of <c>$group</c>.
/// </remarks>
public sealed class LedgerEntry
{
    [BsonId]
    [BsonIgnoreIfDefault]
    public ObjectId Id { get; set; }

    [BsonElement("child")]
    public required string Child { get; set; }

    /// <summary>"current" or "previous". Same convention as <see cref="Order.Season"/>.</summary>
    [BsonElement("season")]
    public required string Season { get; set; }

    [BsonElement("deed")]
    public required string Deed { get; set; }

    /// <summary>Positive for a kindness, negative for mischief.</summary>
    [BsonElement("points")]
    public required int Points { get; set; }
}
