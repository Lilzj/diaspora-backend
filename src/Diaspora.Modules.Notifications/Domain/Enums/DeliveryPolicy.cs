namespace Diaspora.Modules.Notifications.Domain.Enums;

/// <summary>
/// How a rule wants its message delivered, before preferences get a say.
/// <para>
/// The rule proposes and the preference disposes: a rule marked <see cref="Immediate"/> still ends
/// up <see cref="Digest"/> if the member asked for digests on that category, and still ends up
/// suppressed if they muted it — unless the priority is Critical, which no preference can override.
/// The resolution order is written once, in the delivery policy resolver, and nowhere else.
/// </para>
/// </summary>
public enum DeliveryPolicy
{
    /// <summary>Send now, on every channel the member allows.</summary>
    Immediate = 0,

    /// <summary>Hold for a time the rule computes — "the morning of", "an hour before".</summary>
    Scheduled = 1,

    /// <summary>Fold into the member's next digest rather than sending on its own.</summary>
    Digest = 2,

    /// <summary>Record in the inbox and send nowhere. The rule's own mute.</summary>
    Suppressed = 3,

    /// <summary>
    /// Reserved for escalation — retry on a wider set of channels when the member has not responded
    /// within a window. No implementation ships; the value exists so the rule catalogue and the
    /// delivery table do not need migrating when it does.
    /// </summary>
    Escalated = 4
}
