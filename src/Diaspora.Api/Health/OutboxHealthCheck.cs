using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Options;
using Diaspora.Platform.Messaging.Outbox.Statistics;
using Diaspora.Platform.Messaging.Outbox.Telemetry;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Diaspora.Api.Health;

/// <summary>
/// Reports whether committed events are actually going out.
/// <para>
/// <b>Depth is the wrong signal on its own, and it is the one everybody reaches for.</b> A queue
/// of ten thousand draining at full speed is a busy platform; a queue of one that has not moved
/// since lunchtime is a stopped dispatcher, and an incident. Only the age of the oldest
/// undelivered message tells them apart, which is why it carries the tighter thresholds and why
/// depth is here mostly as a secondary signal.
/// </para>
/// <para>
/// <b>Dead letters never fail readiness.</b> They degrade it. A dead letter means delivery for
/// that message has already stopped, and taking the instance out of rotation cannot restart it —
/// it would remove capacity from a platform that still has every other message to deliver. The
/// signal belongs on a dashboard and an alert, not on a load balancer's decision.
/// </para>
/// <para>
/// Reads the shared snapshot the dispatcher refreshes, and only queries the database when that
/// snapshot has gone stale. Every probe in the cluster hits this endpoint every few seconds;
/// running five aggregates over the hot table each time would make the health check the reason
/// the outbox was unhealthy.
/// </para>
/// </summary>
public sealed class OutboxHealthCheck : IHealthCheck
{
    private readonly OutboxStatisticsSnapshot _snapshot;
    private readonly IOutboxQueries _queries;
    private readonly IOutboxTransport _transport;
    private readonly IClock _clock;
    private readonly OutboxOptions _options;

    public OutboxHealthCheck(
        OutboxStatisticsSnapshot snapshot,
        IOutboxQueries queries,
        IOutboxTransport transport,
        IClock clock,
        IOptions<OutboxOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _snapshot = snapshot;
        _queries = queries;
        _transport = transport;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            // Not "healthy". The platform is running without the durability guarantee the rest
            // of it assumes, and a readiness payload that said "Healthy" would be the same kind
            // of lie the B2 readiness check told.
            return HealthCheckResult.Degraded(
                "The outbox is disabled. Integration events are published in-process and are " +
                "lost if a publish fails.");
        }

        var now = _clock.UtcNow;
        var maxAge = TimeSpan.FromSeconds(_options.Health.SnapshotCacheSeconds);

        if (_snapshot.IsStale(now, maxAge))
        {
            // Stale on an instance that does not dispatch — an API pod in a split deployment —
            // is the normal case, not a fault. It reads for itself and caches the result.
            _snapshot.Update(await _queries.GetStatisticsAsync(now, cancellationToken));
        }

        return Evaluate(_snapshot.Current, _options.Health, _transport.Name);
    }

    private static HealthCheckResult Evaluate(
        OutboxStatistics statistics, OutboxHealthOptions thresholds, string transport)
    {
        var oldestSeconds = statistics.OldestPendingAge.TotalSeconds;

        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["transport"] = transport,
            ["pending"] = statistics.Pending,
            ["claimed"] = statistics.Claimed,
            ["retrying"] = statistics.Retrying,
            ["deadLettered"] = statistics.DeadLettered,
            ["oldestPendingSeconds"] = Math.Round(oldestSeconds, 1),
            ["observedAt"] = statistics.ObservedAt,
            ["byModule"] = statistics.ByModule
                .Where(module => module.Pending > 0 || module.DeadLettered > 0)
                .ToDictionary(
                    module => module.Module,
                    module => (object)new { module.Pending, module.DeadLettered },
                    StringComparer.Ordinal)
        };

        if (oldestSeconds >= thresholds.UnhealthyOldestPendingSeconds)
        {
            return HealthCheckResult.Unhealthy(
                $"The oldest undelivered message has been waiting {oldestSeconds:N0}s, past the " +
                $"{thresholds.UnhealthyOldestPendingSeconds}s ceiling. Dispatch has stopped or " +
                "cannot keep up.",
                data: data);
        }

        if (statistics.Pending >= thresholds.UnhealthyDepth)
        {
            return HealthCheckResult.Unhealthy(
                $"{statistics.Pending:N0} messages are undelivered, past the " +
                $"{thresholds.UnhealthyDepth:N0} ceiling.",
                data: data);
        }

        if (oldestSeconds >= thresholds.DegradedOldestPendingSeconds)
        {
            return HealthCheckResult.Degraded(
                $"The oldest undelivered message has been waiting {oldestSeconds:N0}s. Delivery " +
                "is falling behind.",
                data: data);
        }

        if (statistics.Pending >= thresholds.DegradedDepth)
        {
            return HealthCheckResult.Degraded(
                $"{statistics.Pending:N0} messages are undelivered.", data: data);
        }

        if (statistics.DeadLettered >= thresholds.DegradedDeadLetters)
        {
            return HealthCheckResult.Degraded(
                $"{statistics.DeadLettered:N0} message(s) are in the dead-letter queue. They are " +
                "committed events that were never delivered and need a replay or a skip.",
                data: data);
        }

        return HealthCheckResult.Healthy(
            statistics.Pending == 0
                ? "The outbox is empty."
                : $"{statistics.Pending:N0} message(s) pending, oldest {oldestSeconds:N0}s.",
            data);
    }
}
