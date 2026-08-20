using Diaspora.Modules.Notifications.Domain.Analytics;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Notifications.Application.Services;

/// <summary>
/// Moves the daily rollup, from wherever the fact happened.
/// <para>
/// <b>One class so the grain is decided once.</b> Every counter is bucketed by
/// (day, category, channel, template), and an event handler that opened a bucket with a slightly
/// different key — the local day instead of the UTC one, a channel omitted — would create a second
/// row that quietly halves every rate anybody reads.
/// </para>
/// <para>
/// <b>Engagement is recorded against the channel it happened on.</b> An open on a message that
/// arrived by push counts towards push's open rate, which is what makes "push is delivering but
/// nobody reads it" a question with an answer.
/// </para>
/// </summary>
public sealed class NotificationAnalyticsRecorder
{
    private readonly INotificationAnalyticsRepository _analytics;
    private readonly IClock _clock;

    public NotificationAnalyticsRecorder(INotificationAnalyticsRepository analytics, IClock clock)
    {
        _analytics = analytics;
        _clock = clock;
    }

    public Task RecordCreatedAsync(
        string categoryKey, string templateKey, CancellationToken cancellationToken = default) =>
        ApplyAsync(
            categoryKey,
            NotificationChannelKind.InApp,
            templateKey,
            bucket => bucket.RecordCreated(),
            cancellationToken);

    public Task RecordQueuedAsync(
        string categoryKey,
        NotificationChannelKind channel,
        string templateKey,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(categoryKey, channel, templateKey, bucket => bucket.RecordQueued(), cancellationToken);

    public Task RecordDeliveredAsync(
        string categoryKey,
        NotificationChannelKind channel,
        string templateKey,
        int attempt,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            categoryKey, channel, templateKey, bucket => bucket.RecordDelivered(attempt), cancellationToken);

    public Task RecordFailedAsync(
        string categoryKey,
        NotificationChannelKind channel,
        string templateKey,
        int attempts,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            categoryKey, channel, templateKey, bucket => bucket.RecordFailed(attempts), cancellationToken);

    public Task RecordOpenedAsync(
        string categoryKey,
        NotificationChannelKind channel,
        string templateKey,
        double minutesToOpen,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            categoryKey,
            channel,
            templateKey,
            bucket => bucket.RecordOpened(minutesToOpen),
            cancellationToken);

    public Task RecordClickedAsync(
        string categoryKey,
        NotificationChannelKind channel,
        string templateKey,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(categoryKey, channel, templateKey, bucket => bucket.RecordClicked(), cancellationToken);

    public Task RecordArchivedAsync(
        string categoryKey,
        string templateKey,
        bool wasDismissed,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            categoryKey,
            NotificationChannelKind.InApp,
            templateKey,
            bucket =>
            {
                if (wasDismissed)
                {
                    bucket.RecordDismissed();
                }

                bucket.RecordArchived();
            },
            cancellationToken);

    public Task RecordSuppressedAsync(
        string categoryKey,
        NotificationChannelKind channel,
        string templateKey,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            categoryKey, channel, templateKey, bucket => bucket.RecordSuppressed(), cancellationToken);

    /// <summary>
    /// The UTC day is the bucket, always.
    /// <para>
    /// Deliberately not the member's local day: a rollup has no member, and bucketing by whoever
    /// happened to be involved would make two rows for one hour and neither comparable to the other.
    /// </para>
    /// </summary>
    private async Task ApplyAsync(
        string categoryKey,
        NotificationChannelKind channel,
        string templateKey,
        Action<NotificationAnalytics> mutate,
        CancellationToken cancellationToken)
    {
        var day = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);

        var bucket = await _analytics.GetOrOpenAsync(
            day, categoryKey, channel, templateKey, cancellationToken);

        mutate(bucket);

        _analytics.Update(bucket);
    }
}
