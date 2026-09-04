using Diaspora.Modules.Financial.Domain.Enums;
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
    /// The campaign currently delivering over a subject, if there is one.
    /// <para>
    /// <b>Internal, and deliberately not reachable from any route.</b> Every other read here is
    /// keyed by owner, because an advertiser is owed their own numbers and nobody is owed "who is
    /// promoting this listing, and how is it going" — which is what a subject-keyed read answers.
    /// This exists for one caller: the consumer that counts an impression when B14 reports having
    /// served a promoted subject, which has a subject and needs a campaign.
    /// </para>
    /// <para>
    /// Returns null where no campaign is above the promotion. That is an ordinary case rather than
    /// an error: a placement bought before campaigns existed is a real promotion with nothing to
    /// attribute it to.
    /// </para>
    /// </summary>
    Task<AdCampaign?> FindDeliveringForSubjectAsync(
        MonetizationSubjectType subjectType,
        Guid subjectId,
        CancellationToken cancellationToken = default);

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
