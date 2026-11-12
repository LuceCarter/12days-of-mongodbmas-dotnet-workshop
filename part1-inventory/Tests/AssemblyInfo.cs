namespace Part1.Tests;

/// <summary>
/// Puts every Part 1 test class into one xUnit collection.
/// </summary>
/// <remarks>
/// They all work against the same `toys` collection and reseed it before each test,
/// so letting classes run in parallel would have them treading on each other.
/// Tests inside a single collection never run in parallel.
/// </remarks>
[CollectionDefinition(WorkshopCollection.Name, DisableParallelization = true)]
public sealed class WorkshopCollection
{
    public const string Name = "Santa's Workshop (shared database)";
}
