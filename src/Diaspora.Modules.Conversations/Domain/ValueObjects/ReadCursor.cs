using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Conversations.Domain.ValueObjects;

/// <summary>
/// How far one participant has read.
/// <para>
/// <b>A position and a count, and the count is not derivable from the position.</b> "Everything up
/// to message X" answers where to draw the unread separator; "seven unread" answers what the badge
/// says. They look like the same fact and are not: messages arrive after the cursor is set, the
/// sender's own messages never count as unread to them, and deleted messages leave the position
/// valid while changing the count. Deriving one from the other means a <c>COUNT(*)</c> over the
/// message table per conversation per inbox render — the classic N+1 this phase is required not to
/// ship — so the count is maintained where messages are appended and read here.
/// </para>
/// <para>
/// <b>Not persisted as an owned type.</b> It projects three flat columns on the participant row.
/// Nesting an owned value object inside an owned collection entity is legal EF and is also how this
/// platform has previously acquired a shadow composite key it did not want; the columns are plain
/// and this type is the domain's way of talking about them.
/// </para>
/// </summary>
public sealed class ReadCursor : ValueObject
{
    /// <summary>Nothing read. The state of every participant the instant they join.</summary>
    public static ReadCursor Unread { get; } = new(null, null, 0);

    private ReadCursor(ConversationMessageId? lastReadMessageId, DateTimeOffset? lastReadAt, int unreadCount)
    {
        LastReadMessageId = lastReadMessageId;
        LastReadAt = lastReadAt;
        UnreadCount = unreadCount;
    }

    public ConversationMessageId? LastReadMessageId { get; }

    public DateTimeOffset? LastReadAt { get; }

    public int UnreadCount { get; }

    public bool HasUnread => UnreadCount > 0;

    public static ReadCursor Create(
        ConversationMessageId? lastReadMessageId, DateTimeOffset? lastReadAt, int unreadCount) =>
        new(lastReadMessageId, lastReadAt, unreadCount < 0 ? 0 : unreadCount);

    /// <summary>Moves the cursor forward and clears the count.</summary>
    public ReadCursor ReadThrough(ConversationMessageId? messageId, DateTimeOffset at) =>
        new(messageId ?? LastReadMessageId, at, 0);

    /// <summary>Counts one more arrival without moving the position.</summary>
    public ReadCursor Arrive() => new(LastReadMessageId, LastReadAt, UnreadCount + 1);

    /// <summary>
    /// Uncounts one arrival — a message was deleted before it was read.
    /// <para>
    /// Floors at zero rather than trusting arithmetic. A badge that went negative because two
    /// deletions raced a read would be a number a member reports as a bug and nobody can reproduce.
    /// </para>
    /// </summary>
    public ReadCursor Withdraw() =>
        new(LastReadMessageId, LastReadAt, UnreadCount > 0 ? UnreadCount - 1 : 0);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return LastReadMessageId;
        yield return LastReadAt;
        yield return UnreadCount;
    }
}
