using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Analytics;

namespace Diaspora.Modules.Notifications.Application.Analytics;

/// <summary>
/// Sums daily buckets into a window, with three breakdowns.
/// <para>
/// <b>Rates are derived from the sums, in one place, so they cannot be derived differently
/// elsewhere.</b> Averaging averages is the single most common way an analytics surface reports a
/// number nobody can reproduce.
/// </para>
/// </summary>
public static class NotificationAnalyticsAggregator
{
    public static NotificationAnalyticsResponse Aggregate(
        IReadOnlyList<NotificationAnalytics> rows, DateOnly from, DateOnly to, int rowLimit)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var queued = rows.Sum(row => row.QueuedCount);
        var delivered = rows.Sum(row => row.DeliveredCount);
        var opened = rows.Sum(row => row.OpenedCount);
        var minutes = rows.Sum(row => row.MinutesToOpenTotal);

        return new NotificationAnalyticsResponse
        {
            From = from,
            To = to,
            Created = rows.Sum(row => row.CreatedCount),
            Queued = queued,
            Delivered = delivered,
            Opened = opened,
            Clicked = rows.Sum(row => row.ClickedCount),
            Dismissed = rows.Sum(row => row.DismissedCount),
            Archived = rows.Sum(row => row.ArchivedCount),
            Failed = rows.Sum(row => row.FailedCount),
            Suppressed = rows.Sum(row => row.SuppressedCount),
            Digested = rows.Sum(row => row.DigestedCount),
            Retries = rows.Sum(row => row.RetryCount),
            DeliveryRate = Rate(delivered, queued),
            OpenRate = Rate(opened, delivered),
            ClickThroughRate = Rate(rows.Sum(row => row.ClickedCount), opened),
            DismissRate = Rate(rows.Sum(row => row.DismissedCount), delivered),
            FailureRate = Rate(rows.Sum(row => row.FailedCount), queued),
            AverageMinutesToOpen = opened == 0 ? 0 : minutes / opened,
            ByChannel = Breakdown(rows, row => row.Channel.ToString()),
            ByCategory = Breakdown(rows, row => row.CategoryKey),
            ByTemplate = Breakdown(rows, row => row.TemplateKey),

            // Honest rather than silent: a window that hit the cap is narrower than it looks, and a
            // dashboard that did not say so would report a drop that never happened.
            IsTruncated = rows.Count >= rowLimit
        };
    }

    private static List<NotificationAnalyticsBreakdownResponse> Breakdown(
        IReadOnlyList<NotificationAnalytics> rows, Func<NotificationAnalytics, string> key) =>
        rows.GroupBy(key, StringComparer.Ordinal)
            .Select(group =>
            {
                var queued = group.Sum(row => row.QueuedCount);
                var delivered = group.Sum(row => row.DeliveredCount);
                var opened = group.Sum(row => row.OpenedCount);
                var clicked = group.Sum(row => row.ClickedCount);

                return new NotificationAnalyticsBreakdownResponse
                {
                    Key = group.Key,
                    Queued = queued,
                    Delivered = delivered,
                    Opened = opened,
                    Clicked = clicked,
                    Failed = group.Sum(row => row.FailedCount),
                    DeliveryRate = Rate(delivered, queued),
                    OpenRate = Rate(opened, delivered),
                    ClickThroughRate = Rate(clicked, opened)
                };
            })
            .OrderByDescending(entry => entry.Queued)
            .ToList();

    private static double Rate(long numerator, long denominator) =>
        denominator == 0 ? 0 : (double)numerator / denominator;
}
