using System.Text.Json;
using Microsoft.Extensions.AI;
using Part4B.Starter;
using SantasWorkshop.Shared.Models;

namespace Part4B.Tests;

/// <summary>Steps 2–4: the agent's tools. Badge objectives: tools; LLM function calling. Needs MongoDB, and no key.</summary>
public sealed class Step02To04_ToolsTests(AgentFixture fixture) : AgentTestBase(fixture)
{
    // ---- Step 2: find toys ----------------------------------------------------------------

    private static bool Available(Toy t) => t.InStock && !t.Discontinued;

    [Fact(DisplayName = "Step 2 (tools) — with no arguments it returns the first five available toys, by name")]
    public async Task Step02_NoArguments()
    {
        var found = await Tools.FindToysAsync();

        Assert.Equal(Toys.Where(Available).OrderBy(t => t.Name, StringComparer.Ordinal).Take(5).Select(t => t.Name), found.Select(t => t.Name));
    }

    [Fact(DisplayName = "Step 2 (tools) — a category, whatever its capitals")]
    public async Task Step02_ByCategory()
    {
        var found = await Tools.FindToysAsync(category: "PUZZLE", limit: 50);

        Assert.NotEmpty(found);
        Assert.All(found, t => Assert.Equal("puzzle", t.Category));
        Assert.Equal(Toys.Count(t => Available(t) && t.Category == "puzzle"), found.Count);
    }

    [Fact(DisplayName = "Step 2 (tools) — an age returns only toys that suit a child of exactly that age")]
    public async Task Step02_ByAge()
    {
        var found = await Tools.FindToysAsync(age: 4, limit: 50);

        Assert.NotEmpty(found);
        Assert.All(found, t => Assert.True(t.MinAge <= 4 && 4 <= t.MaxAge, $"{t.Name} is for ages {t.MinAge} to {t.MaxAge}."));
        Assert.Equal(Toys.Count(t => Available(t) && t.MinAge <= 4 && 4 <= t.MaxAge), found.Count);
    }

    [Fact(DisplayName = "Step 2 (tools) — stock: available only by default, and everything if asked")]
    public async Task Step02_InStockOnly()
    {
        var onlyAvailable = await Tools.FindToysAsync(limit: 100);
        var everything = await Tools.FindToysAsync(inStockOnly: false, limit: 100);

        Assert.All(onlyAvailable, t => Assert.True(t.InStock));
        Assert.Equal(Toys.Count(Available), onlyAvailable.Count);
        Assert.Equal(Toys.Count, everything.Count);
        Assert.Contains(everything, t => !t.InStock);
    }

    [Fact(DisplayName = "Step 2 (tools) — the arguments combine, and the limit and the order are honoured")]
    public async Task Step02_Combined()
    {
        var found = await Tools.FindToysAsync(category: "wooden", age: 3, limit: 2);

        var expected = Toys.Where(t => Available(t) && t.Category == "wooden" && t.MinAge <= 3 && 3 <= t.MaxAge)
            .OrderBy(t => t.Name, StringComparer.Ordinal).Take(2).Select(t => t.Name).ToList();
        Assert.Equal(expected, found.Select(t => t.Name));
        Assert.True(found.Count <= 2);
    }

    [Theory(DisplayName = "Step 2 (tools) — a model's odd limit returns nothing instead of throwing")]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Step02_ASillyLimit(int limit)
    {
        Assert.Empty(await Tools.FindToysAsync(limit: limit));
    }

    [Fact(DisplayName = "Step 2 (tools) — each toy carries the facts the agent needs")]
    public async Task Step02_CarriesTheFacts()
    {
        var horse = Assert.Single(await Tools.FindToysAsync(category: "wooden", age: 3, limit: 100), t => t.Name == "Rocking Horse");

        Assert.Equal(new ToyInfo("Rocking Horse", "wooden", 2, 6, 89.99m, true), horse);
    }

    // ---- Step 3: order status -------------------------------------------------------------

    [Fact(DisplayName = "Step 3 (tools) — an order is found by its number, with who it is for, its status and what is on it")]
    public async Task Step03_FindsAnOrder()
    {
        var order = Orders.Single(o => o.OrderNumber == "ORD-1001");

        var result = await Tools.GetOrderStatusAsync("ORD-1001");

        Assert.True(result.Found);
        Assert.Equal("ORD-1001", result.OrderNumber);
        Assert.Equal(order.Child, result.Child);
        Assert.Equal(order.Status, result.Status);
        Assert.Equal(order.Lines.Select(l => $"{l.Quantity} x {l.Toy}"), result.Items);
    }

