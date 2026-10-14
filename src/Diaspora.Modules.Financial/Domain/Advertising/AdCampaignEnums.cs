namespace Diaspora.Modules.Financial.Domain.Advertising;

/// <summary>
/// What an advertiser is buying.
/// <para>
/// <b>The objective is not the same as the placement kind, and keeping them apart is the point.</b>
/// A placement kind says <em>where</em> a subject appears — a featured rail, a boosted search slot.
/// An objective says what the advertiser wants to happen, and it is what the performance read counts
/// a "result" as. Two campaigns can buy the same placement kind and be succeeding or failing at
/// different things.
/// </para>
/// </summary>
public enum AdCampaignObjective
{
    /// <summary>Be seen. The result is an impression, and cost-per-result is cost per impression.</summary>
    Awareness = 1,

    /// <summary>Be visited. The result is a click through to the subject.</summary>
    Traffic = 2,

    /// <summary>Be acted on — saved, shared, followed, replied to.</summary>
    Engagement = 3,

    /// <summary>Be contacted. The result is an enquiry the subject's own vertical records.</summary>
    Leads = 4,
}

/// <summary>
/// Where a campaign is in its life.
/// <para>
/// <b>Twelve states, and the register named all twelve.</b> They are not decoration: the client
/// renders a different screen for each, and the four that look alike from a distance are the ones
/// that matter most —
/// </para>
/// <list type="bullet">
/// <item><see cref="PendingPayment"/> is the advertiser's move: the campaign is submitted and the
/// money has not arrived.</item>
/// <item><see cref="UnderReview"/> is the platform's move: the money arrived and somebody is
/// reading the creative.</item>
/// <item><see cref="Rejected"/> is the platform saying no, with a reason and an appeal.</item>
/// <item><see cref="Failed"/> is nobody saying anything — a payment that did not capture, a
/// settlement that errored. It is not a decision and must not read as one.</item>
/// </list>
/// <para>
/// <b><see cref="Completed"/> and <see cref="Cancelled"/> are both endings and stay distinct.</b>
/// A campaign that ran its schedule out spent what it was going to spend; a cancelled one has
/// unspent budget to settle, and the advertiser is owed an answer about it.
/// </para>
/// </summary>
public enum AdCampaignStatus
{
    /// <summary>Being written. Editable, and visible to nobody but its owner.</summary>
    Draft = 1,

    /// <summary>Submitted, awaiting funds. The advertiser has a payment to complete.</summary>
    PendingPayment = 2,

    /// <summary>Funded and queued for review. Nothing is editable from here on.</summary>
    Submitted = 3,

    /// <summary>Somebody is reading it.</summary>
    UnderReview = 4,

    /// <summary>Approved and waiting for its start date.</summary>
    Approved = 5,

    /// <summary>Approved, and its placements are booked.</summary>
    Scheduled = 6,

    /// <summary>Running. The only state in which spend moves.</summary>
    Active = 7,

    /// <summary>Stopped by the advertiser or by staff. Reversible; spend is frozen.</summary>
    Paused = 8,

    /// <summary>Ran to the end of its schedule or exhausted its budget.</summary>
    Completed = 9,

    /// <summary>Refused, with a reason the advertiser is shown and may appeal.</summary>
    Rejected = 10,

    /// <summary>Stopped for good by the advertiser. Unspent budget is settled.</summary>
    Cancelled = 11,

    /// <summary>
    /// The platform could not carry it — a payment that did not capture, a settlement that errored.
    /// Not a decision, and never rendered as one.
    /// </summary>
    Failed = 12,
}

/// <summary>
/// The outcome of a review.
/// <para>
/// Separate from <see cref="AdCampaignStatus"/> because a campaign carries its review decision after
/// the status has moved on. An approved campaign that later completes still has to be able to say
/// who approved it and when.
/// </para>
/// </summary>
public enum AdReviewDecision
{
    /// <summary>Nobody has decided. The value a campaign carries before it is reviewed.</summary>
    Pending = 0,

    Approved = 1,

    Rejected = 2,
}

/// <summary>
/// Who a campaign is aimed at, as a dimension name.
/// <para>
/// <b>This enum is the honest answer to "which dimensions does the delivery engine actually
/// honour".</b> The register asks for that statement by name, because the client's builder collects
/// seven dimensions and the wire carried one. Every member here is a dimension the campaign stores
/// <em>and</em> the estimate reads; a dimension the engine cannot honour does not get a member.
/// </para>
/// </summary>
public enum AdTargetingDimension
{
    /// <summary>ISO 3166-1 alpha-2. The one dimension that existed before this phase.</summary>
    Country = 1,

    /// <summary>A community id, from B18's directory.</summary>
    Community = 2,

    /// <summary>Legacy: a catalogue neighbourhood id. Not stored since 14 Oct 2026; see <see cref="Town"/>.</summary>
    Neighbourhood = 3,

    /// <summary>An interest key.</summary>
    Interest = 4,

    /// <summary>A vertical's category key — a job category, a marketplace category.</summary>
    Category = 5,

    /// <summary>A town by name (14 Oct 2026), replacing catalogue neighbourhoods.</summary>
    Town = 6,
}
