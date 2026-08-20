using Diaspora.Modules.Conversations.Domain.Enums;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.Policies;
using Diaspora.Modules.Conversations.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Conversations.Domain.Conversations;

/// <summary>
/// One member's side of one conversation.
/// <para>
/// <b>This single row is what the phase brief names as five aggregates — ConversationParticipant,
/// ConversationReadState, ConversationMute, ConversationPin and ConversationArchive — and merging
/// them is a deliberate, defensible departure.</b> Every one of those is a fact about the same pair
/// (member, conversation): is it archived <em>for you</em>, muted <em>for you</em>, pinned
/// <em>for you</em>, how far have <em>you</em> read. Modelling them as five aggregates gives one
/// pair of people five rows, five repositories, five write paths and five chances for a mute to
/// exist for a participant who has already left. It also makes rendering one inbox line a five-way
/// join, which is precisely the N+1 shape this phase is required not to ship.
/// </para>
/// <para>
/// They are still five <em>concepts</em>, and the domain keeps them separable: mute, pin, archive
/// and read each have their own method, their own rule and their own domain event, so a future phase
/// that genuinely needs mute history can extract it without touching a caller. What is shared is the
/// row, not the vocabulary.
/// </para>
/// <para>
/// <b>An owned entity, not an aggregate root.</b> Participation has no life outside its
/// conversation: it is created with one, it is loaded with one, and there is no query in this module
/// that wants a participant without wanting the conversation it belongs to. Owning it also makes
/// "add a participant and append the system message about it" a single transaction by construction.
/// </para>
/// </summary>
public sealed class ConversationParticipant : Entity<ParticipantId>
{
    private ConversationParticipant(
        ParticipantId id,
        UserId userId,
        ParticipantRole role,
        DateTimeOffset joinedAt)
        : base(id)
    {
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
        LastActivityAt = joinedAt;
    }

    private ConversationParticipant() { }

    public UserId UserId { get; private set; } = default!;

