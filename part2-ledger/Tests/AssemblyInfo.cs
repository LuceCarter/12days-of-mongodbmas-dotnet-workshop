namespace Part2.Tests;

/// <summary>
/// Puts every Part 2 test class into one xUnit collection.
/// </summary>
/// <remarks>
/// They all work against the same three collections and reseed them before each test, so
/// letting classes run in parallel would have them treading on each other. Tests inside a
/// single collection never run in parallel.
/// </remarks>
[CollectionDefinition(LedgerCollection.Name, DisableParallelization = true)]
public sealed class LedgerCollection
{
    public const string Name = "Santa's Ledger (shared database)";
}
