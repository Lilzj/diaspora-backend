using Diaspora.Modules.Financial.Domain.Advertising;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>
/// EF Core repository for advertising campaigns.
/// <para>
/// <b>Every read includes the placements and the audience.</b> Both are owned, both are always
/// wanted — a campaign row without its audience is a targeting screen with nothing on it — and EF
/// loads owned types with the parent, so the alternative is not a cheaper query, it is a broken one.
/// </para>
/// </summary>
public sealed class AdCampaignRepository : IAdCampaignRepository
{
    private readonly DiasporaDbContext _dbContext;

    public AdCampaignRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<AdCampaign> Campaigns => _dbContext.Set<AdCampaign>();

    public void Add(AdCampaign campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        Campaigns.Add(campaign);
    }

    public async Task<AdCampaign?> FindAsync(
        AdCampaignId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Campaigns.FirstOrDefaultAsync(
            campaign => campaign.Id == id, cancellationToken);
    }

    public async Task<AdCampaign?> FindForOwnerAsync(
        AdCampaignId id, Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        // The ownership check is in the query rather than after it, so a handler cannot load a
        // campaign it may not see and then forget to compare. A campaign somebody else owns
        // answers exactly as one that does not exist.
        return await Campaigns.FirstOrDefaultAsync(
            campaign => campaign.Id == id && campaign.OwnerUserId == ownerUserId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<AdCampaign>> ListForOwnerAsync(
        Guid ownerUserId,
        IReadOnlyCollection<AdCampaignStatus>? statuses = null,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var query = Campaigns
            .AsNoTracking()
            .Where(campaign => campaign.OwnerUserId == ownerUserId);

        if (statuses is { Count: > 0 })
        {
            var wanted = statuses.ToList();
            query = query.Where(campaign => wanted.Contains(campaign.Status));
        }

        return await query
            .OrderByDescending(campaign => campaign.CreatedAt)
            .ThenBy(campaign => campaign.Id)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdCampaign>> ListAwaitingReviewAsync(
        int take = 50, CancellationToken cancellationToken = default) =>
        await Campaigns
            .AsNoTracking()
            .Where(campaign =>
                campaign.Status == AdCampaignStatus.Submitted
                || campaign.Status == AdCampaignStatus.UnderReview)

            // Oldest first. A queue ordered newest-first leaves the campaign that has waited
            // longest waiting longer every time another arrives.
            .OrderBy(campaign => campaign.CreatedAt)
            .ThenBy(campaign => campaign.Id)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);

    public async Task<AdCampaign?> FindDeliveringForSubjectAsync(
        MonetizationSubjectType subjectType,
        Guid subjectId,
        CancellationToken cancellationToken = default) =>

        // Active only. A paused or draft campaign over the same subject is not delivering, and
        // counting an impression against it would show an advertiser numbers for a campaign they
        // stopped.
        await Campaigns
            .AsNoTracking()
            .FirstOrDefaultAsync(
                campaign => campaign.Status == AdCampaignStatus.Active
                    && campaign.Subject != null
                    && campaign.Subject.SubjectType == subjectType
                    && campaign.Subject.SubjectId == subjectId,
                cancellationToken);

    public async Task<IReadOnlyList<AdCampaign>> ListForDeliverySweepAsync(
        DateTimeOffset asOf, int take = 200, CancellationToken cancellationToken = default) =>
        await Campaigns
            .Where(campaign =>

                // Due to start: approved or scheduled, and the window has opened.
                ((campaign.Status == AdCampaignStatus.Approved
                        || campaign.Status == AdCampaignStatus.Scheduled)
                    && campaign.Schedule.StartsAt <= asOf)

                // Or over: running or paused, and the window has closed. A paused campaign is
                // included on purpose — its window closes whether or not it is delivering, and
                // leaving it Paused for ever would leave its budget unsettled.
                || ((campaign.Status == AdCampaignStatus.Active
                        || campaign.Status == AdCampaignStatus.Paused)
                    && campaign.Schedule.EndsAt != null
                    && campaign.Schedule.EndsAt <= asOf))
            .OrderBy(campaign => campaign.Schedule.StartsAt)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);
}
