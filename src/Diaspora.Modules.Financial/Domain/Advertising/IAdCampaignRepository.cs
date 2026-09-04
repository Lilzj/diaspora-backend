using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Advertising;

/// <summary>
/// Reads and writes advertising campaigns.
/// <para>
/// <b>Every read takes the owner, except the two that are staff reads and say so.</b> A campaign is
/// a commercial record — budget, spend, creative, audience — and a repository whose default read
/// was "by id" would make it one missing ownership check away from letting an advertiser read a
/// competitor's targeting and spend. <see cref="FindForOwnerAsync"/> is the ordinary door;
/// <see cref="FindAsync"/> exists for the review queue and the sweeps, which legitimately act
/// across owners.
/// </para>
/// </summary>
public interface IAdCampaignRepository
{
    void Add(AdCampaign campaign);

    /// <summary>
    /// Reads by id, without an ownership check.
    /// <para>
    /// For staff and for background work. A request handler acting for an advertiser should use
    /// <see cref="FindForOwnerAsync"/> instead, so the check cannot be the thing somebody forgets.
    /// </para>
    /// </summary>
    Task<AdCampaign?> FindAsync(AdCampaignId id, CancellationToken cancellationToken = default);

    /// <summary>Reads a campaign the caller owns. Answers null for one they do not.</summary>
    Task<AdCampaign?> FindForOwnerAsync(
        AdCampaignId id, Guid ownerUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The advertiser's own campaigns, newest first.
    /// <para>
    /// <paramref name="statuses"/> empty means every state, terminal ones included: a completed
    /// campaign is what an advertiser goes back to when they want to run it again, and a dashboard
    /// that hid it would hide their own history.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<AdCampaign>> ListForOwnerAsync(
        Guid ownerUserId,
        IReadOnlyCollection<AdCampaignStatus>? statuses = null,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The review queue, oldest first.
    /// <para>
    /// Oldest first because it is a queue somebody works through, and newest-first would leave the
    /// campaign that has waited longest waiting longer every time a new one arrives.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<AdCampaign>> ListAwaitingReviewAsync(
        int take = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Campaigns that should now be running but are not, and ones whose window has closed.
    /// <para>
    /// The sweep's two questions in one read, because they are answered from the same index and a
    /// sweep that made two round trips would do it once a minute for ever.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<AdCampaign>> ListForDeliverySweepAsync(
        DateTimeOffset asOf, int take = 200, CancellationToken cancellationToken = default);
}
