using Diaspora.Platform.Messaging.Outbox.Statistics;

namespace Diaspora.Platform.Messaging.Outbox.Telemetry;

/// <summary>
/// The most recent statistics reading, shared between the dispatcher that refreshes it and
/// everything that polls: metric gauges, health checks and the administration API.
/// <para>
/// A singleton holding one immutable record. The alternative — every reader running its own
/// aggregates — means a health probe every two seconds from every pod, a metrics scrape every
/// fifteen from every pod, and all of them counting the same table. Under the exact conditions
/// where the outbox is deep and the answers matter most, that load lands on the database that
/// is already struggling.
/// </para>
/// <para>
/// Reads are lock-free by construction: the field holds an immutable record and is replaced
/// wholesale, so a reader sees either the old snapshot or the new one and never a half-written
/// mix.
/// </para>
/// </summary>
public sealed class OutboxStatisticsSnapshot
{
    private volatile OutboxStatistics _current = OutboxStatistics.Empty;

    /// <summary>The last reading. Never null; <see cref="OutboxStatistics.Empty"/> before the first refresh.</summary>
    public OutboxStatistics Current => _current;

    /// <summary>Whether anything has been observed yet.</summary>
    public bool HasReading => _current.ObservedAt != default;

    public void Update(OutboxStatistics statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        _current = statistics;
    }

    /// <summary>
    /// Whether the snapshot is older than <paramref name="maxAge"/> and should be refreshed
    /// before being trusted.
    /// </summary>
    public bool IsStale(DateTimeOffset now, TimeSpan maxAge) =>
        !HasReading || now - _current.ObservedAt > maxAge;
}
