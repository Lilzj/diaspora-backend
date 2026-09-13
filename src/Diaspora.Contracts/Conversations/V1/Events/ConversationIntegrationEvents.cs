using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Conversations.V1.Events;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// What the Conversations Platform tells the rest of the platform.
//
// No message body appears in any of these, and that is not an oversight. An integration event is
// written to the outbox, serialised, retried, dead-lettered and read by operators — and it reaches
// consumers this module does not know about. What somebody typed to their landlord belongs in one
// table with one access rule, not in a message bus. Consumers get identifiers, counts and the
// context title, which is already public wherever the entity is.
//
// Recipients are carried as a list rather than one event per person: one message is one fact, and
// splitting it would put N rows in the outbox for one send and make the dedup window meaningless.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>A conversation was opened.</summary>
public sealed record ConversationCreatedIntegrationEvent : IntegrationEvent
{
    public required Guid ConversationId { get; init; }

    /// <summary>A catalogue key — <c>MarketplaceListing</c>, <c>HousingListing</c>, and so on.</summary>
    public required string ContextType { get; init; }

    /// <summary>Whatever the owning module calls its entity. Opaque here.</summary>
    public required string EntityId { get; init; }

    public required Guid InitiatorUserId { get; init; }

    public required IReadOnlyList<Guid> ParticipantUserIds { get; init; }

