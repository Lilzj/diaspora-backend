using Diaspora.Modules.Analytics.Domain.Periods;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.Modules.Analytics.Domain.Rollups;

namespace Diaspora.Modules.Analytics.Application.Services;

/// <summary>
/// Per-item totals for a resolved subject over a window, from the daily rollups.
/// <para>
/// The board, the insights and the premium summary all ask "how did each of these items do", and
/// they ask it of the same rows every owner dashboard reads, so a board and the dashboard above it
/// cannot disagree about an item's views.
/// </para>
/// </summary>
public sealed class AnalyticsItemReads
{
    private readonly AnalyticsReadService _reads;
    private readonly IAnalyticsRollupRepository _rollups;

    public AnalyticsItemReads(AnalyticsReadService reads, IAnalyticsRollupRepository rollups)
    {
        _reads = reads;
        _rollups = rollups;
    }

    public async Task<IReadOnlyList<AnalyticsItemTotals>> ReadAsync(
        AnalyticsActivitySubject subject, AnalyticsPeriod period, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(period);

        switch (subject.Kind)
        {
            case AnalyticsActivitySubjectKind.Platform:
                return await _rollups
                    .SumAllContentAsync(period.FromDate, period.ToDate, cancellationToken)
                    .ConfigureAwait(false);

            case AnalyticsActivitySubjectKind.Content:
            {
                var rows = await _reads
                    .ReadContentDailyAsync(subject.ContentType!.Value, subject.ContentId!.Value, period, cancellationToken)
                    .ConfigureAwait(false);

                return Fold(rows);
            }

            default:
            {
                var rows = await _reads
                    .ReadOwnerContentDailyAsync(subject.OwnerUserId!.Value, period, cancellationToken)
                    .ConfigureAwait(false);

                return Fold(subject.ContentType is { } type
                    ? rows.Where(row => row.ContentType == type).ToList()
                    : rows);
            }
        }
    }

    /// <summary>Daily rows summed per item.</summary>
    public static IReadOnlyList<AnalyticsItemTotals> Fold(IEnumerable<ContentDailyRollup> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return rows
            .GroupBy(row => (row.ContentType, row.ContentId))
            .Select(group => new AnalyticsItemTotals(
                group.Key.ContentType,
                group.Key.ContentId,
                group.Sum(row => row.Counts.Views),
                group.Sum(row => row.Counts.Saves),
                group.Sum(row => row.Counts.Shares),
                group.Sum(row => row.Counts.Comments),
                group.Sum(row => row.Counts.Reactions),
                group.Sum(row => row.Counts.HelpfulVotes),
                group.Sum(row => row.Counts.Conversions),
                group.Sum(row => row.Counts.Enquiries)))
            .ToList();
    }
}
