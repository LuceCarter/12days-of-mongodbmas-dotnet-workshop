using System.ComponentModel;
using Microsoft.Extensions.AI;
using MongoDB.Driver;
using SantasWorkshop.Shared.Models;

namespace Part4B.Starter;

/// <summary>A toy, as the agent sees it: the facts it needs, and nothing it doesn't.</summary>
public sealed record ToyInfo(string Name, string Category, int MinAge, int MaxAge, decimal Price, bool InStock);

/// <summary>What the order tool tells the agent. <see cref="Found"/> is false when there is no such order.</summary>
public sealed record OrderStatusResult(bool Found, string OrderNumber, string? Child, string? Status, List<string> Items);

/// <summary>
/// Steps 2 to 4: the agent's <b>tools</b>. A tool is just a method, and what makes it a tool is that a model can ask for it
/// by name, with arguments it chooses, and get the result back: <b>function calling</b>.
/// </summary>
/// <remarks>
/// The <c>[Description]</c> attributes below are not decoration. They are sent to the model as part of the tool's definition,
/// and they are the <i>only</i> thing it has to go on when it decides whether, and how, to call. Vague descriptions make a
/// model call the wrong tool, or the right one with the wrong arguments.
/// </remarks>
public sealed class ElfTools(IMongoCollection<Toy> toys, IMongoCollection<Order> orders)
{
    private readonly IMongoCollection<Toy> _toys = toys;
    private readonly IMongoCollection<Order> _orders = orders;

    /// <summary>
    /// Step 2 (badge objective: tools) — find toys in the catalogue.
    /// </summary>
    [Description("Finds toys in the workshop catalogue. Use it for any question about which toys exist, what suits a child of a given age, or what is in stock. Returns at most `limit` toys, sorted by name.")]
    public async Task<List<ToyInfo>> FindToysAsync(
        [Description("Only toys of this category, such as 'puzzle', 'board game', 'soft toy', 'wooden', 'electronic', 'craft' or 'outdoor'. Leave out for any category.")] string? category = null,
        [Description("The child's age in years. Only toys that suit a child of exactly this age are returned. Leave out to ignore age.")] int? age = null,
        [Description("True (the default) returns only toys that are in stock and not discontinued. False includes the rest.")] bool inStockOnly = true,
        [Description("The most toys to return.")] int limit = 5)
    {
        // TODO (Step 2): build a filter from whichever arguments were given (Part 1 skills):
        //   category      -> Eq on Category (compare ignoring case: models often write "Puzzle" or "puzzle")
        //   age           -> MinAge <= age AND MaxAge >= age
        //   inStockOnly   -> InStock is true AND Discontinued is false
        //   (given nothing, no filter)
        // Sort by Name, take `limit`, and return each as a ToyInfo.
        //
        // A tool is called by a model that may send odd arguments, so be defensive: a limit under 1 should return nothing, not throw.
        throw new NotImplementedException("Step 2: find toys.");
    }

    /// <summary>
    /// Step 3 (badge objective: tools) — look an order up by its number.
    /// </summary>
    [Description("Looks up an order slip by its order number, such as 'ORD-1001', and says who it is for, its status ('open' means still to be made, 'packed', or 'delivered'), and what is on it. Use it when an elf asks about an order. If you have not been given an order number, ask for one: never guess.")]
    public async Task<OrderStatusResult> GetOrderStatusAsync(
        [Description("The order number, such as 'ORD-1001'. Capital letters don't matter.")] string orderNumber)
    {
        // TODO (Step 3): find the order whose OrderNumber matches, ignoring case and any spaces around it ("ord-1001 " is ORD-1001).
        //   Found    -> new OrderStatusResult(true, order.OrderNumber, order.Child, order.Status, items)
        //               where each item reads like "2 x Rocking Horse"
        //   Not found -> new OrderStatusResult(false, orderNumber, null, null, [])
        //
        // Returning "not found" as a RESULT, rather than throwing, matters: the model sees it and can say so, or ask again.
        // An exception inside a tool is something the model can't read.
        throw new NotImplementedException("Step 3: look up an order.");
    }

    /// <summary>
    /// Step 4 (badge objective: LLM function calling) — turn the two methods above into tools a model can be offered.
    /// </summary>
    public IList<AITool> CreateTools()
    {
        // TODO (Step 4): return a list of two AIFunctions, made with AIFunctionFactory.Create:
        //   the method FindToysAsync      named ElfAgentInstructions.FindToysTool
        //   the method GetOrderStatusAsync named ElfAgentInstructions.OrderStatusTool
        //
        // Use the overload that takes a MethodInfo and a target:
        //   AIFunctionFactory.Create(typeof(ElfTools).GetMethod(nameof(FindToysAsync))!, this, new AIFunctionFactoryOptions { Name = ... })
        //
        // The factory reads the method's parameters and their [Description]s and builds the JSON schema a model is shown.
        // The description of each TOOL comes from the [Description] on its method: you don't repeat it.
        throw new NotImplementedException("Step 4: offer the methods to a model as tools.");
    }
}
