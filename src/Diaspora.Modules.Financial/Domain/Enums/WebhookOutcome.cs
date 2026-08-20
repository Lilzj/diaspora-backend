namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// What the platform did with a provider callback.
/// <para>
/// <b>Every outcome is recorded, including the ones that did nothing.</b> "We received a callback we
/// could not verify" and "we received no callback" look identical in a system that only stores the
/// ones it acted on, and they are the two most different answers to "why was this member not
/// charged".
/// </para>
/// </summary>
public enum WebhookOutcome
{
    /// <summary>The signature did not verify. Nothing downstream ran.</summary>
    Unverified = 1,

    /// <summary>Verified, and about something this platform does not track.</summary>
    Ignored = 2,

    /// <summary>Verified and understood, but no payment matched the reference.</summary>
    Unmatched = 3,

    /// <summary>Already processed. The stored outcome was replayed rather than applied again.</summary>
    Duplicate = 4,

    /// <summary>Applied to the payment.</summary>
    Applied = 5,

    /// <summary>
    /// Verified and refused by the platform — a state transition the aggregate does not allow, an
    /// amount larger than the intent, or a currency that is not the intent's.
    /// </summary>
    Refused = 6
}
