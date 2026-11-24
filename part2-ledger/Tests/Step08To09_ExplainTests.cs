using MongoDB.Bson;
using MongoDB.Driver;

namespace Part2.Tests;

/// <summary>Steps 8–9: reading an <c>explain()</c> plan, then changing it with an index.
/// Badge objective 5 (the Compass half is on the README's checklist — a test can't click).</summary>
[Collection(LedgerCollection.Name)]
public sealed class Step08To09_ExplainTests : LedgerTestBase
{
    [Fact(DisplayName = "Step 8 (obj 5) — ExplainAsync returns the plan, and with no index it scans every order")]
    public async Task Step08_Explain_ShowsACollectionScan()
    {
        var explain = await Reports.ExplainAsync(ToysToMakeStages);

        Assert.True(Plan.HasStage(explain, "COLLSCAN"),
            "Expected a COLLSCAN: with no index, the server reads every document in the collection.");
        Assert.False(Plan.HasStage(explain, "IXSCAN"));
    }

    [Fact(DisplayName = "Step 8 (obj 5) — the plan reports how many documents were examined and returned")]
    public async Task Step08_Explain_ReportsDocumentsExaminedAndReturned()
    {
        var explain = await Reports.ExplainAsync(ToysToMakeStages);

        // These are the numbers Compass shows in its Query Performance Summary.
        var examined = Plan.Numbers(explain, "totalDocsExamined").First();
        var returned = Plan.Numbers(explain, "nReturned").First();

        Assert.Equal(AllOrders.Count, examined);                // every order was read...
        Assert.Equal(OpenThisSeason.Count(), returned);         // ...to find the ones we wanted
    }

    [Fact(DisplayName = "Step 9 (obj 5) — CreateSeasonStatusIndexAsync creates an index on season, then status")]
    public async Task Step09_Index_IsCreatedOnSeasonThenStatus()
    {
        var name = await Reports.CreateSeasonStatusIndexAsync();

        var indexes = await (await Orders.Indexes.ListAsync()).ToListAsync();
        var created = indexes.Single(i => i["name"].AsString == name);

        Assert.Equal(new[] { "season", "status" }, created["key"].AsBsonDocument.Names.ToArray());
    }

    [Fact(DisplayName = "Step 9 (obj 5) — with the index, the server uses it and reads far fewer documents")]
    public async Task Step09_Index_MakesTheServerUseIt()
    {
        await Reports.CreateSeasonStatusIndexAsync();

        // Uses the tests' own explain, so this doesn't depend on Step 8.
        var explain = await ExplainDirectlyAsync(ToysToMakeStages);

        Assert.True(Plan.HasStage(explain, "IXSCAN"), "Expected an IXSCAN now that there's an index on season and status.");
        Assert.False(Plan.HasStage(explain, "COLLSCAN"));

        var examined = Plan.Numbers(explain, "totalDocsExamined").First();
        Assert.Equal(OpenThisSeason.Count(), examined);         // only the matching orders were read
        Assert.True(examined < AllOrders.Count);
    }
}
