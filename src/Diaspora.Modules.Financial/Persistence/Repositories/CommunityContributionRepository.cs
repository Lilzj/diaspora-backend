using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for community contributions.</summary>
public sealed class CommunityContributionRepository : ICommunityContributionRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CommunityContributionRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CommunityContribution> Contributions =>
        _dbContext.Set<CommunityContribution>();

    public void Add(CommunityContribution contribution)
    {
        ArgumentNullException.ThrowIfNull(contribution);

        Contributions.Add(contribution);
    }

    public void Update(CommunityContribution contribution)
    {
        ArgumentNullException.ThrowIfNull(contribution);

        FinancialAggregateUpdater.MarkDirty(_dbContext, contribution);
    }

    public async Task<CommunityContribution?> FindAsync(
        CommunityContributionId contributionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contributionId);

        return await Contributions.FirstOrDefaultAsync(
            contribution => contribution.Id == contributionId, cancellationToken);
    }

    public async Task<CommunityContribution?> FindByReceiptAsync(
        string receiptReference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(receiptReference))
        {
            return null;
        }

        var normalized = receiptReference.Trim().ToUpperInvariant();

        return await Contributions.FirstOrDefaultAsync(
            contribution => contribution.ReceiptReference == normalized, cancellationToken);
    }

    public async Task<CommunityContribution?> FindByPaymentIntentAsync(
        PaymentIntentId paymentIntentId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paymentIntentId);

        return await Contributions.FirstOrDefaultAsync(
            contribution => contribution.PaymentIntentId == paymentIntentId, cancellationToken);
    }

    public async Task<IReadOnlyList<CommunityContribution>> ListByContributorAsync(
        Guid contributorUserId, int take, CancellationToken cancellationToken = default) =>
        await Contributions
            .Where(contribution => contribution.ContributorUserId == contributorUserId)
            .OrderByDescending(contribution => contribution.CreatedAt)
            .Take(Math.Clamp(take, 1, Domain.Policies.FinancialLimits.MaxLedgerPageSize))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CommunityContribution>> ListByFundAsync(
        ContributionFundId fundId,
        GivingStatus? status,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fundId);

        var contributions = Contributions.Where(contribution => contribution.FundId == fundId);

        if (status is { } wanted)
        {
            contributions = contributions.Where(contribution => contribution.Status == wanted);
        }

        return await contributions
            .OrderByDescending(contribution => contribution.CreatedAt)
            .Take(Math.Clamp(take, 1, Domain.Policies.FinancialLimits.MaxLedgerPageSize))
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountNonMonetaryAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
        Contributions.CountAsync(
            contribution => contribution.Status == GivingStatus.Completed
                && contribution.Kind != ContributionKind.Monetary
                && contribution.CreatedAt >= from
                && contribution.CreatedAt < to,
            cancellationToken);

    public async Task<(long RaisedMinorUnits, int ContributorCount)> TotalsForFundAsync(
        ContributionFundId fundId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fundId);

        var completed = Contributions.Where(
            contribution => contribution.FundId == fundId
                && contribution.Status == GivingStatus.Completed);

        // Only the monetary kind carries an amount; the other four count as contributors and
        // deliberately add nothing to the total.
        var raised = await completed
            .Where(contribution => contribution.Kind == ContributionKind.Monetary)
            .SumAsync(contribution => (long?)contribution.AmountMinorUnits, cancellationToken) ?? 0L;

        var contributors = await completed.CountAsync(cancellationToken);

        return (raised, contributors);
    }
}
