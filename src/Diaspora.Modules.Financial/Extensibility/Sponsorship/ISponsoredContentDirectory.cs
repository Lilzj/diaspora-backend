namespace Diaspora.Modules.Financial.Extensibility.Sponsorship;

/// <summary>
/// What a member has paid to promote, read from B15 by a module outside it.
/// <para>
/// <b>Why a seam.</b> The Creator Platform serves <c>GET /creators/me/sponsored</c> — a creator's
/// paid placements over their own content — and §20 of its brief forbids a second monetization
/// system. So the facts stay B15's: placements bought through the catalogue, and advertising
/// campaigns that point at a piece of content. This interface reads both for one member and says
/// nothing about what the content is called; the content's own module answers that.
/// </para>
/// <para>
/// <b>Read-only.</b> Renewing, pausing and paying stay on B15's own routes.
/// </para>
/// </summary>
public interface ISponsoredContentDirectory
{
    /// <summary>Everything the member bought or is running, newest first. At most <paramref name="take"/> of each kind.</summary>
    Task<IReadOnlyList<SponsoredContentSnapshot>> ListForMemberAsync(
        Guid memberUserId, int take, CancellationToken cancellationToken = default);
}

/// <summary>
/// One paid promotion of one subject.
/// </summary>
/// <param name="PlacementId">The B15 placement, for a catalogue purchase. Null for a campaign.</param>
/// <param name="CampaignId">The advertising campaign, for a campaign. Null for a placement.</param>
/// <param name="SubjectType">The monetization subject type name — <c>Guide</c>, <c>Job</c>, <c>MarketplaceListing</c>…</param>
/// <param name="SubjectId">The promoted subject's identifier.</param>
/// <param name="ProductCode">The catalogue product that priced it, when there is one.</param>
/// <param name="PackageLabel">That product's catalogue name.</param>
/// <param name="State">
/// The client's sponsored-content vocabulary: <c>Draft</c>, <c>PendingPayment</c>,
/// <c>PendingApproval</c>, <c>Approved</c>, <c>Scheduled</c>, <c>Active</c>, <c>Paused</c>,
/// <c>Expired</c>, <c>Rejected</c>, <c>Cancelled</c>, <c>Completed</c>, and B15's own
/// <c>Refunded</c> and <c>Failed</c> where nothing in that list is true.
/// </param>
/// <param name="RejectionReason">The reviewer's note, only when a campaign was rejected.</param>
/// <param name="AmountPaid">What was actually captured. Null until a payment completes.</param>
/// <param name="StartsAt">When the promotion starts or started.</param>
/// <param name="EndsAt">When it ends, when it has an end.</param>
/// <param name="IsRenewable">Whether B15 would renew it from its renew route.</param>
/// <param name="CampaignTitle">A campaign's own headline or name. Null for a placement.</param>
/// <param name="Performance">Delivery totals, only when B15 recorded any.</param>
/// <param name="CreatedAt">When it was bought or drafted. The list's order.</param>
public sealed record SponsoredContentSnapshot(
    Guid? PlacementId,
    Guid? CampaignId,
    string SubjectType,
    Guid SubjectId,
    string? ProductCode,
    string? PackageLabel,
    string State,
    string? RejectionReason,
    SponsoredAmount? AmountPaid,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    bool IsRenewable,
    string? CampaignTitle,
    SponsoredPerformance? Performance,
    DateTimeOffset CreatedAt);

/// <summary>An amount in minor units, exactly as B15 stored it.</summary>
public sealed record SponsoredAmount(long MinorUnits, string Currency, int Exponent);

/// <summary>Summed from B15's daily delivery rows.</summary>
public sealed record SponsoredPerformance(long Impressions, long Clicks, long Conversions);
