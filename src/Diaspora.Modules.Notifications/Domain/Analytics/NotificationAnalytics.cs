using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Analytics;

/// <summary>
/// One day's measured record for one category, channel and template.
/// <para>
/// <b>A rollup, not a query.</b> Every number the brief asks for — delivery rate, open rate,
/// click-through, dismiss rate, failure rate, retry count — is an aggregate over the notification
/// table, and computing any of them on demand means scanning millions of rows to draw one chart. A
/// daily bucket incremented inside the transaction that caused the event costs one indexed upsert
/// per notification and answers every one of them from a table that stays small.
/// </para>
/// <para>
/// <b>The grain is (day, category, channel, template) and every part of it earns its place.</b>
/// Without the channel, "push is failing" is invisible behind healthy in-app numbers. Without the
/// template, "this particular wording is never opened" cannot be asked. Without the day, nothing can
/// be compared to last week, which is the only way anybody notices a regression.
/// </para>
/// <para>
/// <b>It outlives what it measures.</b> A template is deactivated and its rows stay; a member deletes
/// their account and the counters, which name nobody, stay. That is deliberate — an analytics row
/// that vanished with its subject would make every historical comparison a lie.
/// </para>
/// </summary>
public sealed class NotificationAnalytics : AggregateRoot<NotificationAnalyticsId>, IAuditableEntity
{
    private NotificationAnalytics(
        NotificationAnalyticsId id,
        DateOnly day,
        string categoryKey,
        NotificationChannelKind channel,
        string templateKey,
        DateTimeOffset createdAt) : base(id)
    {
        Day = day;
        CategoryKey = categoryKey;
        Channel = channel;
        TemplateKey = templateKey;
        CreatedAt = createdAt;
    }

    private NotificationAnalytics() { }

    /// <summary>The UTC day. Deliberately not the member's day — a rollup has no member.</summary>
    public DateOnly Day { get; private set; }

    public string CategoryKey { get; private set; } = string.Empty;

    public NotificationChannelKind Channel { get; private set; }

    public string TemplateKey { get; private set; } = string.Empty;

    public long CreatedCount { get; private set; }

    public long QueuedCount { get; private set; }

    public long DeliveredCount { get; private set; }

    public long OpenedCount { get; private set; }

    public long ClickedCount { get; private set; }

    public long DismissedCount { get; private set; }

    public long ArchivedCount { get; private set; }

    public long FailedCount { get; private set; }

    public long SuppressedCount { get; private set; }

    public long DigestedCount { get; private set; }

    public long RetryCount { get; private set; }

    /// <summary>
    /// Total minutes from creation to first open, across every message that was opened.
    /// <para>
    /// A sum rather than an average, because averages do not add. Dividing by
    /// <see cref="OpenedCount"/> gives the mean for any set of buckets a caller chooses to combine;
    /// storing the mean would make a week's average of seven daily averages silently wrong whenever
    /// the days had different volumes.
    /// </para>
    /// </summary>
    public double MinutesToOpenTotal { get; private set; }

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static NotificationAnalytics OpenBucket(
        NotificationAnalyticsId id,
        DateOnly day,
        string categoryKey,
        NotificationChannelKind channel,
        string templateKey,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);

        return new NotificationAnalytics(
            id, day, categoryKey.Trim().ToLowerInvariant(), channel, templateKey.Trim().ToLowerInvariant(), createdAt);
    }

    public void RecordCreated() => Bump(() => CreatedCount++);

    public void RecordQueued() => Bump(() => QueuedCount++);

    public void RecordDelivered(int attempt)
    {
        DeliveredCount++;

        // Attempt one is not a retry. Counting it as one would put the retry rate at 100%.
        if (attempt > 1)
        {
            RetryCount += attempt - 1;
        }

        Revision++;
    }

    public void RecordOpened(double minutesToOpen)
    {
        OpenedCount++;
        MinutesToOpenTotal += Math.Max(0, minutesToOpen);

        Revision++;
    }

    public void RecordClicked() => Bump(() => ClickedCount++);

    public void RecordDismissed() => Bump(() => DismissedCount++);

    public void RecordArchived() => Bump(() => ArchivedCount++);

    public void RecordSuppressed() => Bump(() => SuppressedCount++);

    public void RecordDigested() => Bump(() => DigestedCount++);

    public void RecordFailed(int attempts)
    {
        FailedCount++;

        if (attempts > 1)
        {
            RetryCount += attempts - 1;
        }

        Revision++;
    }

    // ── Derived rates, computed rather than stored ──────────────────────────────────────────

    /// <summary>Delivered over queued. Zero when nothing was queued, never a division by zero.</summary>
    public double DeliveryRate => QueuedCount == 0 ? 0 : (double)DeliveredCount / QueuedCount;

    /// <summary>
    /// Opened over delivered.
    /// <para>
    /// Over delivered rather than over created, deliberately: a message the member never received
    /// cannot be opened, and including it would blame the wording for a provider outage.
    /// </para>
    /// </summary>
    public double OpenRate => DeliveredCount == 0 ? 0 : (double)OpenedCount / DeliveredCount;

    /// <summary>Clicked over opened. The measure of whether the message was worth acting on.</summary>
    public double ClickThroughRate => OpenedCount == 0 ? 0 : (double)ClickedCount / OpenedCount;

    public double DismissRate => DeliveredCount == 0 ? 0 : (double)DismissedCount / DeliveredCount;

    public double FailureRate =>
        QueuedCount == 0 ? 0 : (double)FailedCount / QueuedCount;

    /// <summary>Mean minutes to first open. See <see cref="MinutesToOpenTotal"/> for why it is derived.</summary>
    public double AverageMinutesToOpen =>
        OpenedCount == 0 ? 0 : MinutesToOpenTotal / OpenedCount;

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    private void Bump(Action increment)
    {
        increment();
        Revision++;
    }
}
