using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using MongoDB.Bson;
using MongoDB.Driver;
using Part4B.Starter;

namespace Part4B.Tests;

/// <summary>Steps 7–9: short-term memory in MongoDB. Badge objectives: short- vs. long-term memory; short-term agent memory with MongoDB.
/// Needs MongoDB, and no key.</summary>
public sealed class Step07To09_MemoryTests(AgentFixture fixture) : AgentTestBase(fixture)
{
    private MongoChatStore NewStore(string name, out IMongoCollection<StoredMessage> collection)
    {
        collection = Fixture.FreshMessages(name);
        return new MongoChatStore(collection);
    }

    private static string Roles(IEnumerable<ChatMessage> messages) => string.Join(",", messages.Select(m => m.Role.Value));

    // ---- Step 7: the store ----------------------------------------------------------------

    [Fact(DisplayName = "Step 7 (short-term memory) — messages come back as they went in, oldest first")]
    public async Task Step07_RoundTrip()
    {
        var store = NewStore("rt", out _);

        await store.AppendAsync("c1", [Say(ChatRole.User, "hello"), Say(ChatRole.Assistant, "hello, elf")]);
        var back = await store.LoadAsync("c1");

        Assert.Equal(["hello", "hello, elf"], back.Select(m => m.Text));
        Assert.Equal("user,assistant", Roles(back));
    }

    [Fact(DisplayName = "Step 7 (short-term memory) — a tool call and its result survive the round trip intact")]
    public async Task Step07_ToolCallsSurvive()
    {
        var store = NewStore("tools", out _);
        var call = Asks(("call-1", "get_order_status", new() { ["orderNumber"] = "ORD-1001" }));
        var result = new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-1", "open")]);

        await store.AppendAsync("c1", [Say(ChatRole.User, "where is ORD-1001?"), call, result, Say(ChatRole.Assistant, "It is open.")]);
        var back = await store.LoadAsync("c1");

        var restoredCall = Assert.Single(back.SelectMany(m => m.Contents).OfType<FunctionCallContent>());
        Assert.Equal("call-1", restoredCall.CallId);
        Assert.Equal("get_order_status", restoredCall.Name);
        Assert.Equal("ORD-1001", restoredCall.Arguments!["orderNumber"]?.ToString());

        var restoredResult = Assert.Single(back.SelectMany(m => m.Contents).OfType<FunctionResultContent>());
        Assert.Equal("call-1", restoredResult.CallId);
        Assert.Equal("user,assistant,tool,assistant", Roles(back));
    }

    [Fact(DisplayName = "Step 7 (short-term memory) — several appends add to the end, in order, numbered without gaps")]
    public async Task Step07_AppendsAccumulate()
    {
        var store = NewStore("append", out var collection);

        await store.AppendAsync("c1", [Say(ChatRole.User, "one")]);
        await store.AppendAsync("c1", [Say(ChatRole.Assistant, "two"), Say(ChatRole.User, "three")]);

        Assert.Equal(["one", "two", "three"], (await store.LoadAsync("c1")).Select(m => m.Text));
        var sequences = (await collection.Find(m => m.ConversationId == "c1").SortBy(m => m.Sequence).ToListAsync()).Select(m => m.Sequence);
        Assert.Equal([0L, 1L, 2L], sequences);
    }

    [Fact(DisplayName = "Step 7 (short-term memory) — each conversation keeps to itself")]
    public async Task Step07_ConversationsAreSeparate()
    {
        var store = NewStore("separate", out _);

        await store.AppendAsync("mine", [Say(ChatRole.User, "my secret")]);
        await store.AppendAsync("yours", [Say(ChatRole.User, "your secret")]);

        Assert.Equal(["my secret"], (await store.LoadAsync("mine")).Select(m => m.Text));
        Assert.Equal(["your secret"], (await store.LoadAsync("yours")).Select(m => m.Text));
    }

