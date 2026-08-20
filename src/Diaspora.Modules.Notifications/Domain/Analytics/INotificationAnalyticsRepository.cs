using Diaspora.Modules.Notifications.Domain.Enums;

namespace Diaspora.Modules.Notifications.Domain.Analytics;

/// <summary>
/// The analytics rollup's persistence port.
/// <para>
/// <see cref="GetOrOpenAsync"/> rather than find-then-add: the bucket is written from inside the
/// transaction of whatever caused the event, concurrently, by every request on the platform. A
/// two-step create is a race the unique index on the grain would turn into an exception on the
/// request path.
/// </para>
/// </summary>
public interface INotificationAnalyticsRepository
{
    Task<NotificationAnalytics> GetOrOpenAsync(
        DateOnly day,
        string categoryKey,
        NotificationChannelKind channel,
        string templateKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationAnalytics>> QueryAsync(
        NotificationAnalyticsFilter filter, CancellationToken cancellationToken = default);

    void Update(NotificationAnalytics analytics);
}
