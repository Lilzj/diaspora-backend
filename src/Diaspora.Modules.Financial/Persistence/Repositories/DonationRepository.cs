using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for donations.</summary>
public sealed class DonationRepository : IDonationRepository
{
    private readonly DiasporaDbContext _dbContext;

    public DonationRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<Donation> Donations => _dbContext.Set<Donation>();

    public void Add(Donation donation)
    {
        ArgumentNullException.ThrowIfNull(donation);

        Donations.Add(donation);
    }

    public void Update(Donation donation)
    {
        ArgumentNullException.ThrowIfNull(donation);

        FinancialAggregateUpdater.MarkDirty(_dbContext, donation);
    }

    public async Task<Donation?> FindAsync(
        DonationId donationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(donationId);

        return await Donations.FirstOrDefaultAsync(
            donation => donation.Id == donationId, cancellationToken);
    }

    public async Task<Donation?> FindByReceiptAsync(
        string receiptReference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(receiptReference))
        {
            return null;
        }

        var normalized = receiptReference.Trim().ToUpperInvariant();

        return await Donations.FirstOrDefaultAsync(
            donation => donation.ReceiptReference == normalized, cancellationToken);
    }

    public async Task<Donation?> FindByPaymentIntentAsync(
        PaymentIntentId paymentIntentId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paymentIntentId);

        return await Donations.FirstOrDefaultAsync(
            donation => donation.PaymentIntentId == paymentIntentId, cancellationToken);
    }

    public async Task<IReadOnlyList<Donation>> ListByDonorAsync(
        Guid donorUserId, int take, CancellationToken cancellationToken = default) =>
        await Donations
            .Where(donation => donation.DonorUserId == donorUserId)
            .OrderByDescending(donation => donation.CreatedAt)
            .Take(Math.Clamp(take, 1, Domain.Policies.FinancialLimits.MaxLedgerPageSize))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Donation>> ListByDonorForCauseAsync(
        Guid donorUserId,
        DonationCauseId causeId,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(causeId);

        return await Donations
            .Where(donation => donation.DonorUserId == donorUserId && donation.CauseId == causeId)
            .OrderByDescending(donation => donation.CreatedAt)
            .ThenByDescending(donation => donation.Id)
            .Take(Math.Clamp(take, 1, Domain.Policies.FinancialLimits.MaxLedgerPageSize))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Donation>> ListByCauseAsync(
        DonationCauseId causeId,
        GivingStatus? status,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(causeId);

        var donations = Donations.Where(donation => donation.CauseId == causeId);

        if (status is { } wanted)
        {
            donations = donations.Where(donation => donation.Status == wanted);
        }

        return await donations
            .OrderByDescending(donation => donation.CreatedAt)
            .Take(Math.Clamp(take, 1, Domain.Policies.FinancialLimits.MaxLedgerPageSize))
            .ToListAsync(cancellationToken);
    }

    public async Task<(long RaisedMinorUnits, int SupporterCount)> TotalsForCauseAsync(
        DonationCauseId causeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(causeId);

        var completed = Donations.Where(
            donation => donation.CauseId == causeId && donation.Status == GivingStatus.Completed);

        var raised = await completed.SumAsync(
            donation => (long?)donation.AmountMinorUnits, cancellationToken) ?? 0L;

        var supporters = await completed.CountAsync(cancellationToken);

        return (raised, supporters);
    }

    // ── B15.1 · COMMUNITY-LIFE-GAP-007 ───────────────────────────────────────────────────────

    public async Task<IReadOnlyList<Donation>> ListSupportersAsync(
        DonationCauseId causeId,
        DateTimeOffset? before,
        IReadOnlyList<Guid> excludeIds,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(causeId);
        ArgumentNullException.ThrowIfNull(excludeIds);

        var query = Donations.Where(donation =>
            donation.CauseId == causeId && donation.Status == GivingStatus.Completed);

        if (before is { } boundary)
        {
            query = query.Where(donation => donation.CreatedAt <= boundary);
        }

        if (excludeIds.Count > 0)
        {
            // Compared as DonationId, never as `.Value` on a value-converted property — that
            // compiles and throws at runtime (ADR-017, DSP3003).
            var excluded = excludeIds.Select(DonationId.From).ToList();

            query = query.Where(donation => !excluded.Contains(donation.Id));
        }

        return await query
            .OrderByDescending(donation => donation.CreatedAt)
            .Take(Math.Clamp(take, 1, Domain.Policies.FinancialLimits.MaxLedgerPageSize))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Buckets a cause's completed giving by UTC day, adding up in the database.
    /// <para>
    /// <b>Grouped on <c>CompletedAt</c> rather than <c>CreatedAt</c>.</b> A donation pledged on
    /// Monday and captured on Tuesday is Tuesday's money — the day it arrived is the day the chart
    /// is about, and using the pledge date would put money on a day the cause did not have it.
    /// </para>
    /// <para>
    /// <b>The <c>.Date</c> grouping is done over a materialised projection of two columns.</b> Every
    /// provider translates <c>SUM</c> and <c>GROUP BY</c>, but not every one translates
    /// <c>DateTimeOffset.UtcDateTime.Date</c>, and SQLite is one that does not. Projecting the two
    /// numbers the bucket needs and grouping them in memory reads at most one row per completed
    /// donation in the window and never the whole aggregate graph — which is the cost this method
    /// exists to avoid.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<DonationPeriodTotal>> TimeSeriesForCauseAsync(
        DonationCauseId causeId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(causeId);

        var rows = await Donations
            .Where(donation => donation.CauseId == causeId
                && donation.Status == GivingStatus.Completed
                && donation.CompletedAt != null
                && donation.CompletedAt >= from
                && donation.CompletedAt <= to)
            .Select(donation => new
            {
                Completed = donation.CompletedAt!.Value,
                donation.AmountMinorUnits
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows
                .GroupBy(row => new DateTimeOffset(row.Completed.UtcDateTime.Date, TimeSpan.Zero))
                .OrderBy(group => group.Key)
                .Select(group => new DonationPeriodTotal
                {
                    PeriodStart = group.Key,
                    AmountMinorUnits = group.Sum(row => row.AmountMinorUnits),
                    DonationCount = group.Count()
                })
        ];
    }

    public async Task<DonationWindowSummary> SummariseForCauseAsync(
        DonationCauseId causeId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(causeId);

        // The window is on CompletedAt for anything that completed and on CreatedAt for anything
        // that did not — a donation that failed has no completion instant, and excluding it because
        // of that would report zero failures on a day full of them.
        var rows = await Donations
            .Where(donation => donation.CauseId == causeId
                && ((donation.CompletedAt != null
                        && donation.CompletedAt >= from
                        && donation.CompletedAt <= to)
                    || (donation.CompletedAt == null
                        && donation.CreatedAt >= from
                        && donation.CreatedAt <= to)))
            .Select(donation => new
            {
                donation.Status,
                donation.AmountMinorUnits,
                donation.Currency,
                donation.DonorUserId,
                donation.IsAnonymous
            })
            .ToListAsync(cancellationToken);

        var completed = rows.Where(row => row.Status == GivingStatus.Completed).ToList();
        var refunded = rows.Where(row => row.Status == GivingStatus.Refunded).ToList();

        // An anonymous gift carries no identifier, so it cannot be deduplicated against another —
        // each counts once. A named donor giving three times is one supporter.
        var namedSupporters = completed
            .Where(row => !row.IsAnonymous && row.DonorUserId is not null)
            .Select(row => row.DonorUserId!.Value)
            .Distinct()
            .Count();

        var anonymousGifts = completed.Count(row => row.IsAnonymous || row.DonorUserId is null);

        return new DonationWindowSummary
        {
            CompletedMinorUnits = completed.Sum(row => row.AmountMinorUnits),
            CompletedCount = completed.Count,
            RefundedMinorUnits = refunded.Sum(row => row.AmountMinorUnits),
            RefundedCount = refunded.Count,
            FailedCount = rows.Count(row => row.Status == GivingStatus.Failed),
            SupporterCount = namedSupporters + anonymousGifts,
            Currencies =
            [
                .. rows.Select(row => row.Currency).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            ]
        };
    }
}