    [Theory(DisplayName = "Step 3 (tools) — capitals and stray spaces in what a model sends don't matter")]
    [InlineData("ord-1001")]
    [InlineData("  ORD-1001 ")]
    [InlineData("Ord-1001")]
    public async Task Step03_ForgivesTheNumbersFormat(string typed)
    {
        var result = await Tools.GetOrderStatusAsync(typed);

        Assert.True(result.Found);
        Assert.Equal("ORD-1001", result.OrderNumber);
    }

    [Fact(DisplayName = "Step 3 (tools) — an order that doesn't exist is a RESULT saying so, not an exception")]
    public async Task Step03_NotFoundIsAResult()
    {
        var result = await Tools.GetOrderStatusAsync("ORD-9999");

        Assert.False(result.Found);
        Assert.Equal("ORD-9999", result.OrderNumber);
        Assert.Null(result.Child);
        Assert.Null(result.Status);
        Assert.Empty(result.Items);
    }

    [Theory(DisplayName = "Step 3 (tools) — every status comes through as stored")]
    [InlineData("open")]
    [InlineData("packed")]
    [InlineData("delivered")]
    public async Task Step03_EveryStatus(string status)
    {
        var order = Orders.First(o => o.Status == status);

        var result = await Tools.GetOrderStatusAsync(order.OrderNumber);

        Assert.Equal(status, result.Status);
    }

    // ---- Step 4: offer them as tools ------------------------------------------------------

    private static AIFunction Function(IList<AITool> tools, string name) =>
        Assert.IsAssignableFrom<AIFunction>(Assert.Single(tools, t => t.Name == name));

    [Fact(DisplayName = "Step 4 (function calling) — two tools, named the way the instructions name them")]
    public void Step04_TwoNamedTools()
    {
        var tools = Tools.CreateTools();

        Assert.Equal([ElfAgentInstructions.FindToysTool, ElfAgentInstructions.OrderStatusTool], tools.Select(t => t.Name).Order());
        Assert.All(tools, t => Assert.IsAssignableFrom<AIFunction>(t));
    }

    [Fact(DisplayName = "Step 4 (function calling) — each tool carries the description a model will read to decide when to call it")]
    public void Step04_ToolsAreDescribed()
    {
        var tools = Tools.CreateTools();

        Assert.Contains("catalogue", Function(tools, ElfAgentInstructions.FindToysTool).Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("order", Function(tools, ElfAgentInstructions.OrderStatusTool).Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Step 4 (function calling) — the schema a model is shown names every parameter and describes it")]
    public void Step04_TheSchemaDescribesTheParameters()
    {
        var tools = Tools.CreateTools();

        var toys = Function(tools, ElfAgentInstructions.FindToysTool).JsonSchema.GetProperty("properties");
        foreach (var name in new[] { "category", "age", "inStockOnly", "limit" })
            Assert.False(string.IsNullOrWhiteSpace(toys.GetProperty(name).GetProperty("description").GetString()), $"find_toys: '{name}' has no description.");

        var order = Function(tools, ElfAgentInstructions.OrderStatusTool).JsonSchema;
        Assert.False(string.IsNullOrWhiteSpace(order.GetProperty("properties").GetProperty("orderNumber").GetProperty("description").GetString()));
    }

    [Fact(DisplayName = "Step 4 (function calling) — the order number is required; every toy filter is optional")]
    public void Step04_WhatIsRequired()
    {
        var tools = Tools.CreateTools();

        var orderRequired = Function(tools, ElfAgentInstructions.OrderStatusTool).JsonSchema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(["orderNumber"], orderRequired);

        var toySchema = Function(tools, ElfAgentInstructions.FindToysTool).JsonSchema;
        Assert.True(!toySchema.TryGetProperty("required", out var required) || required.GetArrayLength() == 0,
            "A model should be able to call find_toys with no arguments at all.");
    }

    [Fact(DisplayName = "Step 4 (function calling) — calling a tool the way a model does runs your method")]
    public async Task Step04_AToolRuns()
    {
        var tools = Tools.CreateTools();

        var order = await Function(tools, ElfAgentInstructions.OrderStatusTool)
            .InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["orderNumber"] = "ord-1001" }));
        var toys = await Function(tools, ElfAgentInstructions.FindToysTool)
            .InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["age"] = 1, ["limit"] = 3 }));

        Assert.Contains(Orders.Single(o => o.OrderNumber == "ORD-1001").Child, JsonSerializer.Serialize(order));
        var expected = Math.Min(3, Toys.Count(t => Available(t) && t.MinAge <= 1 && 1 <= t.MaxAge));
        Assert.True(expected > 0, "The catalogue should have a toy for a one-year-old.");
        Assert.Equal(expected, JsonDocument.Parse(JsonSerializer.Serialize(toys)).RootElement.GetArrayLength());
    }
}
