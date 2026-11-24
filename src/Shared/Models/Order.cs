using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SantasWorkshop.Shared.Models;

/// <summary>
/// One slip in the elves' order book: a child, where they live, and what they asked for.
/// </summary>
/// <remarks>
/// Part 2 aggregates over these. Two things about the shape are deliberate:
/// <list type="bullet">
///   <item><see cref="Lines"/> is an <b>array of sub-documents</b>, which is what makes
///         <c>$unwind</c> worth learning: one order becomes one row per line.</item>
///   <item>A line holds the toy's <b>name and a quantity — no price</b>. The price lives in
///         the catalogue and changes (Part 1, Step 3 changed one), so an order must not
///         carry a stale copy. <c>$lookup</c> brings the current price in when you need it.</item>
/// </list>
/// </remarks>
public sealed class Order
{
    [BsonId]
    [BsonIgnoreIfDefault]
    public ObjectId Id { get; set; }

    /// <summary>"ORD-1001". Unique per order slip.</summary>
    [BsonElement("orderNumber")]
    public required string OrderNumber { get; set; }

    [BsonElement("child")]
    public required string Child { get; set; }

    /// <summary>One of: Europe, North America, South America, Asia, Africa, Oceania.</summary>
    [BsonElement("region")]
    public required string Region { get; set; }

    /// <summary>"current" or "previous". Deliberately not a date, so the exercises read
    /// the same whenever you do them.</summary>
    [BsonElement("season")]
    public required string Season { get; set; }

    /// <summary>"open" (still to be made), "packed" or "delivered".</summary>
    [BsonElement("status")]
    public required string Status { get; set; }

    [BsonElement("lines")]
    public List<OrderLine> Lines { get; set; } = [];
}

/// <summary>One toy and how many of it, inside an <see cref="Order"/>.</summary>
public sealed class OrderLine
{
    /// <summary>The toy's <c>name</c> in the catalogue. Not every name is in it: some
    /// children ask for things the workshop doesn't make.</summary>
    [BsonElement("toy")]
    public required string Toy { get; set; }

    [BsonElement("quantity")]
    public required int Quantity { get; set; }
}
