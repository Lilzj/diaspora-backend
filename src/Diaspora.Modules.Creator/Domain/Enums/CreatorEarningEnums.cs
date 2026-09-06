namespace Diaspora.Modules.Creator.Domain.Enums;

/// <summary>
/// Where an earning came from.
/// <para>
/// The register's complaint, in one word per value: B15 records that £12.00 moved and nothing
/// records that it was a sponsorship share rather than a refund. A transaction has a direction and
/// an amount; this says what it <em>meant</em>.
/// </para>
/// <para>
/// Every value here corresponds to an integration event the platform already publishes. Nothing is
/// aspirational: a source with no producer would be a filter on a revenue screen that can never
/// return a row, which is the same defect as a targeting dimension nothing honours.
/// </para>
/// </summary>
public enum CreatorEarningSource
{
    /// <summary>A donation whose beneficiary is this creator. `DonationCompletedIntegrationEvent`.</summary>
    Donation = 1,

    /// <summary>
    /// A share of something bought against this creator's own subject — a promoted listing, a
    /// featured guide. `MonetizationProductPurchasedIntegrationEvent`.
    /// <para>
    /// Named for the client's word rather than the shorter one: the revenue surface has called this
    /// `SponsoredContent` since before the backend recorded any earning, and one vocabulary with two
    /// spellings is a mapping layer nobody needs.
    /// </para>
    /// </summary>
    SponsoredContent = 2,

    /// <summary>A share of a subscription attributed to this creator.</summary>
    Subscription = 3,

    /// <summary>A referral bonus the programme awards, not money a member paid.</summary>
    Referral = 4,
}

/// <summary>
/// Where an earning has got to.
/// <para>
/// <b>The states the register asks for, and they are not decoration.</b> A creator reading
/// "£120 earned" wants to know how much of it they can actually take out, and the difference between
/// <see cref="Pending"/> and <see cref="Available"/> is the whole answer. Collapsing them — showing
/// a lifetime total as though it were a balance — is the failure this enum exists to prevent.
/// </para>
/// <para>
/// <b>Nothing here moves money.</b> These describe an earning's own lifecycle; the wallet is B15's
/// and moves when a payout captures. An earning marked <see cref="Paid"/> is a statement that a
/// payout referenced it, not a statement that a balance changed.
/// </para>
/// </summary>
public enum CreatorEarningState
{
    /// <summary>Recorded, and not yet the creator's to take. A refund window is usually why.</summary>
    Pending = 1,

    /// <summary>Cleared, and countable towards a payout.</summary>
    Available = 2,

    /// <summary>Named on a payout that has not settled.</summary>
    Processing = 3,

    /// <summary>Named on a payout that settled.</summary>
    Paid = 4,

    /// <summary>A payout naming it failed. The earning returns to <see cref="Available"/> on retry.</summary>
    Failed = 5,

    /// <summary>The originating payment was reversed after the earning was recorded.</summary>
    Reversed = 6,

    /// <summary>The originating payment was refunded.</summary>
    Refunded = 7,

    /// <summary>Withdrawn before it cleared — a donation cancelled, a purchase voided.</summary>
    Cancelled = 8,
}
