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
    Refused = 6,

    /// <summary>
    /// The callback was right and the platform's own capture was not.
    /// <para>
    /// <b>Money is held at the provider and this platform does not have it.</b> An authorisation
    /// callback authorises the intent and then asks the provider to take the money; when that second
    /// call is refused the authorisation is real, the capture never happened, and the payment stops
    /// there — <c>Authorized</c> for ever. Nothing expires it: <c>ListExpiredAsync</c> covers
    /// <c>Draft</c> and <c>Pending</c> only, so no sweep cancels it and no sweep retries it. The
    /// member's money stays reserved until the provider's own authorisation lapses, and the platform
    /// never learns that it did.
    /// </para>
    /// <para>
    /// It was recorded as <see cref="Refused"/>, which reads as "the platform rejected this
    /// callback" and files it beside replays, wrong currencies and transitions the aggregate does
    /// not allow. Those are all cases where nothing was owed and nothing was taken. This one is the
    /// opposite, and it is the only outcome here that means somebody's money is somewhere neither
    /// they nor this platform can reach.
    /// </para>
    /// </summary>
    CaptureFailed = 7
}
