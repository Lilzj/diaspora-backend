using System.ComponentModel.DataAnnotations;
using Diaspora.Modules.Creator.Domain.Enums;

namespace Diaspora.Modules.Creator.Options;

/// <summary>
/// What share of a payment is the creator's, per source.
/// <para>
/// <b>Configuration rather than code, because this is a business decision and not a technical
/// one.</b> Whether a sponsored listing pays its creator 70% or nothing is a revenue-model
/// question with an owner, and §11 forbids revenue being authoritative client-side data for exactly
/// the same reason it should not be a number somebody picked while writing a consumer. A rate
/// hard-coded here would be a policy invented by whoever typed fastest.
/// </para>
/// <para>
/// <b>The seeded defaults are deliberately unambitious.</b> A donation is recorded at 100%, because
/// a donation naming a beneficiary is unambiguously theirs — the platform is not taking a cut it has
/// never agreed. Every other source seeds at **zero**: the earning is still recorded, with its gross
/// intact, so the dashboard can show that something was attributed and carried no share yet. Zero is
/// an honest "nobody has set this" and reads as such; a made-up 70% would read as policy.
/// </para>
/// <para>
/// <b>Basis points, not decimals.</b> 10,000 is 100%. A percentage as a <c>decimal</c> invites
/// rounding drift on a figure that becomes somebody's income, and integer arithmetic on minor units
/// keeps the share exactly derivable from the gross.
/// </para>
/// </summary>
public sealed class CreatorRevenueShareOptions
{
    public const string SectionName = "Creator:RevenueShare";

    /// <summary>A donation naming this creator. Theirs in full unless configured otherwise.</summary>
    [Range(0, 10_000)]
    public int DonationBasisPoints { get; set; } = 10_000;

    /// <summary>A purchase against this creator's own subject. Unset until somebody sets it.</summary>
    [Range(0, 10_000)]
    public int SponsorshipBasisPoints { get; set; }

    /// <summary>A subscription share. Unset until somebody sets it.</summary>
    [Range(0, 10_000)]
    public int SubscriptionBasisPoints { get; set; }

    /// <summary>A referral bonus. Unset until somebody sets it.</summary>
    [Range(0, 10_000)]
    public int ReferralBasisPoints { get; set; }

    /// <summary>
    /// How long an earning waits before it can be paid out.
    /// <para>
    /// The refund window, in effect: an earning made available the instant a payment captures can be
    /// paid out and then refunded, leaving the platform to recover money it has already sent.
    /// </para>
    /// </summary>
    [Range(0, 180)]
    public int ClearingDays { get; set; } = 30;

    /// <summary>The rate for one source, in basis points.</summary>
    public int RateFor(CreatorEarningSource source) => source switch
    {
        CreatorEarningSource.Donation => DonationBasisPoints,
        CreatorEarningSource.Sponsorship => SponsorshipBasisPoints,
        CreatorEarningSource.Subscription => SubscriptionBasisPoints,
        CreatorEarningSource.Referral => ReferralBasisPoints,

        // A source with no rate earns nothing rather than everything. A new enum member reaching
        // this arm is a configuration gap, and paying 100% by default would turn that gap into a
        // payout.
        _ => 0,
    };

    /// <summary>
    /// The creator's share of a gross amount.
    /// <para>
    /// Rounds **down**. A half-minor-unit is a fraction of a penny, and rounding it towards the
    /// creator would have the platform paying out marginally more than it took, once per earning,
    /// for ever.
    /// </para>
    /// </summary>
    public long ShareOf(long grossMinorUnits, CreatorEarningSource source) =>
        grossMinorUnits <= 0 ? 0 : grossMinorUnits * RateFor(source) / 10_000;
}
