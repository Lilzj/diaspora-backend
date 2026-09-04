using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Advertising;

/// <summary>
/// Reads and writes delivery counters.
/// <para>
/// <b>Every read is by campaign, and there is no read by subject.</b> An advertiser is owed their
/// own campaign's numbers; nobody is owed "how is everybody's promotion of this listing doing",
/// which is what a subject-keyed read would answer. The absence is the access control.
/// </para>
/// </summary>
public interface IAdPerformanceRepository
{
    void Add(AdPerformanceDay row);

    /// <summary>
    /// The row for one campaign, placement, surface and day, if it exists.
    /// <para>
    /// The recorder's first call on every impression. Indexed on exactly this tuple, because it
    /// runs once per served promotion and a scan there would be a scan on delivery.
    /// </para>
    /// </summary>
    Task<AdPerformanceDay?> FindAsync(
        AdCampaignId campaignId,
        PlacementId? placementId,
        string surface,
        DateOnly day,
        CancellationToken cancellationToken = default);

    /// <summary>Every day a campaign delivered on, within a window.</summary>
    Task<IReadOnlyList<AdPerformanceDay>> ListForCampaignAsync(
        AdCampaignId campaignId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every day one placement delivered on.
    /// <para>
    /// Separate from the campaign read because the register asks for both, and because a placement
    /// can be shared across surfaces — summing it from a campaign read on the device would mean the
    /// client doing the grouping.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<AdPerformanceDay>> ListForPlacementAsync(
        PlacementId placementId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Everything an advertiser's campaigns delivered, for the dashboard rollup.
    /// <para>
    /// Takes the campaign ids rather than the owner, because ownership is the campaign
    /// repository's question and answering it twice in two places is how the two come to disagree.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<AdPerformanceDay>> ListForCampaignsAsync(
        IReadOnlyCollection<AdCampaignId> campaignIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
