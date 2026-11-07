using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Memberships;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for the membership payment binding.</summary>
public sealed class MembershipPaymentBindingRepository : IMembershipPaymentBindingRepository
{
    private readonly DiasporaDbContext _dbContext;

    public MembershipPaymentBindingRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<MembershipPaymentBinding> Bindings => _dbContext.Set<MembershipPaymentBinding>();

    public void Add(MembershipPaymentBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        Bindings.Add(binding);
    }

    public void Update(MembershipPaymentBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        FinancialAggregateUpdater.MarkDirty(_dbContext, binding);
    }

    public void Forget(MembershipPaymentBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        _dbContext.Entry(binding).State = EntityState.Detached;
    }

    public async Task<MembershipPaymentBinding?> FindByPaymentIntentAsync(
        PaymentIntentId paymentIntentId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paymentIntentId);

        return await Bindings.FirstOrDefaultAsync(
            binding => binding.PaymentIntentId == paymentIntentId, cancellationToken);
    }

    public async Task<IReadOnlyList<MembershipPaymentBinding>> ListByMembershipAsync(
        Guid membershipId, CancellationToken cancellationToken = default) =>
        await Bindings
            .Where(binding => binding.MembershipId == membershipId)
            .OrderByDescending(binding => binding.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MembershipPaymentBinding>> ListForReconciliationAsync(
        DateTimeOffset boundBefore,
        int maxReannouncements,
        int take,
        CancellationToken cancellationToken = default) =>
        await Bindings
            .Where(binding => binding.CreatedAt <= boundBefore
                && binding.ReannounceCount < maxReannouncements)
            .OrderBy(binding => binding.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
}

/// <summary>EF Core repository for campaigns.</summary>
public sealed class CampaignRepository : ICampaignRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CampaignRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<Campaign> Campaigns => _dbContext.Set<Campaign>();

    public void Add(Campaign campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        Campaigns.Add(campaign);
    }

    public void Update(Campaign campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        FinancialAggregateUpdater.MarkDirty(_dbContext, campaign);
    }

    public async Task<Campaign?> FindAsync(
        CampaignId campaignId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(campaignId);

        return await Campaigns.FirstOrDefaultAsync(
            campaign => campaign.Id == campaignId, cancellationToken);
    }

    public async Task<Campaign?> FindByIdempotencyKeyAsync(
        Guid organizerUserId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || organizerUserId == Guid.Empty)
        {
            return null;
        }

        var normalized = idempotencyKey.Trim();

        // Scoped to the organizer. See IPaymentIntentRepository for why the owner is half
        // the key rather than a filter.
        return await Campaigns.FirstOrDefaultAsync(
            campaign => campaign.IdempotencyKey == normalized
                && campaign.OrganizerUserId == organizerUserId,
            cancellationToken);
    }

    public async Task<Campaign?> FindByCauseCodeAsync(
        string causeCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(causeCode))
        {
            return null;
        }

        var normalized = causeCode.Trim().ToLowerInvariant();

        return await Campaigns.FirstOrDefaultAsync(
            campaign => campaign.CauseCode == normalized, cancellationToken);
    }

    /// <summary>
    /// Searches campaigns, newest first.
    /// <para>
    /// <b>Visibility is composed into the predicate rather than filtered afterwards.</b> Trimming a
    /// materialised page down to what the viewer may see returns short pages and eventually an empty
    /// one that is not the end of the data — the classic post-filter paging defect.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<Campaign>> SearchAsync(
        CampaignFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Campaigns.AsQueryable();

        if (filter.OrganizedByViewerOnly && filter.ViewerUserId is { } mine)
        {
            query = query.Where(campaign => campaign.OrganizerUserId == mine);
        }
        else if (filter.ViewerUserId is { } viewer)
        {
            // Public campaigns, plus the viewer's own whatever state they are in. The hidden states
            // are CampaignTransitions.IsPubliclyVisible's, spelled out because EF cannot translate
            // a call into it.
            query = query.Where(campaign =>
                campaign.OrganizerUserId == viewer
                || (campaign.Status != CampaignStatus.Draft
                    && campaign.Status != CampaignStatus.PendingReview
                    && campaign.Status != CampaignStatus.Rejected
                    && campaign.Status != CampaignStatus.Suspended));
        }
        else
        {
            query = query.Where(campaign =>
                campaign.Status != CampaignStatus.Draft
                && campaign.Status != CampaignStatus.PendingReview
                && campaign.Status != CampaignStatus.Rejected
                && campaign.Status != CampaignStatus.Suspended);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(campaign => campaign.Status == status);
        }

        if (filter.Category is { } category)
        {
            query = query.Where(campaign => campaign.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(filter.CountryCode))
        {
            var country = filter.CountryCode.Trim().ToUpperInvariant();

            query = query.Where(campaign => campaign.CountryCode == country);
        }

        if (filter.OrganizationId is { } organizationId)
        {
            query = query.Where(campaign => campaign.OrganizationId == organizationId);
        }

        if (filter.Before is { } before)
        {
            query = query.Where(campaign => campaign.CreatedAt <= before);
        }

        if (filter.ExcludeIds.Count > 0)
        {
            // Compared as the identifier type, never as `.Value` on a converted property — that
            // compiles, reads correctly and cannot be translated (ADR-017, and DSP3003).
            var excluded = filter.ExcludeIds.Select(CampaignId.From).ToList();

            query = query.Where(campaign => !excluded.Contains(campaign.Id));
        }

        return await query
            .OrderByDescending(campaign => campaign.CreatedAt)
            .Take(filter.Take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Campaign>> ListPastDeadlineAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken = default) =>
        await Campaigns
            .Where(campaign => campaign.Deadline != null
                && campaign.Deadline <= asOf
                && (campaign.Status == CampaignStatus.Published
                    || campaign.Status == CampaignStatus.Active
                    || campaign.Status == CampaignStatus.GoalReached))
            .OrderBy(campaign => campaign.Deadline)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Campaign>> ListWithOpenMilestonesAsync(
        int take, CancellationToken cancellationToken = default) =>
        await Campaigns
            .Where(campaign => campaign.CauseId != null
                && campaign.Milestones.Any(milestone => milestone.ReachedAt == null))
            .OrderBy(campaign => campaign.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
}

/// <summary>EF Core repository for campaign updates.</summary>
public sealed class CampaignUpdateRepository : ICampaignUpdateRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CampaignUpdateRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CampaignUpdate> Updates => _dbContext.Set<CampaignUpdate>();

    public void Add(CampaignUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        Updates.Add(update);
    }

    public void Update(CampaignUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        FinancialAggregateUpdater.MarkDirty(_dbContext, update);
    }

    public async Task<CampaignUpdate?> FindAsync(
        CampaignUpdateId updateId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updateId);

        return await Updates.FirstOrDefaultAsync(
            update => update.Id == updateId, cancellationToken);
    }

    public async Task<CampaignUpdate?> FindByIdempotencyKeyAsync(
        Guid authorUserId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || authorUserId == Guid.Empty)
        {
            return null;
        }

        var normalized = idempotencyKey.Trim();

        return await Updates.FirstOrDefaultAsync(
            update => update.IdempotencyKey == normalized && update.AuthorUserId == authorUserId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<CampaignUpdate>> ListByCampaignAsync(
        CampaignId campaignId,
        bool includeWithdrawn,
        DateTimeOffset? before,
        IReadOnlyList<Guid> excludeIds,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(campaignId);
        ArgumentNullException.ThrowIfNull(excludeIds);

        var query = Updates.Where(update => update.CampaignId == campaignId);

        if (!includeWithdrawn)
        {
            query = query.Where(update => update.WithdrawnAt == null);
        }

        if (before is { } boundary)
        {
            query = query.Where(update => update.PublishedAt <= boundary);
        }

        if (excludeIds.Count > 0)
        {
            var excluded = excludeIds.Select(CampaignUpdateId.From).ToList();

            query = query.Where(update => !excluded.Contains(update.Id));
        }

        return await query
            .OrderByDescending(update => update.PublishedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}

/// <summary>EF Core repository for recurring donation schedules.</summary>
public sealed class RecurringDonationRepository : IRecurringDonationRepository
{
    private readonly DiasporaDbContext _dbContext;

    public RecurringDonationRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<RecurringDonation> Schedules => _dbContext.Set<RecurringDonation>();

    public void Add(RecurringDonation schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        Schedules.Add(schedule);
    }

    public void Update(RecurringDonation schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        FinancialAggregateUpdater.MarkDirty(_dbContext, schedule);
    }

    public async Task<RecurringDonation?> FindAsync(
        RecurringDonationId scheduleId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scheduleId);

        return await Schedules.FirstOrDefaultAsync(
            schedule => schedule.Id == scheduleId, cancellationToken);
    }

    public async Task<RecurringDonation?> FindByIdempotencyKeyAsync(
        Guid donorUserId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || donorUserId == Guid.Empty)
        {
            return null;
        }

        var normalized = idempotencyKey.Trim();

        return await Schedules.FirstOrDefaultAsync(
            schedule => schedule.IdempotencyKey == normalized && schedule.DonorUserId == donorUserId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<RecurringDonation>> ListByDonorAsync(
        Guid donorUserId, CancellationToken cancellationToken = default) =>
        await Schedules
            .Where(schedule => schedule.DonorUserId == donorUserId)
            .OrderByDescending(schedule => schedule.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RecurringDonation>> ListDueAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken = default) =>
        await Schedules
            .Where(schedule =>
                (schedule.Status == RecurringDonationStatus.Active
                    || schedule.Status == RecurringDonationStatus.Retrying)
                && schedule.NextChargeAt != null
                && schedule.NextChargeAt <= asOf)
            .OrderBy(schedule => schedule.NextChargeAt)
            .Take(take)
            .ToListAsync(cancellationToken);
}