    [Fact(DisplayName = "Step 7 (short-term memory) — a conversation nobody has stored is empty, not an error, and storing nothing is fine")]
    public async Task Step07_NothingToRemember()
    {
        var store = NewStore("empty", out var collection);

        await store.AppendAsync("c1", []);

        Assert.Empty(await store.LoadAsync("never-said-anything"));
        Assert.Equal(0, await collection.CountDocumentsAsync(FilterDefinition<StoredMessage>.Empty));
    }

    [Fact(DisplayName = "Step 7 (short-term memory) — each stored document carries its conversation, role and time, to read in Compass")]
    public async Task Step07_TheDocumentIsReadable()
    {
        var store = NewStore("doc", out var collection);
        var before = DateTime.UtcNow.AddSeconds(-5);

        await store.AppendAsync("c1", [Say(ChatRole.User, "hello")]);

        var stored = await collection.Find(FilterDefinition<StoredMessage>.Empty).SingleAsync();
        Assert.Equal("c1", stored.ConversationId);
        Assert.Equal("user", stored.Role);
        Assert.True(stored.CreatedAt > before && stored.CreatedAt < DateTime.UtcNow.AddSeconds(5), "CreatedAt should be now, in UTC.");
        Assert.Contains("hello", stored.Json);
    }

    [Fact(DisplayName = "Step 7 (short-term memory) — a unique index on conversation and sequence, so a message can't be stored twice")]
    public async Task Step07_AUniqueIndexOnTheConversation()
    {
        var store = NewStore("index-unique", out var collection);
        await store.EnsureIndexesAsync(TimeSpan.FromHours(1));
        await store.AppendAsync("c1", [Say(ChatRole.User, "one")]);

        var indexes = await (await collection.Indexes.ListAsync()).ToListAsync();
        var unique = Assert.Single(indexes, i => i.GetValue("unique", false).AsBoolean);
        Assert.Equal(["conversationId", "sequence"], unique["key"].AsBsonDocument.Names);

        // And it is enforced: the same position in the same conversation can't be written again.
        var duplicate = new StoredMessage { ConversationId = "c1", Sequence = 0, Role = "user", Json = "{}", CreatedAt = DateTime.UtcNow };
        await Assert.ThrowsAsync<MongoWriteException>(() => collection.InsertOneAsync(duplicate));
    }

    [Fact(DisplayName = "Step 7 (short-term memory) — a TTL index on createdAt makes MongoDB forget old conversations")]
    public async Task Step07_ATtlIndexForgets()
    {
        var store = NewStore("index-ttl", out var collection);

        await store.EnsureIndexesAsync(TimeSpan.FromHours(2));

        var indexes = await (await collection.Indexes.ListAsync()).ToListAsync();
        var ttl = Assert.Single(indexes, i => i.Contains("expireAfterSeconds"));
        Assert.Equal(["createdAt"], ttl["key"].AsBsonDocument.Names);
        Assert.Equal(7200, ttl["expireAfterSeconds"].ToInt32());
    }

    // ---- Step 8: the window ---------------------------------------------------------------

    private static List<ChatMessage> TwoToolTurns() =>
    [
        Say(ChatRole.User, "u1"),
        Asks(("c1", "find_toys", null)),
        new ChatMessage(ChatRole.Tool, [new FunctionResultContent("c1", "toys")]),
        Say(ChatRole.Assistant, "a1"),
        Say(ChatRole.User, "u2"),
        Asks(("c2", "get_order_status", null)),
        new ChatMessage(ChatRole.Tool, [new FunctionResultContent("c2", "open")]),
        Say(ChatRole.Assistant, "a2"),
    ];

    [Fact(DisplayName = "Step 8 (short-term memory) — if it all fits, all of it is kept")]
    public void Step08_EverythingFits()
    {
        var messages = TwoToolTurns();

        var trimmed = ChatWindow.TrimToRecent(messages, 8);

        Assert.Equal(messages, trimmed);
        Assert.NotSame(messages, trimmed);
    }

