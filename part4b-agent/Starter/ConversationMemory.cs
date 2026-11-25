using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Part4B.Starter;

/// <summary>One message of one conversation, as MongoDB stores it.</summary>
public sealed class StoredMessage
{
    [BsonId, BsonIgnoreIfDefault] public ObjectId Id { get; set; }

    /// <summary>Which conversation this belongs to.</summary>
    [BsonElement("conversationId")] public required string ConversationId { get; set; }

    /// <summary>Its position in that conversation, from 0. Messages are read back in this order.</summary>
    [BsonElement("sequence")] public required long Sequence { get; set; }

    /// <summary>Handy to read in Compass: <c>user</c>, <c>assistant</c> or <c>tool</c>.</summary>
    [BsonElement("role")] public required string Role { get; set; }

    /// <summary>The whole <see cref="ChatMessage"/> as JSON: its text, and any tool calls and results inside it.</summary>
    [BsonElement("message")] public required string Json { get; set; }

    /// <summary>When it was stored. A TTL index on this is what makes short-term memory short.</summary>
    [BsonElement("createdAt")] public required DateTime CreatedAt { get; set; }
}

/// <summary>
/// Step 7: <b>short-term memory</b> is the conversation so far. It is kept in MongoDB so that it survives the program being restarted,
/// and so that more than one copy of the program could share it.
/// </summary>
/// <remarks>
/// <para><b>Short-term</b> memory is what was said <i>in this conversation</i>: the messages themselves, tool calls and all. It is replayed to the
/// model every turn, it grows with every turn, and it should expire. <b>Long-term</b> memory is what's worth keeping <i>across</i> conversations:
/// facts and preferences, distilled out of what was said. It is searched rather than replayed. This part builds the first.</para>
/// </remarks>
public sealed class MongoChatStore(IMongoCollection<StoredMessage> messages)
{
    private readonly IMongoCollection<StoredMessage> _messages = messages;

    /// <summary>
    /// Step 7 (badge objective: implement short-term agent memory with MongoDB) — store <paramref name="newMessages"/> at the end of
    /// <paramref name="conversationId"/>.
    /// </summary>
    public async Task AppendAsync(string conversationId, IEnumerable<ChatMessage> newMessages)
    {
        // TODO (Step 7): for each message, insert a StoredMessage with
        //   ConversationId   conversationId
        //   Sequence         its position in the whole conversation: the number already stored, then +1 for each one you add
        //   Role             message.Role.Value
        //   Json             JsonSerializer.Serialize(message, AIJsonUtilities.DefaultOptions)   <- keeps tool calls and results intact
        //   CreatedAt        DateTime.UtcNow
        // Storing nothing is fine: no messages, no work.
        throw new NotImplementedException("Step 7: remember a conversation.");
    }

    /// <summary>
    /// Step 7 — read <paramref name="conversationId"/> back, oldest first. A conversation nobody has stored is empty, not an error.
    /// </summary>
    public async Task<List<ChatMessage>> LoadAsync(string conversationId)
    {
        // TODO (Step 7): find this conversation's StoredMessages, sorted by Sequence, and turn each Json back into a ChatMessage:
        //   JsonSerializer.Deserialize<ChatMessage>(json, AIJsonUtilities.DefaultOptions)!
        throw new NotImplementedException("Step 7: recall a conversation.");
    }

    /// <summary>
    /// Step 7 — create the two indexes this collection needs: one so that reading a conversation is fast and no message can be stored
    /// twice, and one so that old conversations are forgotten.
    /// </summary>
    public async Task EnsureIndexesAsync(TimeSpan keepFor)
    {
        // TODO (Step 7): CreateManyAsync with two CreateIndexModel<StoredMessage>:
        //   1. a UNIQUE compound index on ConversationId then Sequence   (look-ups by conversation, in order; and a message can't be written twice)
        //   2. a TTL index on CreatedAt that expires documents after `keepFor`  (CreateIndexOptions { ExpireAfter = keepFor })
        //
        // A TTL index makes MongoDB delete documents for you once they are older than the limit. (It checks about once a minute, so
        // not to the second.) Short-term memory that never expires is just a log.
        throw new NotImplementedException("Step 7: index the conversations, and let them expire.");
    }
}

