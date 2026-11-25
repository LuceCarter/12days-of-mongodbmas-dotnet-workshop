using System.Globalization;
using SantasWorkshop.Shared.Models;

namespace Part4A.Starter;

/// <summary>
/// Step 2: the same assistant can also draw on <b>structured</b> data. A toy is a document with fields,
/// and an embedding model reads <i>text</i>, so a record has to become a sentence before it can be embedded.
/// </summary>
public static class ToyFacts
{
    /// <summary>
    /// Step 2 (badge objective: suitable structured and unstructured data sources) — turn a toy into a short piece
    /// of text a model can embed and a language model can quote.
    /// </summary>
    /// <remarks>
    /// For <c>Rocking Horse</c> (wooden, ages 2 to 6, £89.99, in stock) something like
    /// <c>Rocking Horse (wooden). For ages 2 to 6. £89.99. In stock. A hand-carved wooden rocking horse...</c>
    /// The wording is yours; the tests look for the facts, not the sentence.
    /// </remarks>
public static string ToyToText(Toy toy)
    {
        // TODO (Step 2): include, in words, the toy's name, category, age range, price, whether it is in stock
        // (and say so plainly when it isn't), whether it is discontinued, and its description.
        //
        // Format the price with CultureInfo.InvariantCulture, so "89.99" doesn't become "89,99" on a machine set to another country.
        //
        // Why bother with the stock status? A language model that is only told a toy exists will happily
        // recommend it. The facts that change what a sensible answer is have to be IN the text.
        throw new NotImplementedException("Step 2: turn a structured record into text.");
    }
}
