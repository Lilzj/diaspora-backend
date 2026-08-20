using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Conversations.Domain.Enums;
using Diaspora.Modules.Conversations.Domain.Errors;
using Diaspora.Modules.Conversations.Domain.Events;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.Policies;
using Diaspora.Modules.Conversations.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Conversations.Domain.Conversations;

/// <summary>
/// A thread between people, about something.
/// <para>
/// <b>The aggregate owns its participants and does not own its messages, and that split is the
/// single most consequential decision in this module.</b> Participants are bounded — eight at most,
/// loaded on every read, written on every send — so they are an owned collection and every rule
/// about who is in a conversation is enforced in one transaction. Messages are unbounded: a
/// year-old enquiry can hold four thousand, and an owned collection would load all of them to
/// append the four thousand and first. They are a separate aggregate with their own repository and
/// their own cursor.
/// </para>
/// <para>
/// The cost of that split is that "append a message" touches two aggregates, and the counters here
/// — <see cref="MessageCount"/>, <see cref="LastMessageAt"/>, each participant's unread — would
/// drift if either half could be written alone. They cannot: the send path is
/// <c>[Transactional]</c>, the message is added and <see cref="RecordMessage"/> called inside one
/// unit of work, and an integration test kills the write between them to prove the counter does not
/// move on its own.
/// </para>
/// <para>
/// <b>Nothing here knows what a Marketplace listing is.</b> The context is a catalogue key and an
/// opaque identifier; no method branches on the type, and an architecture test says so. That is what
/// lets a vertical land without this module changing.
/// </para>
/// </summary>
public sealed class Conversation
    : AggregateRoot<ConversationId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<ConversationParticipant> _participants = [];

    private Conversation(
        ConversationId id,
        ConversationContext context,
        ConversationMetadata metadata,
        ConversationStatus status,
        DateTimeOffset createdAt)
        : base(id)
    {
        Context = context;
        Metadata = metadata;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        LastActivityAt = createdAt;
    }

    private Conversation() { }

    public ConversationContext Context { get; private set; } = default!;

    public ConversationMetadata Metadata { get; private set; } = default!;

    public ConversationStatus Status { get; private set; }

    /// <summary>
    /// Every participation this conversation has ever had, including people who left.
    /// <para>
    /// Exposed read-only, which means EF reaches the list through the backing field. The field is
    /// named <c>_participants</c> so the by-name convention finds it; a mismatch surfaces as
    /// "Collection is read-only" on the first read-back and on nothing before that.
    /// </para>
    /// </summary>
    public IReadOnlyCollection<ConversationParticipant> Participants => _participants.AsReadOnly();

    public IEnumerable<ConversationParticipant> ActiveParticipants =>
        _participants.Where(participant => participant.IsActive);

    /// <summary>The last message, denormalised so an inbox render is one query.</summary>
    public ConversationMessageId? LastMessageId { get; private set; }

    public DateTimeOffset? LastMessageAt { get; private set; }

    public UserId? LastMessageSenderId { get; private set; }

    public int MessageCount { get; private set; }

    /// <summary>
    /// What the inbox sorts by.
    /// <para>
    /// Distinct from <see cref="UpdatedAt"/>, which moves when anybody mutes or pins. Sorting an
    /// inbox by "last touched" would float a conversation to the top because somebody archived it,
    /// which is the opposite of what archiving is for.
    /// </para>
    /// </summary>
    public DateTimeOffset LastActivityAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public string? ClosedReason { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>The optimistic concurrency token. Two people muting at once must not lose a mute.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public bool IsClosed => Status == ConversationStatus.Closed;

    public bool IsPendingRequest => Status == ConversationStatus.PendingRequest;

    // ════════════════════════════════════════════════════════════════════════════════════════════
    // Opening
    // ════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Opens a conversation between two people about something.
    /// <para>
    /// <paramref name="requiresAcceptance"/> is decided by the caller, not here: whether two people
    /// have a prior relationship is a Trust question and this module does not answer Trust
    /// questions. What the domain guarantees is that a conversation which requires acceptance starts
    /// <see cref="ConversationStatus.PendingRequest"/> and refuses messages from the recipient until
    /// they accept — see <see cref="AcceptRequest"/>.
    /// </para>
    /// </summary>
    public static Result<Conversation> Open(
        ConversationId id,
        ConversationContext context,
        UserId initiatorUserId,
        UserId recipientUserId,
        DateTimeOffset now,
        ConversationMetadata? metadata = null,
        bool requiresAcceptance = false)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(initiatorUserId);
        ArgumentNullException.ThrowIfNull(recipientUserId);

        if (initiatorUserId == recipientUserId)
        {
            return Result.Failure<Conversation>(ConversationErrors.SelfConversation);
        }

        var conversation = new Conversation(
            id,
            context,

            // Never the Empty singleton. A cached owned value object shared by two aggregates is a
            // defect this platform has already paid for: EF tracks the one instance, writes it under
            // the first owner and leaves the second owner's columns null.
            metadata ?? ConversationMetadata.Create(),
            requiresAcceptance ? ConversationStatus.PendingRequest : ConversationStatus.Active,
            now);

        conversation._participants.Add(
            ConversationParticipant.Join(initiatorUserId, ParticipantRole.Initiator, now));

        conversation._participants.Add(
            ConversationParticipant.Join(recipientUserId, ParticipantRole.Recipient, now));

        conversation.RaiseDomainEvent(new ConversationCreatedDomainEvent
        {
            ConversationId = id,
            ContextTypeKey = context.Type.Key,
            EntityId = context.EntityId,
            InitiatorUserId = initiatorUserId,
            ParticipantUserIds = [initiatorUserId, recipientUserId],
            Status = conversation.Status,
            CreatedAt = now
        });

        return Result.Success(conversation);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════
    // Participation
    // ════════════════════════════════════════════════════════════════════════════════════════════

    public ConversationParticipant? ParticipantFor(UserId userId) =>
        userId is null
            ? null
            : _participants.Find(participant => participant.UserId == userId);

    /// <summary>
    /// Whether this member may currently see the conversation at all.
    /// <para>
    /// Somebody who has left is <em>not</em> a participant for this purpose. Their old messages
    /// remain attributed to them, but they lose the thread — which is what removal means and what a
    /// caller reading a conversation they were removed from must not be able to do.
    /// </para>
    /// </summary>
    public bool HasActiveParticipant(UserId userId) =>
        ParticipantFor(userId) is { IsActive: true };

    /// <summary>
    /// Everybody owed a notice that this thread has gone quiet — B23.x Wave 12.
    /// <para>
    /// <b>Not the person who spoke last.</b> Telling somebody nobody replied to them is a reminder;
    /// telling the person who sent the last message that it went quiet is telling them what they
    /// already know, and it is the difference between a useful trigger and one a member switches off.
    /// </para>
    /// <para>
    /// <b>Not a closed conversation, and not a pending request.</b> A closed thread is over and a
    /// request nobody accepted was never a conversation — reminding on either would be reminding
    /// somebody about a silence that is the correct outcome.
    /// </para>
    /// <para>
    /// Mute, archive and having left are applied per participant, where that state lives. Marking is
    /// the caller's next step and is a separate method, so a sweep that fails between deciding and
    /// publishing re-decides rather than silently swallowing the notice.
    /// </para>
    /// </summary>
    public IReadOnlyList<ConversationParticipant> ParticipantsOwedInactivityNotice(
        DateTimeOffset now)
    {
        if (Status != ConversationStatus.Active || LastMessageAt is not { } lastMessageAt)
        {
            return [];
        }

        return
        [
            .. _participants.Where(participant =>
                participant.UserId != LastMessageSenderId &&
                participant.IsOwedInactivityNotice(lastMessageAt, now))
        ];
    }

    /// <summary>
    /// Records that a participant was told the thread was quiet — B23.x Wave 12.
    /// <para>
    /// On the aggregate because participation is owned by it: a sweep reaching into the child entity
    /// would be the one caller in the module that writes participation without going through here.
    /// </para>
    /// </summary>
    public void NoteInactivityReported(UserId userId, DateTimeOffset at) =>
        ParticipantFor(userId)?.NoteInactivityReported(at);

    /// <summary>Records that a member was told they had unread messages — B23.x Wave 12.</summary>
    public void NoteUnreadReported(UserId userId, DateTimeOffset at) =>
        ParticipantFor(userId)?.NoteUnreadReported(at);

    public Result AddParticipant(UserId userId, ParticipantRole role, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(userId);

        if (IsClosed)
        {
            return Result.Failure(ConversationErrors.Closed);
        }

        var existing = ParticipantFor(userId);

        if (existing is { IsActive: true })
        {
            return Result.Success();
        }

        if (existing is not null)
        {
            existing.Rejoin(role, now);
            Touch(now);

            RaiseDomainEvent(new ParticipantAddedDomainEvent
            {
                ConversationId = Id,
                UserId = userId,
                Role = role,
                JoinedAt = now
            });

            return Result.Success();
        }

        if (ActiveParticipants.Count() >= ConversationLimits.MaxParticipants)
        {
            return Result.Failure(ConversationErrors.ParticipantLimitReached);
        }

        _participants.Add(ConversationParticipant.Join(userId, role, now));
        Touch(now);

        RaiseDomainEvent(new ParticipantAddedDomainEvent
        {
            ConversationId = Id,
            UserId = userId,
            Role = role,
            JoinedAt = now
        });

        return Result.Success();
    }

    public Result RemoveParticipant(UserId userId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(userId);

        if (ParticipantFor(userId) is not { IsActive: true } participant)
        {
            return Result.Failure(ConversationErrors.NotAParticipant);
        }

        participant.Leave(now);
        Touch(now);

        RaiseDomainEvent(new ParticipantRemovedDomainEvent
        {
            ConversationId = Id,
            UserId = userId,
            RemovedAt = now
        });

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The recipient of a pending request accepts it.
    /// <para>
    /// Only the recipient may. An initiator who could accept their own request would be an initiator
    /// who could bypass the whole mechanism by calling one more endpoint.
    /// </para>
    /// </summary>
    public Result AcceptRequest(UserId acceptedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(acceptedBy);

        if (Status != ConversationStatus.PendingRequest)
        {
            return Result.Failure(ConversationErrors.RequestNotFound);
        }

        if (ParticipantFor(acceptedBy) is not { IsActive: true, Role: ParticipantRole.Recipient })
        {
            return Result.Failure(ConversationErrors.RequestNotFound);
        }

        var initiator = _participants
            .Find(participant => participant.Role == ParticipantRole.Initiator);

        Status = ConversationStatus.Active;
        Touch(now);

        RaiseDomainEvent(new ConversationRequestAcceptedDomainEvent
        {
            ConversationId = Id,
            AcceptedByUserId = acceptedBy,
            InitiatorUserId = initiator?.UserId ?? acceptedBy,
            AcceptedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Ends the conversation for everybody.
    /// <para>
    /// <b>Not a deletion.</b> A closed conversation is still readable by its participants — the
    /// listing was sold, and what was agreed before it sold is exactly what somebody needs to read
    /// afterwards. It refuses new messages and nothing else.
    /// </para>
    /// </summary>
    public Result Close(string reason, DateTimeOffset now)
    {
        if (IsClosed)
        {
            return Result.Success();
        }

        Status = ConversationStatus.Closed;
        ClosedAt = now;
        ClosedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        Touch(now);

        RaiseDomainEvent(new ConversationClosedDomainEvent
        {
            ConversationId = Id,
            ParticipantUserIds = [.. ActiveParticipants.Select(participant => participant.UserId)],
            Reason = ClosedReason ?? string.Empty,
            ClosedAt = now
        });

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════
    // Message bookkeeping
    // ════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Records that a message was appended, and counts it unread for everybody but the sender.
    /// <para>
    /// <b>Called inside the same transaction as the message insert, never after it.</b> The message
    /// row and these counters are one fact expressed twice; a caller that committed one without the
    /// other would leave an inbox badge that no read can clear, because the message it counts does
    /// not exist.
    /// </para>
    /// </summary>
    public Result<IReadOnlyList<UserId>> RecordMessage(
        ConversationMessageId messageId, UserId senderId, DateTimeOffset sentAt)
    {
        ArgumentNullException.ThrowIfNull(messageId);
        ArgumentNullException.ThrowIfNull(senderId);

        if (IsClosed)
        {
            return Result.Failure<IReadOnlyList<UserId>>(ConversationErrors.Closed);
        }

        if (ParticipantFor(senderId) is not { IsActive: true } sender)
        {
            return Result.Failure<IReadOnlyList<UserId>>(ConversationErrors.NotAParticipant);
        }

        // A pending request carries the initiator's opening message and nothing more until it is
        // accepted. Letting the initiator keep writing would make the request mechanism a delay
        // rather than a consent gate, which is the whole of what it is for.
        if (Status == ConversationStatus.PendingRequest
            && sender.Role == ParticipantRole.Initiator
            && MessageCount > 0)
        {
            return Result.Failure<IReadOnlyList<UserId>>(ConversationErrors.RequestPending);
        }

        // The recipient answering a request accepts it. Refusing to send until they press a separate
        // button would be a state the client cannot reach: its composer is the accept.
        if (Status == ConversationStatus.PendingRequest && sender.Role == ParticipantRole.Recipient)
        {
            var accepted = AcceptRequest(senderId, sentAt);

            if (accepted.IsFailure)
            {
                return Result.Failure<IReadOnlyList<UserId>>(accepted.Error);
            }
        }

        var recipients = new List<UserId>();

        foreach (var participant in _participants.Where(participant => participant.IsActive))
        {
            if (participant.UserId == senderId)
            {
                // The sender has read what they just wrote. Not doing this is how a member's own
                // message shows as unread on their own second device.
                participant.ReadThrough(messageId, sentAt);
                continue;
            }

            participant.RecordArrival(sentAt);
            recipients.Add(participant.UserId);
        }

        LastMessageId = messageId;
        LastMessageAt = sentAt;
        LastMessageSenderId = senderId;
        LastActivityAt = sentAt;
        MessageCount++;
        Touch(sentAt);

        return Result.Success<IReadOnlyList<UserId>>(recipients);
    }

    /// <summary>
    /// Un-counts a message withdrawn before its recipients read it.
    /// <para>
    /// <see cref="MessageCount"/> deliberately does not move. It counts what was said, and a deleted
    /// message was said — the row survives, the body does not. Decrementing it would make a cursor
    /// page that returns a tombstone look like it over-returned.
    /// </para>
    /// </summary>
    public void WithdrawMessage(UserId senderId, ConversationMessageId messageId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(senderId);
        ArgumentNullException.ThrowIfNull(messageId);

        foreach (var participant in _participants.Where(participant => participant.IsActive))
        {
            if (participant.UserId == senderId)
            {
                continue;
            }

            // Only when they had not read past it. Decrementing unconditionally is how a badge goes
            // negative, and the platform's own floor at zero would then hide the arithmetic error
            // rather than the symptom.
            if (participant.LastReadMessageId != messageId && participant.UnreadCount > 0)
            {
                participant.WithdrawArrival();
            }
        }

        Touch(now);
    }

    /// <summary>Moves one participant's read cursor and announces what it cleared.</summary>
    public Result<int> MarkRead(
        UserId userId, ConversationMessageId? throughMessageId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(userId);

        if (ParticipantFor(userId) is not { IsActive: true } participant)
        {
            return Result.Failure<int>(ConversationErrors.NotAParticipant);
        }

        var cleared = participant.ReadThrough(throughMessageId ?? LastMessageId, now);

        Touch(now);

        // Raised even when nothing was cleared. The other party's read receipt is what turns two
        // ticks blue, and a member who opens a conversation they had already read still needs the
        // sender told — the sender may have been offline the first time.
        RaiseDomainEvent(new MessagesReadDomainEvent
        {
            ConversationId = Id,
            ReaderUserId = userId,
            ThroughMessageId = throughMessageId ?? LastMessageId,
            ClearedCount = cleared,
            ReadAt = now
        });

        return Result.Success(cleared);
    }

    public Result AcknowledgeDelivery(
        UserId userId, ConversationMessageId messageId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(messageId);

        if (ParticipantFor(userId) is not { IsActive: true } participant)
        {
            return Result.Failure(ConversationErrors.NotAParticipant);
        }

        participant.AcknowledgeDelivery(messageId, now);
        Touch(now);

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════
    // Per-participant state
    // ════════════════════════════════════════════════════════════════════════════════════════════

    public Result Archive(UserId userId, DateTimeOffset now)
    {
        if (ParticipantFor(userId) is not { IsActive: true } participant)
        {
            return Result.Failure(ConversationErrors.NotAParticipant);
        }

        participant.Archive(now);
        Touch(now);

        RaiseDomainEvent(new ConversationArchivedDomainEvent
        {
            ConversationId = Id,
            UserId = userId,
            ArchivedAt = now
        });

        return Result.Success();
    }

    public Result Unarchive(UserId userId, DateTimeOffset now)
    {
        if (ParticipantFor(userId) is not { IsActive: true } participant)
        {
            return Result.Failure(ConversationErrors.NotAParticipant);
        }

        participant.Unarchive(now);
        Touch(now);

        RaiseDomainEvent(new ConversationRestoredDomainEvent
        {
            ConversationId = Id,
            UserId = userId,
            RestoredAt = now
        });

        return Result.Success();
    }

    public Result Mute(UserId userId, DateTimeOffset? until, DateTimeOffset now)
    {
        if (ParticipantFor(userId) is not { IsActive: true } participant)
        {
            return Result.Failure(ConversationErrors.NotAParticipant);
        }

        participant.Mute(until, now);
        Touch(now);

        RaiseDomainEvent(new ConversationMutedDomainEvent
        {
            ConversationId = Id,
            UserId = userId,
            MutedUntil = until,
            IsMuted = true,
            OccurredOn = now
        });

        return Result.Success();
    }

    public Result Unmute(UserId userId, DateTimeOffset now)
    {
        if (ParticipantFor(userId) is not { IsActive: true } participant)
        {
            return Result.Failure(ConversationErrors.NotAParticipant);
        }

        participant.Unmute(now);
        Touch(now);

        RaiseDomainEvent(new ConversationMutedDomainEvent
        {
            ConversationId = Id,
            UserId = userId,
            MutedUntil = null,
            IsMuted = false,
            OccurredOn = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Pins for one member.
    /// <para>
    /// <paramref name="currentPinCount"/> is supplied by the caller because the limit spans
    /// conversations and this aggregate can only see one. The aggregate still enforces it rather
    /// than trusting the handler: an aggregate that accepted any order would let a second code path
    /// create a sixth pin the client refuses to render.
    /// </para>
    /// </summary>
    public Result Pin(UserId userId, int currentPinCount, DateTimeOffset now)
    {
        if (ParticipantFor(userId) is not { IsActive: true } participant)
        {
            return Result.Failure(ConversationErrors.NotAParticipant);
        }

        if (participant.IsPinned)
        {
            return Result.Success();
        }

        if (!ConversationParticipant.CanPin(currentPinCount))
        {
            return Result.Failure(ConversationErrors.PinLimitReached);
        }

        participant.Pin(currentPinCount + 1, now);
        Touch(now);

        RaiseDomainEvent(new ConversationPinnedDomainEvent
        {
            ConversationId = Id,
            UserId = userId,
            IsPinned = true,
            OccurredOn = now
        });

        return Result.Success();
    }

    public Result Unpin(UserId userId, DateTimeOffset now)
    {
        if (ParticipantFor(userId) is not { IsActive: true } participant)
        {
            return Result.Failure(ConversationErrors.NotAParticipant);
        }

        participant.Unpin(now);
        Touch(now);

        RaiseDomainEvent(new ConversationPinnedDomainEvent
        {
            ConversationId = Id,
            UserId = userId,
            IsPinned = false,
            OccurredOn = now
        });

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════
    // Infrastructure contracts
    // ════════════════════════════════════════════════════════════════════════════════════════════

    private void Touch(DateTimeOffset now)
    {
        UpdatedAt = now;
        Revision++;
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }
}