    [Fact(DisplayName = "Step 8 (short-term memory) — a cut that lands on a turn boundary keeps exactly that many")]
    public void Step08_ACleanCut()
    {
        var trimmed = ChatWindow.TrimToRecent(TwoToolTurns(), 4);

        Assert.Equal("user,assistant,tool,assistant", Roles(trimmed));
        Assert.Equal("u2", trimmed[0].Text);
    }

    [Theory(DisplayName = "Step 8 (short-term memory) — a cut in the MIDDLE of a turn moves forward to the next user message, so a tool result is never orphaned")]
    [InlineData(7)]   // would start at the tool call
    [InlineData(6)]   // would start at the first tool result
    [InlineData(5)]   // would start at the first answer
    public void Step08_NeverStartsMidTurn(int max)
    {
        var trimmed = ChatWindow.TrimToRecent(TwoToolTurns(), max);

        Assert.Equal(ChatRole.User, trimmed[0].Role);
        Assert.True(trimmed.Count <= max, "It may keep fewer than asked, never more.");
        Assert.DoesNotContain(trimmed.Take(1), m => m.Contents.OfType<FunctionResultContent>().Any());
    }

    [Fact(DisplayName = "Step 8 (short-term memory) — if no whole turn fits, nothing is kept, rather than half of one")]
    public void Step08_NoWholeTurnFits()
    {
        Assert.Empty(ChatWindow.TrimToRecent(TwoToolTurns(), 3));
    }

