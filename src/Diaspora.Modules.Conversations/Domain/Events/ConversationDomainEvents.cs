using Diaspora.Modules.Conversations.Domain.Enums;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Conversations.Domain.Events;

/// <summary>
/// A conversation was opened.
/// <para>
/// <b>Carries the recipients rather than leaving a handler to load them.</b> The handler that turns
/// this into a realtime write and a push runs after the transaction, and the aggregate it would
/// reload is the one it is already being told about — a reload that costs a round trip and can
/// observe a later state than the event describes.
/// </para>
/// </summary>
public sealed record ConversationCreatedDomainEvent : DomainEvent
{
    public required ConversationId ConversationId { get; init; }

    public required string ContextTypeKey { get; init; }

    public required string EntityId { get; init; }

    public required UserId InitiatorUserId { get; init; }

    public required IReadOnlyList<UserId> ParticipantUserIds { get; init; }

    public required ConversationStatus Status { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>A message was appended. The event the whole realtime path hangs from.</summary>
public sealed record MessageSentDomainEvent : DomainEvent
{
    public required ConversationId ConversationId { get; init; }

    public required ConversationMessageId MessageId { get; init; }

    public required UserId SenderId { get; init; }

    public required MessageKind Kind { get; init; }

    /// <summary>
    /// Everybody except the sender.
    /// <para>
    /// Computed on the aggregate, where participation is known, rather than in the handler — a
    /// handler that filtered the sender out itself is a handler that will one day forget to, and
    /// the symptom is a member being pushed their own message on their own phone.
    /// </para>
    /// </summary>
    public required IReadOnlyList<UserId> RecipientUserIds { get; init; }

    public required DateTimeOffset SentAt { get; init; }

    /// <summary>Present when the sender was answering something. Used for the reply notification.</summary>
    public ConversationMessageId? ReplyToMessageId { get; init; }

    public UserId? ReplyToSenderId { get; init; }

    public bool HasAttachment { get; init; }
}

public sealed record MessageEditedDomainEvent : DomainEvent
{
    public required ConversationId ConversationId { get; init; }

    public required ConversationMessageId MessageId { get; init; }

    public required UserId SenderId { get; init; }

    public required DateTimeOffset EditedAt { get; init; }
}

public sealed record MessageDeletedDomainEvent : DomainEvent
{
    public required ConversationId ConversationId { get; init; }

    public required ConversationMessageId MessageId { get; init; }

    public required UserId SenderId { get; init; }

    public required DateTimeOffset DeletedAt { get; init; }
}

/// <summary>
/// A participant read up to a point.
/// <para>
/// Raised by the conversation rather than by the message, because the read cursor is participant
/// state and the message does not know who has seen it. The receipt that reaches the other party's
/// socket is derived from this.
/// </para>
/// </summary>
public sealed record MessagesReadDomainEvent : DomainEvent
{
    public required ConversationId ConversationId { get; init; }

    public required UserId ReaderUserId { get; init; }

    public ConversationMessageId? ThroughMessageId { get; init; }

    public required int ClearedCount { get; init; }

    public required DateTimeOffset ReadAt { get; init; }
}

public sealed record ConversationArchivedDomainEvent : DomainEvent
{
    public required ConversationId ConversationId { get; init; }

    public required UserId UserId { get; init; }

    public required DateTimeOffset ArchivedAt { get; init; }
}

public sealed record ConversationRestoredDomainEvent : DomainEvent
{
    public required ConversationId ConversationId { get; init; }

    public required UserId UserId { get; init; }

    public required DateTimeOffset RestoredAt { get; init; }
}

public sealed record ConversationMutedDomainEvent : DomainEvent
{
    public required ConversationId ConversationId { get; init; }

    public required UserId UserId { get; init; }

    /// <summary>Null when the mute is indefinite; absent entirely when this is an unmute.</summary>
    public DateTimeOffset? MutedUntil { get; init; }

    public required bool IsMuted { get; init; }

    public required DateTimeOffset OccurredOn { get; init; }
}

public sealed record ConversationPinnedDomainEvent : DomainEvent
{
    public required ConversationId ConversationId { get; init; }

    public required UserId UserId { get; init; }

    public required bool IsPinned { get; init; }

    public required DateTimeOffset OccurredOn { get; init; }
}

public sealed record ConversationClosedDomainEvent : DomainEvent
{
    public required ConversationId ConversationId { get; init; }

    public required IReadOnlyList<UserId> ParticipantUserIds { get; init; }

    public required string Reason { get; init; }

    public required DateTimeOffset ClosedAt { get; init; }
}

public sealed record ParticipantAddedDomainEvent : DomainEvent
{
    public required ConversationId ConversationId { get; init; }

    public required UserId UserId { get; init; }

    public required ParticipantRole Role { get; init; }

    public required DateTimeOffset JoinedAt { get; init; }
}

public sealed record ParticipantRemovedDomainEvent : DomainEvent
{
    public required ConversationId ConversationId { get; init; }

    public required UserId UserId { get; init; }

    public required DateTimeOffset RemovedAt { get; init; }
}

/// <summary>The recipient of a pending request accepted it.</summary>
public sealed record ConversationRequestAcceptedDomainEvent : DomainEvent
{
    public required ConversationId ConversationId { get; init; }

    public required UserId AcceptedByUserId { get; init; }

    public required UserId InitiatorUserId { get; init; }

    public required DateTimeOffset AcceptedAt { get; init; }
}

public sealed record ConversationBlockedDomainEvent : DomainEvent
{
    public required ConversationBlockId BlockId { get; init; }

    public required UserId BlockerUserId { get; init; }

    public required UserId BlockedUserId { get; init; }

    public required DateTimeOffset BlockedAt { get; init; }
}

public sealed record ConversationUnblockedDomainEvent : DomainEvent
{
    public required UserId BlockerUserId { get; init; }

    public required UserId BlockedUserId { get; init; }

    public required DateTimeOffset UnblockedAt { get; init; }
}
