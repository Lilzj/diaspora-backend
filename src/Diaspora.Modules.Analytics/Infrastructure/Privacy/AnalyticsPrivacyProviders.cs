using Diaspora.Modules.Analytics.Domain.Exports;
using Diaspora.Modules.Analytics.Domain.Reports;
using Diaspora.Persistence;
using Diaspora.Platform.Operations.Privacy;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Analytics.Infrastructure.Privacy;

/// <summary>
/// Erases what a member kept in Analytics: their saved report definitions and the exports they asked
/// for. Both are the member's own records, so both are deleted outright.
/// <para>
/// The raw activity store is not covered here. It holds keyed hashes rather than members, and its
/// erasure path is <c>IAnalyticsEventRepository.DeleteForActorAsync</c>.
/// </para>
/// </summary>
public sealed class AnalyticsDataErasureProvider : IDataErasureProvider
{
    private const string ModuleName = "Analytics";
    private const string ReportsDataSet = "saved analytics reports";
    private const string ExportsDataSet = "analytics exports";

    private readonly DiasporaDbContext _dbContext;

    public AnalyticsDataErasureProvider(DiasporaDbContext dbContext) => _dbContext = dbContext;

    public string Module => ModuleName;

    public async Task<IReadOnlyList<ErasureResult>> PreviewAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var reports = await _dbContext.Set<AnalyticsSavedReport>()
            .CountAsync(report => report.OwnerUserId == userId, cancellationToken);

        var exports = await _dbContext.Set<AnalyticsExport>()
            .CountAsync(export => export.OwnerUserId == userId, cancellationToken);

        return [Outcome(ReportsDataSet, reports), Outcome(ExportsDataSet, exports)];
    }

    public async Task<IReadOnlyList<ErasureResult>> EraseAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var reports = await _dbContext.Set<AnalyticsSavedReport>()
            .Where(report => report.OwnerUserId == userId)
            .ToListAsync(cancellationToken);

        var exports = await _dbContext.Set<AnalyticsExport>()
            .Where(export => export.OwnerUserId == userId)
            .ToListAsync(cancellationToken);

        if (reports.Count > 0 || exports.Count > 0)
        {
            _dbContext.Set<AnalyticsSavedReport>().RemoveRange(reports);
            _dbContext.Set<AnalyticsExport>().RemoveRange(exports);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return [Outcome(ReportsDataSet, reports.Count), Outcome(ExportsDataSet, exports.Count)];
    }

    private static ErasureResult Outcome(string dataSet, int count) =>
        new(ModuleName, dataSet, count == 0 ? ErasureOutcome.NothingHeld : ErasureOutcome.Deleted, count);
}