    [Theory(DisplayName = "Step 8 (short-term memory) — a window of no messages is refused")]
    [InlineData(0)]
    [InlineData(-2)]
    public void Step08_RefusesAnImpossibleWindow(int max)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChatWindow.TrimToRecent(TwoToolTurns(), max));
    }

    // ---- Step 9: the provider -------------------------------------------------------------

    private static ChatClientAgent AgentOver(IChatClient chat, MongoChatHistoryProvider provider, IList<AITool>? tools = null) =>
        chat.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "Memory test agent",
            ChatOptions = new ChatOptions { Instructions = "You are an elf.", Tools = tools },
            ChatHistoryProvider = provider,
        });

    private static ScriptedChat Answers(params string[] replies) =>
        new((_, n) => Say(ChatRole.Assistant, replies[Math.Min(n, replies.Length) - 1]));

    [Fact(DisplayName = "Already written — a session belongs to a conversation, the same one every time you ask, and a new session gets its own")]
    public async Task Step09_ASessionKeepsItsConversation()
    {
        var provider = new MongoChatHistoryProvider(NewStore("provider-id", out _));
        var agent = AgentOver(Answers("x"), provider);

        var one = await agent.CreateSessionAsync();
        var two = await agent.CreateSessionAsync();

        Assert.Equal(MongoChatHistoryProvider.ConversationOf(one), MongoChatHistoryProvider.ConversationOf(one));
        Assert.NotEqual(MongoChatHistoryProvider.ConversationOf(one), MongoChatHistoryProvider.ConversationOf(two));
    }

    [Fact(DisplayName = "Step 9 (short-term agent memory) — RememberAsync stores what was asked, then everything the agent produced")]
    public async Task Step09_RememberStoresTheExchange()
    {
        var store = NewStore("provider-remember", out _);
        var provider = new MongoChatHistoryProvider(store);
        var session = await AgentOver(Answers("x"), provider).CreateSessionAsync();
        MongoChatHistoryProvider.UseConversation(session, "talk");

        await provider.RememberAsync(session, [Say(ChatRole.User, "hello")], [Say(ChatRole.Assistant, "hi")]);

        Assert.Equal(["hello", "hi"], (await store.LoadAsync("talk")).Select(m => m.Text));
    }

    [Fact(DisplayName = "Step 9 (short-term agent memory) — RecallAsync returns this conversation's history, and not another's")]
    public async Task Step09_RecallReturnsThisConversation()
    {
        var store = NewStore("provider-recall", out _);
        var provider = new MongoChatHistoryProvider(store);
        var session = await AgentOver(Answers("x"), provider).CreateSessionAsync();
        MongoChatHistoryProvider.UseConversation(session, "mine");
        await store.AppendAsync("mine", [Say(ChatRole.User, "my question"), Say(ChatRole.Assistant, "my answer")]);
        await store.AppendAsync("other", [Say(ChatRole.User, "someone else's")]);

        var recalled = await provider.RecallAsync(session);

        Assert.Equal(["my question", "my answer"], recalled.Select(m => m.Text));
    }

    [Fact(DisplayName = "Step 9 (short-term agent memory) — RecallAsync trims to the window, starting at a user message")]
    public async Task Step09_RecallTrims()
    {
        var store = NewStore("provider-trim", out _);
        var provider = new MongoChatHistoryProvider(store, maxMessages: 4);
        var session = await AgentOver(Answers("x"), provider).CreateSessionAsync();
        MongoChatHistoryProvider.UseConversation(session, "long");
        await store.AppendAsync("long", TwoToolTurns());

        var recalled = await provider.RecallAsync(session);

        Assert.Equal("u2", recalled[0].Text);
        Assert.Equal(4, recalled.Count);
    }

    [Fact(DisplayName = "Step 9 (short-term agent memory) — an agent remembers across turns: the second turn shows the model the first, tool call and all")]
    public async Task Step09_TheAgentRemembers()
    {
        var store = NewStore("provider-turns", out _);
        var provider = new MongoChatHistoryProvider(store);
        var chat = new ScriptedChat((seen, n) => n switch
        {
            1 => Asks(("c1", ElfAgentInstructions.OrderStatusTool, new() { ["orderNumber"] = "ORD-1001" })),
            2 => Say(ChatRole.Assistant, "ORD-1001 is open."),
            _ => Say(ChatRole.Assistant, "Still open."),
        });
        var agent = AgentOver(chat, provider, Tools.CreateTools());
        var session = await agent.CreateSessionAsync();

        await agent.RunAsync("Where is ORD-1001?", session);
        await agent.RunAsync("And is it still open?", session);

        // Turn two is call 3. It must show turn one in full (the question, the tool call, its result, the answer) and then the new question.
        var shown = chat.Shown(3);
        Assert.Contains("Where is ORD-1001?", shown);
        Assert.Contains($"[call {ElfAgentInstructions.OrderStatusTool}", shown);
        Assert.Contains("ORD-1001 is open.", shown);
        Assert.True(shown.IndexOf("Where is ORD-1001?", StringComparison.Ordinal) < shown.IndexOf("And is it still open?", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Step 9 (short-term agent memory) — turn one is stored whole: the question, the tool call, its result and the answer")]
    public async Task Step09_TheWholeTurnIsStored()
    {
        var store = NewStore("provider-stored", out _);
        var provider = new MongoChatHistoryProvider(store);
        var chat = new ScriptedChat((_, n) => n == 1
            ? Asks(("c1", ElfAgentInstructions.OrderStatusTool, new() { ["orderNumber"] = "ORD-1001" }))
            : Say(ChatRole.Assistant, "ORD-1001 is open."));
        var agent = AgentOver(chat, provider, Tools.CreateTools());
        var session = await agent.CreateSessionAsync();

        await agent.RunAsync("Where is ORD-1001?", session);

        var stored = await store.LoadAsync(MongoChatHistoryProvider.ConversationOf(session));
        Assert.Equal("user,assistant,tool,assistant", Roles(stored));
    }

    [Fact(DisplayName = "Step 9 (short-term agent memory) — after a restart, a new program with a restored session carries on the same conversation")]
    public async Task Step09_MemorySurvivesARestart()
    {
        var collection = Fixture.FreshMessages("provider-restart");

        // Before the restart.
        var first = new MongoChatHistoryProvider(new MongoChatStore(collection));
        var agentOne = AgentOver(Answers("It is a brass test tube."), first);
        var session = await agentOne.CreateSessionAsync();
        await agentOne.RunAsync("What is the safety tube made of?", session);
        var saved = await agentOne.SerializeSessionAsync(session);

        // After it: a new store, a new provider, a new model client, and a session restored from what was saved.
        var chat = Answers("Brass, as I said.");
        var second = new MongoChatHistoryProvider(new MongoChatStore(collection));
        var agentTwo = AgentOver(chat, second);
        var restored = await agentTwo.DeserializeSessionAsync(saved);
        await agentTwo.RunAsync("Say that again?", restored);

        Assert.Contains("What is the safety tube made of?", chat.Shown(1));
        Assert.Contains("It is a brass test tube.", chat.Shown(1));
    }

    [Fact(DisplayName = "Step 9 (short-term agent memory) — two sessions on one conversation id share it; a different id sees nothing")]
    public async Task Step09_ConversationsCanBeSharedOrKeptApart()
    {
        var collection = Fixture.FreshMessages("provider-share");
        var provider = new MongoChatHistoryProvider(new MongoChatStore(collection));

        var agentOne = AgentOver(Answers("Noted."), provider);
        var sessionOne = await agentOne.CreateSessionAsync();
        MongoChatHistoryProvider.UseConversation(sessionOne, "shift-1");
        await agentOne.RunAsync("The reindeer are in the north paddock.", sessionOne);

        var sharedChat = Answers("In the north paddock.");
        var sharedAgent = AgentOver(sharedChat, provider);
        var sharedSession = await sharedAgent.CreateSessionAsync();
        MongoChatHistoryProvider.UseConversation(sharedSession, "shift-1");
        await sharedAgent.RunAsync("Where are the reindeer?", sharedSession);

        var apartChat = Answers("I don't know.");
        var apartAgent = AgentOver(apartChat, provider);
        var apartSession = await apartAgent.CreateSessionAsync();
        MongoChatHistoryProvider.UseConversation(apartSession, "shift-2");
        await apartAgent.RunAsync("Where are the reindeer?", apartSession);

        Assert.Contains("north paddock", sharedChat.Shown(1));
        Assert.DoesNotContain("north paddock", apartChat.Shown(1));
    }

    [Fact(DisplayName = "Step 9 (short-term agent memory) — the window applies to the agent: old turns drop out, and what the model sees still begins with a user message")]
    public async Task Step09_TheWindowReachesTheModel()
    {
        var store = NewStore("provider-window", out _);
        var provider = new MongoChatHistoryProvider(store, maxMessages: 2);
        var chat = Answers("one", "two", "three");
        var agent = AgentOver(chat, provider);
        var session = await agent.CreateSessionAsync();

        await agent.RunAsync("first question", session);
        await agent.RunAsync("second question", session);
        await agent.RunAsync("third question", session);

        // Turn three is call 3. Of the history only the last whole turn (second question, its answer) fits in a window of 2.
        var shown = chat.Calls[2].Messages;
        Assert.DoesNotContain("first question", chat.Shown(3));
        Assert.Contains("second question", chat.Shown(3));
        Assert.Equal(ChatRole.User, shown[0].Role);
    }

    [Fact(DisplayName = "Step 9 (short-term agent memory) — a run that fails remembers nothing: the earlier turn is still there, and the failed one left no trace")]
    public async Task Step09_AFailedRunIsNotRemembered()
    {
        var store = NewStore("provider-failed", out var collection);
        var provider = new MongoChatHistoryProvider(store);

        // The model answers the first question and falls over on the second.
        var chat = new ScriptedChat((_, n) => n == 1
            ? Say(ChatRole.Assistant, "Fine.")
            : throw new InvalidOperationException("the model fell over"));
        var agent = AgentOver(chat, provider);
        var session = await agent.CreateSessionAsync();

        await agent.RunAsync("first question", session);
        Assert.Equal(2, await collection.CountDocumentsAsync(FilterDefinition<StoredMessage>.Empty));

        await Assert.ThrowsAnyAsync<Exception>(() => agent.RunAsync("second question", session));

        Assert.Equal(["first question", "Fine."], (await store.LoadAsync(MongoChatHistoryProvider.ConversationOf(session))).Select(m => m.Text));
    }
}
