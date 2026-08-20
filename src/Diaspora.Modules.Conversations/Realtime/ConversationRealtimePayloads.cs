namespace Diaspora.Modules.Conversations.Realtime;

/// <summary>
/// A message, as a connected client receives it.
/// <para>
/// <b>The same words the REST surface uses, under the same names.</b> This is the live view of a row
/// the client will also fetch, so every field is spelled exactly as <c>MessageResponse</c> spells it
/// — <c>messageId</c>, <c>senderId</c>, <c>type</c>, <c>text</c>, <c>createdAt</c>. A client that was
/// offline fetches history and gets these fields inside a larger record, so one render path serves
/// both and no mapper sits between them. B13 learned this the expensive way with
/// <c>RealtimeNotification</c>, which was renamed toward the client before it shipped.
/// </para>
/// <para>
/// <b>A subset, not a second richer channel.</b> Absent here and present on REST: <c>attachment</c>'s
/// full shape, <c>replyTo</c>'s preview, <c>isEdited</c>, <c>delivery</c>. A message that has this
/// instant arrived has known values for the last two, and a client that needs the first two has the
/// message identifier to fetch with.
/// </para>
/// </summary>
public sealed record ConversationMessagePayload(
    Guid ConversationId,
    Guid MessageId,
    Guid SenderId,
    string Type,
    string? Text,
    bool HasAttachment,
    Guid? ReplyToMessageId,
    DateTimeOffset CreatedAt,
    string? LocalId);

/// <summary>
/// A conversation appeared for this member.
/// <para>
/// Carries the identifier and enough to render an inbox row's skeleton, not the whole conversation.
/// The client fetches the rest — it has to anyway, because participants and context need the
/// resolvers a socket write has no business invoking.
/// </para>
/// </summary>
public sealed record ConversationCreatedPayload(
    Guid ConversationId,
    string ContextType,
    string EntityId,
    Guid InitiatorUserId,
    string State,
    DateTimeOffset CreatedAt);

/// <summary>
/// One of this member's per-conversation flags moved.
/// <para>
/// Sent to the member's own group, never to the conversation group: archiving is a private filing
/// decision and broadcasting it would tell the other party you had put them away.
/// </para>
/// </summary>
public sealed record ConversationStateChangedPayload(
    Guid ConversationId,
    string State,
    bool IsArchived,
    bool IsMuted,
    bool IsPinned,
    DateTimeOffset? MutedUntil,
    DateTimeOffset At);

/// <summary>A conversation ended for everybody in it.</summary>
public sealed record ConversationClosedPayload(
    Guid ConversationId,
    string Reason,
    DateTimeOffset ClosedAt);

/// <summary>
/// The conversation unread badge.
/// <para>
/// Named <c>total</c> to match <c>ConversationUnreadCountResponse</c>, for the same reason B13
/// renamed its own counter: a client stores one number, and two names for it is how a client ends up
/// with two.
/// </para>
/// </summary>
public sealed record ConversationUnreadPayload(int Total, DateTimeOffset At);