    /// <summary><c>PendingRequest</c> or <c>Active</c>.</summary>
    public required string Status { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// A message was sent, to recipients who should hear about it.
/// <para>
/// <b><see cref="RecipientUserIds"/> has already had mute and block applied.</b> Those are
/// conversation state and B13 has never heard of them — so the decision is made where the state
/// lives, and this event names only the people who should be interrupted. B13 still decides
/// <em>how</em>: quiet hours, channel preferences, digest batching and throttling are all its own,
/// and re-implementing any of them upstream would be a second opinion that eventually disagrees.
/// </para>
/// </summary>
public sealed record ConversationMessageSentIntegrationEvent : IntegrationEvent
{
    public required Guid ConversationId { get; init; }

    public required Guid MessageId { get; init; }

    public required Guid SenderUserId { get; init; }

    /// <summary>Everybody who should be told. Already filtered — see the summary.</summary>
    public required IReadOnlyList<Guid> RecipientUserIds { get; init; }

    public required string ContextType { get; init; }

    public required string EntityId { get; init; }

    /// <summary>What the conversation is about, as snapshotted. Public wherever the entity is.</summary>
    public required string ContextTitle { get; init; }

    /// <summary><c>text</c>, <c>image</c>, <c>document</c>, and so on. Never the body.</summary>
    public required string MessageKind { get; init; }

    public required bool HasAttachment { get; init; }

    /// <summary>Set when the message answers one of theirs, so B13 can use the reply wording.</summary>
    public Guid? RepliedToUserId { get; init; }

    public required DateTimeOffset SentAt { get; init; }
}

/// <summary>
/// Somebody wrote the first message in a conversation.
/// <para>
/// <b>A fact, where <see cref="ConversationMessageSentIntegrationEvent"/> is a notification.</b> That
/// event names only the people who should be interrupted, and is not published at all when every
/// recipient has muted the conversation or push is switched off — right for a phone, wrong for
/// anything that counts. This one is published for every first message, whoever is listening.
/// </para>
/// <para>
/// <b>Why the first message matters.</b> Tapping "Message" on a listing opens a conversation before
/// anything is typed, and most such conversations stay empty. The first message is the moment an
/// enquiry is real. Later messages in the same conversation are not announced here, so a consumer
/// counting enquiries counts each conversation once.
/// </para>
/// </summary>
public sealed record ConversationFirstMessageSentIntegrationEvent : IntegrationEvent
{
    public required Guid ConversationId { get; init; }

    public required Guid MessageId { get; init; }

    public required Guid SenderUserId { get; init; }

    /// <summary>A catalogue key — <c>MarketplaceListing</c>, <c>HousingListing</c>, and so on.</summary>
    public required string ContextType { get; init; }

    /// <summary>Whatever the owning module calls its entity. Opaque here.</summary>
    public required string EntityId { get; init; }

    public required DateTimeOffset SentAt { get; init; }
}

/// <summary>A message was edited. Carries no text, for the reason at the top of this file.</summary>
public sealed record ConversationMessageEditedIntegrationEvent : IntegrationEvent
{
    public required Guid ConversationId { get; init; }

    public required Guid MessageId { get; init; }

    public required Guid SenderUserId { get; init; }

    public required DateTimeOffset EditedAt { get; init; }
}

/// <summary>A message was withdrawn by its sender.</summary>
public sealed record ConversationMessageDeletedIntegrationEvent : IntegrationEvent
{
    public required Guid ConversationId { get; init; }

    public required Guid MessageId { get; init; }

    public required Guid SenderUserId { get; init; }

    public required DateTimeOffset DeletedAt { get; init; }
}

/// <summary>
/// Somebody read up to a point.
/// <para>
/// Published for the recommendation platform's engagement signals and for analytics, not for
/// delivery: the sender's ticks turn blue over the socket, which is immediate and needs no outbox.
/// </para>
/// </summary>
public sealed record ConversationMessageReadIntegrationEvent : IntegrationEvent
{
    public required Guid ConversationId { get; init; }

    public required Guid ReaderUserId { get; init; }

    public Guid? ThroughMessageId { get; init; }

    public required int ClearedCount { get; init; }

    public required DateTimeOffset ReadAt { get; init; }
}

/// <summary>A member filed a conversation away, for themselves only.</summary>
public sealed record ConversationArchivedIntegrationEvent : IntegrationEvent
{
    public required Guid ConversationId { get; init; }

    public required Guid UserId { get; init; }

    public required DateTimeOffset ArchivedAt { get; init; }
}

/// <summary>A member took a conversation back out of their archive.</summary>
public sealed record ConversationRestoredIntegrationEvent : IntegrationEvent
{
    public required Guid ConversationId { get; init; }

    public required Guid UserId { get; init; }

    public required DateTimeOffset RestoredAt { get; init; }
}

/// <summary>A member silenced or unsilenced a conversation.</summary>
public sealed record ConversationMutedIntegrationEvent : IntegrationEvent
{
    public required Guid ConversationId { get; init; }

    public required Guid UserId { get; init; }

    public required bool IsMuted { get; init; }

    /// <summary>Null while muted means indefinitely.</summary>
    public DateTimeOffset? MutedUntil { get; init; }

    public required DateTimeOffset OccurredOn { get; init; }
}

/// <summary>A member pinned or unpinned a conversation.</summary>
public sealed record ConversationPinnedIntegrationEvent : IntegrationEvent
{
    public required Guid ConversationId { get; init; }

    public required Guid UserId { get; init; }

    public required bool IsPinned { get; init; }

    public required DateTimeOffset OccurredOn { get; init; }
}

/// <summary>
/// A conversation ended.
/// <para>
/// Marketplace reads this to release the enquiry reference it holds, and B13 turns it into a quiet
/// in-app line. The reason is a short machine token — <c>declined</c>, <c>entity_closed</c>,
/// <c>moderated</c> — not a sentence, because a consumer branches on it and a sentence is localised.
/// </para>
/// </summary>
public sealed record ConversationClosedIntegrationEvent : IntegrationEvent
{
    public required Guid ConversationId { get; init; }

    public required IReadOnlyList<Guid> ParticipantUserIds { get; init; }

    public required string ContextType { get; init; }

    public required string EntityId { get; init; }

    public required string ContextTitle { get; init; }

    public required string Reason { get; init; }

    public required DateTimeOffset ClosedAt { get; init; }
}

/// <summary>Somebody was added to a conversation.</summary>
public sealed record ConversationParticipantAddedIntegrationEvent : IntegrationEvent
{
    public required Guid ConversationId { get; init; }

    public required Guid UserId { get; init; }

    public required string Role { get; init; }

    public required string ContextType { get; init; }

    public required string ContextTitle { get; init; }

    public required DateTimeOffset JoinedAt { get; init; }
}

/// <summary>Somebody was removed from a conversation, or left it.</summary>
public sealed record ConversationParticipantRemovedIntegrationEvent : IntegrationEvent
{
    public required Guid ConversationId { get; init; }

    public required Guid UserId { get; init; }

    public required DateTimeOffset RemovedAt { get; init; }
}

/// <summary>
/// A pending request was accepted.
/// <para>
/// Worth its own event rather than folding into <c>ConversationCreated</c>: a request that was
/// accepted is a materially different fact from one that was opened, and it is the moment Trust and
/// Recommendation would count a genuine connection.
/// </para>
/// </summary>
public sealed record ConversationRequestAcceptedIntegrationEvent : IntegrationEvent
{
    public required Guid ConversationId { get; init; }

    public required Guid AcceptedByUserId { get; init; }

    public required Guid InitiatorUserId { get; init; }

    public required DateTimeOffset AcceptedAt { get; init; }
}

/// <summary>
/// One member blocked another.
/// <para>
/// Published so Trust can weigh a pattern of being blocked and Recommendation can stop suggesting
/// somebody a member has shut out. <b>Carries no reason</b> — the reason is the blocker's private
/// note and is never shown even to the blocked member, so it has no business on a bus.
/// </para>
/// </summary>
public sealed record ConversationBlockedIntegrationEvent : IntegrationEvent
{
    public required Guid BlockerUserId { get; init; }

    public required Guid BlockedUserId { get; init; }

    public required DateTimeOffset BlockedAt { get; init; }
}

/// <summary>A block was lifted.</summary>
public sealed record ConversationUnblockedIntegrationEvent : IntegrationEvent
{
    public required Guid BlockerUserId { get; init; }

    public required Guid BlockedUserId { get; init; }

    public required DateTimeOffset UnblockedAt { get; init; }
}
