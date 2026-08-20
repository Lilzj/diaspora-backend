using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Revenue;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for revenue rollups.</summary>
public sealed class RevenueRecordRepository : IRevenueRecordRepository
{
    private readonly DiasporaDbContext _dbContext;

    public RevenueRecordRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<RevenueRecord> Records => _dbContext.Set<RevenueRecord>();

    public void Add(RevenueRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        Records.Add(record);
    }

    public void Update(RevenueRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        FinancialAggregateUpdater.MarkDirty(_dbContext, record);
    }

    public async Task<RevenueRecord?> FindAsync(
        RevenueRecordId recordId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recordId);

        return await Records.FirstOrDefaultAsync(record => record.Id == recordId, cancellationToken);
    }

    public async Task<RevenueRecord?> FindForPeriodAsync(
        DateTimeOffset periodStart,
        RevenueDimension dimension,
        string dimensionValue,
        string currency,
        CancellationToken cancellationToken = default)
    {
        var value = string.IsNullOrWhiteSpace(dimensionValue)
            ? "*"
            : dimensionValue.Trim().ToLowerInvariant();

        var normalizedCurrency = currency?.Trim().ToUpperInvariant() ?? string.Empty;

        return await Records.FirstOrDefaultAsync(
            record => record.PeriodStart == periodStart
                && record.Dimension == dimension
                && record.DimensionValue == value
                && record.Currency == normalizedCurrency,
            cancellationToken);
    }

    public async Task<IReadOnlyList<RevenueRecord>> ListAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        RevenueDimension? dimension,
        string? currency,
        CancellationToken cancellationToken = default)
    {
        var records = Records.Where(
            record => record.PeriodStart >= from && record.PeriodStart < to);

        if (dimension is { } wanted)
        {
            records = records.Where(record => record.Dimension == wanted);
        }

        if (!string.IsNullOrWhiteSpace(currency))
        {
            var normalized = currency.Trim().ToUpperInvariant();

            records = records.Where(record => record.Currency == normalized);
        }

        return await records
            .OrderBy(record => record.PeriodStart)
            .ThenBy(record => record.DimensionValue)
            .ToListAsync(cancellationToken);
    }
}