    public ParticipantRole Role { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    /// <summary>
    /// When they were removed, or null while they are still in.
    /// <para>
    /// The row survives departure rather than being deleted. A message they sent before leaving is
    /// still in the thread and still needs an author the other party can resolve; deleting the row
    /// would leave those messages attributed to a participant that no longer exists.
    /// </para>
    /// </summary>
    public DateTimeOffset? LeftAt { get; private set; }

    public bool IsActive => LeftAt is null;

    // ── Per-participant state: the four flags the client renders per conversation ───────────────

    public bool IsArchived { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public bool IsMuted { get; private set; }

    /// <summary>Null while muted means indefinitely — which is what the client calls "forever".</summary>
    public DateTimeOffset? MutedUntil { get; private set; }

    public bool IsPinned { get; private set; }

    /// <summary>
    /// Where this sits among the member's pins, 1-based.
    /// <para>
    /// The client sorts pinned conversations by this and falls back to <c>?? 0</c>, so a null while
    /// pinned would silently sort a pin to the top. Assigned when pinning and cleared when
    /// unpinning; gaps after an unpin are tolerated because the client sorts rather than indexes.
    /// </para>
    /// </summary>
    public int? PinOrder { get; private set; }

    // ── Read state ──────────────────────────────────────────────────────────────────────────────

    public ConversationMessageId? LastReadMessageId { get; private set; }

    public DateTimeOffset? LastReadAt { get; private set; }

    /// <summary>
    /// Maintained on append rather than counted on read. See <see cref="ReadCursor"/>.
    /// </summary>
    public int UnreadCount { get; private set; }

    /// <summary>
    /// The last message this member's device acknowledged receiving.
    /// <para>
    /// Distinct from <see cref="LastReadMessageId"/>: a device that received a message has two
    /// ticks, a person who opened it has blue. B13's runtime keeps the two receipt kinds apart for
    /// exactly this reason and this is where the durable half of that distinction lives — a
    /// relayed receipt is lost on reconnect, this is not.
    /// </para>
    /// </summary>
    public ConversationMessageId? LastDeliveredMessageId { get; private set; }

    public DateTimeOffset? LastDeliveredAt { get; private set; }

    public DateTimeOffset LastActivityAt { get; private set; }

    /// <summary>
    /// When this member was last told the conversation had gone quiet — B23.x Wave 12.
    /// <para>
    /// <b>The whole of the repeat suppression, and it is here rather than downstream because it has
    /// to be.</b> The attention sweep publishes a fact; a fact carries its own event id; and a
    /// consumer's idempotency keys on that id. So two publications about one silence are two
    /// notifications no matter what the consumer does, and the only place that can be prevented is
    /// the place that decides to publish.
    /// </para>
    /// <para>
    /// Compared against the conversation's last message rather than against the clock: a notice
    /// issued before the last message was sent is a notice about a different silence, and one issued
    /// after it means this silence has already been reported.
    /// </para>
    /// </summary>
    public DateTimeOffset? InactivityNoticedAt { get; private set; }

    /// <summary>
    /// When this member was last told they had unread messages — B23.x Wave 12.
    /// <para>
    /// A cooldown rather than a comparison with anything, because unread is a standing state rather
    /// than an event: a member who leaves forty messages unread for a fortnight is in the same
    /// condition on day one and day fourteen, and the honest cadence is "not again for a while".
    /// </para>
    /// </summary>
    public DateTimeOffset? UnreadNoticedAt { get; private set; }

    public ReadCursor Cursor => ReadCursor.Create(LastReadMessageId, LastReadAt, UnreadCount);

    /// <summary>
    /// Records that the quiet was reported, so it is not reported again — B23.x Wave 12.
    /// <para>
    /// Internal, like every other mutator here: participation is an owned entity and only the
    /// conversation may move it.
    /// </para>
    /// </summary>
    internal void NoteInactivityReported(DateTimeOffset at) => InactivityNoticedAt = at;

    internal void NoteUnreadReported(DateTimeOffset at) => UnreadNoticedAt = at;

    /// <summary>
    /// Whether this member is owed an inactivity notice about a conversation last spoken in at
    /// <paramref name="lastMessageAt"/>.
    /// <para>
    /// False for somebody who has left, muted or archived it — those are decisions about wanting to
    /// hear less, and an inactivity reminder is hearing more. False for a conversation that has never
    /// had a message: an enquiry nobody opened is not a conversation that went quiet.
    /// </para>
    /// </summary>
    public bool IsOwedInactivityNotice(DateTimeOffset? lastMessageAt, DateTimeOffset now) =>
        IsActive
        && !IsArchived
        && !IsMutedAt(now)
        && lastMessageAt is { } spokenAt
        && (InactivityNoticedAt is not { } noticed || noticed < spokenAt);

    internal static ConversationParticipant Join(
        UserId userId, ParticipantRole role, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return new ConversationParticipant(ParticipantId.New(), userId, role, at);
    }

    internal void Leave(DateTimeOffset at)
    {
        if (LeftAt is not null)
        {
            return;
        }

        LeftAt = at;

        // Pins and unread counts belong to a conversation the member is in. Leaving one that is
        // still pinned would leave a row on their inbox they cannot open, which reads as a bug on a
        // screen and not as a permission decision.
        IsPinned = false;
        PinOrder = null;
        UnreadCount = 0;
    }

    internal void Rejoin(ParticipantRole role, DateTimeOffset at)
    {
        LeftAt = null;
        Role = role;
        LastActivityAt = at;

        // Nothing is restored. A member added back to a conversation is starting again: the read
        // cursor from before they left would mark everything said in their absence as already seen.
        LastReadMessageId = null;
        LastReadAt = null;
        UnreadCount = 0;
    }

    internal void Archive(DateTimeOffset at)
    {
        IsArchived = true;
        ArchivedAt = at;
        LastActivityAt = at;
    }

    internal void Unarchive(DateTimeOffset at)
    {
        IsArchived = false;
        ArchivedAt = null;
        LastActivityAt = at;
    }

    internal void Mute(DateTimeOffset? until, DateTimeOffset at)
    {
        IsMuted = true;
        MutedUntil = until;
        LastActivityAt = at;
    }

    internal void Unmute(DateTimeOffset at)
    {
        IsMuted = false;
        MutedUntil = null;
        LastActivityAt = at;
    }

    /// <summary>
    /// Whether notifications are silenced for this member right now.
    /// <para>
    /// <b>Reads the clock rather than trusting the flag.</b> A 24-hour mute set yesterday leaves
    /// <see cref="IsMuted"/> true until something writes to the row, and nothing necessarily does —
    /// there is no sweep whose job is to unmute, and adding one would be a background job existing
    /// only to make a boolean honest. Every caller that asks "is this muted" asks with a time.
    /// </para>
    /// </summary>
    public bool IsMutedAt(DateTimeOffset now) =>
        IsMuted && (MutedUntil is null || MutedUntil > now);

    internal void Pin(int order, DateTimeOffset at)
    {
        IsPinned = true;
        PinOrder = order < 1 ? 1 : order;
        LastActivityAt = at;
    }

    internal void Unpin(DateTimeOffset at)
    {
        IsPinned = false;
        PinOrder = null;
        LastActivityAt = at;
    }

    /// <summary>
    /// Records that a message arrived for this member.
    /// <para>
    /// Un-archives on purpose. A member who archived a conversation and then receives a new message
    /// in it expects it back in their inbox — that is what archiving means everywhere else, and the
    /// alternative is a message that is delivered, pushed and invisible. Muting is <em>not</em>
    /// cleared by the same event: mute is a statement about notifications, not about filing.
    /// </para>
    /// </summary>
    internal void RecordArrival(DateTimeOffset at)
    {
        UnreadCount++;
        LastActivityAt = at;

        if (IsArchived)
        {
            IsArchived = false;
            ArchivedAt = null;
        }
    }

    /// <summary>Un-counts a message withdrawn before this member read it.</summary>
    internal void WithdrawArrival()
    {
        if (UnreadCount > 0)
        {
            UnreadCount--;
        }
    }

    /// <summary>
    /// Moves the read cursor forward and returns how many were cleared.
    /// <para>
    /// The count is returned rather than announced, because the caller is the aggregate and it is
    /// the aggregate that raises the event — a participant that raised its own domain events would
    /// be an owned entity with a life of its own.
    /// </para>
    /// </summary>
    internal int ReadThrough(ConversationMessageId? messageId, DateTimeOffset at)
    {
        var cleared = UnreadCount;

        if (messageId is not null)
        {
            LastReadMessageId = messageId;
        }

        LastReadAt = at;
        UnreadCount = 0;
        LastActivityAt = at;

        // Reading implies receipt. A device cannot render a message it never received, so a read
        // that left the delivery cursor behind would report a message read but not delivered.
        if (messageId is not null)
        {
            LastDeliveredMessageId = messageId;
            LastDeliveredAt ??= at;
        }

        return cleared;
    }

    internal void AcknowledgeDelivery(ConversationMessageId messageId, DateTimeOffset at)
    {
        LastDeliveredMessageId = messageId;
        LastDeliveredAt = at;
        LastActivityAt = at;
    }

    /// <summary>Whether this member may still be pinned to, given how many they already hold.</summary>
    internal static bool CanPin(int currentPinCount) =>
        currentPinCount < ConversationLimits.MaxPinnedConversations;
}
