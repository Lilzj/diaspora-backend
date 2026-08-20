namespace Diaspora.Modules.Conversations.Domain.Policies;

/// <summary>
/// The numbers the domain enforces, in one place.
/// <para>
/// <b>Domain limits, not configuration.</b> These are invariants a message or a conversation is
/// valid against, so they are compiled in and asserted by unit tests. Anything a deployment might
/// reasonably want to tune — how fast somebody may send, how long history is kept, whether
/// attachments exist at all — lives in <c>ConversationOptions</c> instead. The line between the two
/// is whether changing it invalidates rows already written: a shorter body limit does, a lower rate
/// limit does not.
/// </para>
/// </summary>
public static class ConversationLimits
{
    /// <summary>
    /// The longest message body.
    /// <para>
    /// Four thousand rather than a database maximum: a conversation is not a document, and a limit
    /// large enough to paste a contract into is a limit that makes every message list render
    /// unpredictably. Long enough that nobody composing a genuine reply meets it.
    /// </para>
    /// </summary>
    public const int MaxMessageLength = 4000;

    /// <summary>
    /// The pin limit the client already enforces.
    /// <para>
    /// <b>Five, because the client says five.</b> <c>conversation-hub.ts</c> refuses the sixth pin
    /// locally and shows "Max 5 pins". A server that permitted six would let a second device create
    /// a state the first device refuses to display, and the member would see a pin vanish on
    /// refresh. Enforced here as well, because a client-side limit is a suggestion.
    /// </para>
    /// </summary>
    public const int MaxPinnedConversations = 5;

    /// <summary>
    /// How many people one conversation holds.
    /// <para>
    /// Group conversations beyond this are explicitly out of scope for this phase. The number is
    /// eight rather than two so the model does not have to change to support them — every read path,
    /// the unread bookkeeping and the realtime fan-out are already written for N participants, and a
    /// limit of two would have hard-coded the assumption in a hundred places.
    /// </para>
    /// </summary>
    public const int MaxParticipants = 8;

    /// <summary>How long after sending a message may still be edited.</summary>
    public static readonly TimeSpan EditWindow = TimeSpan.FromMinutes(15);

    /// <summary>The longest a mute may run before "forever" is the honest word for it.</summary>
    public static readonly TimeSpan MaxMuteDuration = TimeSpan.FromDays(7);

    /// <summary>The page size a caller gets when they ask for none.</summary>
    public const int DefaultPageSize = 30;

    /// <summary>
    /// The largest page any caller can ask for.
    /// <para>
    /// A conversation with four thousand messages is ordinary, and a client that asked for all of
    /// them would hold the connection open long enough to look broken. The cursor exists so nobody
    /// needs to.
    /// </para>
    /// </summary>
    public const int MaxPageSize = 100;

    /// <summary>The longest block reason a member may record.</summary>
    public const int MaxBlockReasonLength = 280;
}
