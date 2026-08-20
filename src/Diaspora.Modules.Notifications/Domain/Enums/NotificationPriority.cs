namespace Diaspora.Modules.Notifications.Domain.Enums;

/// <summary>
/// How urgently a message must reach the member. The single most consequential value in the module.
/// <para>
/// <b>Priority is a delivery policy, not a decoration.</b> It decides whether quiet hours are
/// honoured, whether a digest may swallow the message, how many channels are used and how hard a
/// failed delivery is retried. Everything else about a notification can be wrong and recoverable;
/// getting this wrong either wakes somebody at 3am about a reaction to a photograph, or holds a
/// fraud alert until morning.
/// </para>
/// <para>
/// An enum rather than a catalogue row, deliberately. Categories are configurable because a
/// deployment genuinely adds new subject areas; the urgency vocabulary is a fixed set the delivery
/// policies are written against, and a sixth value would need code to mean anything.
/// </para>
/// </summary>
public enum NotificationPriority
{
    /// <summary>
    /// Overrides quiet hours, do-not-disturb and every category mute. Reserved for security and
    /// safety: a sign-in from a new country, an account suspension, a fraud alert. A member cannot
    /// switch these off, and the preference screen says so rather than pretending they can.
    /// </summary>
    Critical = 0,

    /// <summary>
    /// Delivered immediately on every allowed channel, but honours quiet hours by deferring to the
    /// end of them. An interview time, an offer, a viewing confirmation.
    /// </summary>
    High = 1,

    /// <summary>The default. Immediate in-app, batched into a digest on other channels if the member asked for one.</summary>
    Normal = 2,

    /// <summary>Digest-first. A reaction, a follow, a new guide in a category the member reads.</summary>
    Low = 3,

    /// <summary>
    /// Recorded in the inbox and never pushed anywhere. Present so a message can exist for the
    /// audit trail and the unread badge without interrupting anybody — which is the honest way to
    /// express "we logged this" rather than suppressing it and losing the record.
    /// </summary>
    Silent = 4
}
