namespace Part4B.Starter;

/// <summary>
/// Step 1: an agent starts with a plan, and most of the plan is words. This is the text that tells a model what it is,
/// what it can do, and how to behave.
/// </summary>
public static class ElfAgentInstructions
{
    /// <summary>The tool that looks toys up in the catalogue. Step 4 gives it this name.</summary>
    public const string FindToysTool = "find_toys";

    /// <summary>The tool that looks an order up. Step 4 gives it this name.</summary>
    public const string OrderStatusTool = "get_order_status";

    /// <summary>The tool that searches the Elf Handbook. Step 6 gives it this name.</summary>
    public const string HandbookTool = "search_elf_handbook";

    /// <summary>
    /// Step 1 (badge objectives: an agent's reasoning, tools and memory; planning its tasks and data) — write the agent's
    /// instructions, naming each of <paramref name="toolNames"/>.
    /// </summary>
    public static string Build(IEnumerable<string> toolNames)
    {
        // TODO (Step 1): return the instructions as one string. A good set of agent instructions says, in plain words:
        //
        //   WHO it is and who it helps        (the Elf Assistant at Santa's Workshop, helping elves)
        //   WHAT it can do, tool by tool      name EACH tool in toolNames, and say when to use it:
        //                                     toys and stock -> find_toys, an order -> get_order_status,
        //                                     rules and procedures -> search_elf_handbook
        //   WHEN to ask instead of guess      if the elf hasn't given an order number, ask for it: never invent one
        //   WHAT never to do                  don't make up stock levels, order details or handbook rules; if a tool finds
        //                                     nothing, say so
        //   HOW to remember                   it has the earlier messages of this conversation, so it shouldn't ask again
        //                                     for something it has already been told
        //
        // The tests check that every tool is named, and that the rules above are there. Make the words yours.
        throw new NotImplementedException("Step 1: write the agent's instructions.");
    }
}
