using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Revenue;

/// <summary>Retrieval and staging for revenue rollups.</summary>
public interface IRevenueRecordRepository
{
    void Add(RevenueRecord record);

    void Update(RevenueRecord record);

    Task<RevenueRecord?> FindAsync(
        RevenueRecordId recordId, CancellationToken cancellationToken = default);

    /// <summary>The existing row for a period and dimension, so a re-roll updates rather than duplicates.</summary>
    Task<RevenueRecord?> FindForPeriodAsync(
        DateTimeOffset periodStart,
        RevenueDimension dimension,
        string dimensionValue,
        string currency,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RevenueRecord>> ListAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        RevenueDimension? dimension,
        string? currency,
        CancellationToken cancellationToken = default);
}
