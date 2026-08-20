using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Conversations.V1.Events;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// The two facts a conversation produces by *not* changing — B23.x Wave 12.
//
// Every other contract in this module reports something somebody did. These two report the absence
// of it: a thread that has gone quiet, and messages that have been sitting unread. They exist
// because the Phase 18 frontend offers `conversation.inactive` and `conversation.unreadReminder`,
// and Wave 11 refused to build either from this module's state — correctly, and in these words:
// "deriving inactivity would mean this module reading another module's message state. Conversations
// owns that and must publish the fact."
//
// So this module publishes it. What the automation platform gets is a timestamp, a count and two
// identifiers. No message, no sender name, no thread preview — nothing this module would not put on
// any other contract, and for the reason stated at the top of the sibling file: what somebody typed
// to their landlord belongs in one table with one access rule.
//
// **Neither is emitted more than once per quiet period.** A sweep that republished every cycle would
// turn one silent conversation into a notification every few minutes, and the automation platform's
// idempotency cannot save it — each publication carries its own event id, so each would be a new
// fact. The suppression is a marker on the participant row, in the module that owns it.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A conversation somebody is part of has had no reply for a while — B23.x Wave 12.
/// <para>
/// <b>Named for the participant who is waiting, not for the conversation.</b> One quiet thread
/// produces one of these per participant who is owed a reply, and none at all for the person who sent
/// the last message: reminding somebody that nobody has answered <em>them</em> is a reminder; telling
/// the person who spoke last that it went quiet is telling them what they already know.
/// </para>
/// <para>
/// <b>A muted or archived participant produces nothing.</b> Mute and archive are conversation state
/// and no downstream consumer has heard of them, so the decision is made here — the same rule
/// <c>ConversationMessageSentIntegrationEvent</c> already applies to its recipient list.
/// </para>
/// </summary>
public sealed record ConversationInactivityDetectedIntegrationEvent : IntegrationEvent
{
    public required Guid ConversationId { get; init; }

    /// <summary>The member who is waiting on a reply. One event per such member.</summary>
    public required Guid ParticipantUserId { get; init; }

    /// <summary>A catalogue key — <c>MarketplaceListing</c>, <c>HousingListing</c>, and so on.</summary>
    public required string ContextType { get; init; }

    /// <summary>Whatever the owning module calls its entity. Opaque here.</summary>
    public required string EntityId { get; init; }

    /// <summary>What the conversation is about, as snapshotted. Public wherever the entity is.</summary>
    public required string ContextTitle { get; init; }

    /// <summary>When the last message was sent. Null on a conversation that never had one.</summary>
    public DateTimeOffset? LastMessageAt { get; init; }

    /// <summary>How long it has been quiet, in whole hours. What a condition compares.</summary>
    public required int QuietHours { get; init; }

    /// <summary>
    /// True when the member waiting sent the last message themselves.
    /// <para>
    /// Always false as this module publishes it today — the sweep skips that case — and carried so a
    /// consumer branching on "am I owed a reply" reads a field rather than inferring it from an
    /// absence, and so a later change of policy here is visible rather than silent.
    /// </para>
    /// </summary>
    public required bool IsAwaitingReply { get; init; }
}

/// <summary>
/// A member has messages that have been sitting unread — B23.x Wave 12.
/// <para>
/// <b>A count and an age, and nothing else.</b> The frontend's own wording for the trigger is
/// "nothing about their contents is read", and this contract is how that is kept structurally rather
/// than by anybody remembering it: there is no field a body could go in.
/// </para>
/// <para>
/// <b>Aggregated per member, not per conversation.</b> A member with nine unread threads gets one of
/// these, because the trigger is "I have unread messages" and nine events would be nine
/// notifications. The <c>countAbove</c> condition the frontend offers on this trigger compares
/// <see cref="UnreadCount"/>, which is why it is a total rather than a per-thread figure.
/// </para>
/// </summary>
public sealed record ConversationUnreadThresholdReachedIntegrationEvent : IntegrationEvent
{
    public required Guid MemberUserId { get; init; }

    /// <summary>How many messages are unread across every conversation this member is in.</summary>
    public required int UnreadCount { get; init; }

    /// <summary>How many conversations they are spread across.</summary>
    public required int ConversationCount { get; init; }

    /// <summary>How long the oldest of them has been waiting, in whole hours.</summary>
    public required int OldestUnreadHours { get; init; }
}
