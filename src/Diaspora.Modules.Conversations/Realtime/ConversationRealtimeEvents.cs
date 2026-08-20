namespace Diaspora.Modules.Conversations.Realtime;

/// <summary>
/// The event names this module adds to the hub, composed in one place.
/// <para>
/// <b>Declared here rather than appended to B13's <c>RealtimeEvents</c>, and that is a boundary
/// decision rather than a filing one.</b> <c>RealtimeEvents</c> is the Communication Platform's
/// published contract for traffic the runtime itself produces; these are conversation facts that
/// only this module can raise. Putting them there would make the runtime's contract grow every time
/// a vertical wanted to push something, which is exactly the coupling <c>IRealtimeNotifier</c>
/// exists to prevent — the notifier takes any name, so a vertical owning its own names costs
/// nothing and keeps B13's list about B13.
/// </para>
/// <para>
/// <b><c>MessageReceived</c> is not here.</b> It is B13's constant, declared by the runtime and
/// documented as "raised by whichever module owns conversations" — so this module raises that one by
/// its published name, which is what lets a client that was written against the runtime's contract
/// receive messages with no change.
/// </para>
/// <para>
/// These are matched by string at the client. A rename breaks every shipped mobile build and no
/// compiler will say so; they are constants so a rename is one edit, and frozen so it is a decision.
/// </para>
/// </summary>
public static class ConversationRealtimeEvents
{
    /// <summary>A message was edited in place. Payload: <c>ConversationMessagePayload</c>.</summary>
    public const string MessageEdited = "MessageEdited";

    /// <summary>A message was withdrawn. Payload: <c>ConversationMessagePayload</c>, body null.</summary>
    public const string MessageDeleted = "MessageDeleted";

    /// <summary>
    /// A conversation was created, or the caller was added to one.
    /// <para>
    /// Sent to each participant's own user group rather than to the conversation group: nobody has
    /// joined the conversation group yet, because they do not know the conversation exists.
    /// </para>
    /// </summary>
    public const string ConversationCreated = "ConversationCreated";

    /// <summary>
    /// A per-participant flag moved — archived, muted or pinned.
    /// <para>
    /// One event rather than three, because all three are the same fact from the client's point of
    /// view: this conversation's row in your inbox changed and only for you. Three names would be
    /// three subscriptions doing one thing.
    /// </para>
    /// </summary>
    public const string ConversationStateChanged = "ConversationStateChanged";

    /// <summary>A conversation ended. Payload: <c>ConversationClosedPayload</c>.</summary>
    public const string ConversationClosed = "ConversationClosed";

    /// <summary>
    /// The member's total unread moved.
    /// <para>
    /// Distinct from B13's <c>InboxCountersChanged</c>, which is the notification badge. Two
    /// different numbers on two different tabs; collapsing them would make a read receipt clear a
    /// notification count.
    /// </para>
    /// </summary>
    public const string ConversationUnreadChanged = "ConversationUnreadChanged";
}