/// <summary>Step 8: short-term memory has a size limit, and cutting it carelessly breaks the conversation.</summary>
public static class ChatWindow
{
    /// <summary>
    /// Step 8 (badge objective: short-term memory) — keep roughly the last <paramref name="maxMessages"/> messages, but only ever
    /// starting at a <b>user</b> message.
    /// </summary>
    /// <remarks>
    /// <para>A model's context window is finite and every message costs tokens, so you can't replay a conversation for ever.</para>
    /// <para>Cut in the wrong place and you break it. A tool call and its result belong together: a result with no call before it
    /// (or a call with no result after it) is an error from most model APIs. So the window may only begin where a <i>turn</i> begins, at a
    /// user message. That can keep slightly fewer than <paramref name="maxMessages"/>, never more.</para>
    /// </remarks>
    public static List<ChatMessage> TrimToRecent(IReadOnlyList<ChatMessage> messages, int maxMessages)
    {
        // TODO (Step 8):
        //   - If everything fits (messages.Count <= maxMessages), return all of it, as a new list.
        //   - Otherwise start at messages.Count - maxMessages, and move FORWARD to the first message with role ChatRole.User.
        //     Return everything from there.
        //   - If there is no user message from that point on, return an empty list: there is no whole turn that fits.
        //   - maxMessages below 1 is an ArgumentOutOfRangeException.
        throw new NotImplementedException("Step 8: trim without breaking a turn.");
    }
}

/// <summary>
/// Step 9: plugs <see cref="MongoChatStore"/> into Agent Framework. A <c>ChatHistoryProvider</c> is asked for the history before each run, and
/// told what happened after it.
/// </summary>
/// <remarks>
/// Which conversation a session belongs to is kept <i>in the session itself</i> (<see cref="UseConversation"/>). Serialise the session, and its
/// conversation id goes with it: that is how a new copy of the program, after a restart, picks the conversation up again.
/// </remarks>
public sealed class MongoChatHistoryProvider(MongoChatStore store, int maxMessages = MongoChatHistoryProvider.DefaultMaxMessages) : ChatHistoryProvider
{
    public const int DefaultMaxMessages = 20;

    private readonly MongoChatStore _store = store;
    private readonly int _maxMessages = maxMessages;

    private const string ConversationKey = "elf.conversationId";

    /// <summary>Makes <paramref name="session"/> belong to the conversation <paramref name="conversationId"/>.</summary>
    public static void UseConversation(AgentSession session, string conversationId) =>
        session.StateBag.SetValue(ConversationKey, conversationId);

    /// <summary>The conversation <paramref name="session"/> belongs to: the one it was given, or a new one that it then keeps.</summary>
    public static string ConversationOf(AgentSession session)
    {
        if (session.StateBag.TryGetValue<string>(ConversationKey, out var existing) && !string.IsNullOrEmpty(existing))
            return existing;

        var created = Guid.NewGuid().ToString("N");
        session.StateBag.SetValue(ConversationKey, created);
        return created;
    }

    public override IReadOnlyList<string> StateKeys => [ConversationKey];

    /// <summary>
    /// Step 9 (badge objective: short-term agent memory with MongoDB) — the history to show the model before a run: this session's
    /// conversation, from MongoDB, trimmed to the most recent <c>maxMessages</c> without breaking a turn.
    /// </summary>
    public async Task<List<ChatMessage>> RecallAsync(AgentSession session)
    {
        // TODO (Step 9): load the conversation (ConversationOf(session)) from _store, and return ChatWindow.TrimToRecent(it, _maxMessages).
        throw new NotImplementedException("Step 9: recall the conversation.");
    }

    /// <summary>
    /// Step 9 — after a run, store what happened: the messages the elf sent, then everything the agent produced (its tool calls,
    /// the results, and the answer).
    /// </summary>
    public async Task RememberAsync(AgentSession session, IEnumerable<ChatMessage> request, IEnumerable<ChatMessage> response)
    {
        // TODO (Step 9): append request, then response, to this session's conversation in _store.
        throw new NotImplementedException("Step 9: remember what happened.");
    }

    // The two hooks Agent Framework calls. Already written: they hand over to your methods.
    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(InvokingContext context, CancellationToken cancellationToken = default) =>
        await RecallAsync(context.Session!);

    protected override async ValueTask StoreChatHistoryAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        // A run that failed has nothing worth remembering.
        if (context.InvokeException is not null) return;

        await RememberAsync(context.Session!, context.RequestMessages, context.ResponseMessages ?? []);
    }
}
