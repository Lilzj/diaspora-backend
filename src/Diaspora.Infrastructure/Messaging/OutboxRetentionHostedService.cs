using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Options;
using Diaspora.Platform.Messaging.Outbox.Telemetry;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Infrastructure.Messaging;

/// <summary>
/// Keeps the outbox tables from growing forever.
/// <para>
/// <b>The component every outbox implementation discovers it needed, about a year in.</b>
/// Delivered rows accumulate at exactly the rate the product succeeds, and nothing about them
/// looks wrong: every message was published, every consumer ran, every metric is green. The
/// first symptom is the claim query getting slower, and by then the table has tens of millions
/// of rows in it.
/// </para>
/// <para>
/// Separate from the dispatcher on purpose. This holds delete locks on the busiest table in the
/// platform, and sharing a loop would mean a long purge delayed delivery — the one thing the
/// dispatcher must not do. Running them independently means the sweep can be slow without
/// anyone noticing.
/// </para>
/// <para>
/// Dead letters are swept on a far longer horizon than delivered messages, and the options
/// validator refuses a configuration where that is not true. A dead letter purged before
/// someone replays it is a committed business event permanently lost, which is the exact
/// failure this phase was written to eliminate — reintroducing it through a retention setting
/// would be a poor joke.
/// </para>
/// </summary>
public sealed class OutboxRetentionHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxRetentionHostedService> _logger;

    public OutboxRetentionHostedService(
        IServiceScopeFactory scopes,
        IOptions<OutboxOptions> options,
        ILogger<OutboxRetentionHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retention = _options.Retention;

        if (!_options.Enabled || !retention.Enabled)
        {
            _logger.LogWarning(
                "Outbox retention is disabled. The outbox tables will grow without bound.");
            return;
        }

        // Deliberately after the dispatcher's startup delay. A cold start should deliver before
        // it tidies.
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(retention.SweepIntervalMinutes));

        do
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // A failed sweep must not stop the loop.
            catch (Exception exception)
            {
                // Nothing about a failed sweep is urgent — the rows it did not collect are
                // expensive, not wrong — and the next pass takes another bite.
                _logger.LogError(exception, "The outbox retention sweep failed; it will run again.");
            }
#pragma warning restore CA1031
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>One pass over all three populations. Public so a test can drive it directly.</summary>
    public async Task SweepAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopes.CreateScope();

        var maintenance = scope.ServiceProvider.GetRequiredService<IOutboxMaintenance>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var retention = _options.Retention;
        var now = clock.UtcNow;

        var delivered = await PurgeAsync(
            "delivered",
            cutoff => maintenance.PurgeDeliveredAsync(cutoff, retention.PurgeBatchSize, cancellationToken),
            now.AddHours(-retention.DeliveredRetentionHours),
            cancellationToken);

        var deadLetters = await PurgeAsync(
            "dead-letter",
            cutoff => maintenance.PurgeDeadLettersAsync(cutoff, retention.PurgeBatchSize, cancellationToken),
            now.AddHours(-retention.DeadLetterRetentionHours),
            cancellationToken);

        var attempts = await PurgeAsync(
            "attempt",
            cutoff => maintenance.PurgeAttemptsAsync(cutoff, retention.PurgeBatchSize, cancellationToken),
            now.AddHours(-retention.AttemptRetentionHours),
            cancellationToken);

        if (delivered + deadLetters + attempts > 0)
        {
            _logger.LogInformation(
                "Outbox retention swept {Delivered} delivered message(s), {DeadLetters} dead " +
                "letter(s) and {Attempts} attempt record(s).",
                delivered, deadLetters, attempts);
        }

        if (deadLetters > 0)
        {
            // Warning, separately. These are committed business events that were never
            // delivered and are now unrecoverable, and the retention window expiring is the
            // last moment anyone could have known.
            _logger.LogWarning(
                "{Count} dead letter(s) passed their {Hours}h retention window and were removed. " +
                "Those events were never delivered and can no longer be replayed.",
                deadLetters, retention.DeadLetterRetentionHours);
        }
    }

    /// <summary>
    /// Deletes in bounded batches until a batch comes back short.
    /// <para>
    /// Capped per sweep, so a first run against a table that accumulated for a month takes a
    /// series of short locks across several sweeps rather than one lock for an hour. Whatever
    /// is left is collected next time.
    /// </para>
    /// </summary>
    private async Task<int> PurgeAsync(
        string population,
        Func<DateTimeOffset, Task<int>> purge,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        var retention = _options.Retention;
        var total = 0;

        for (var batch = 0; batch < retention.MaxBatchesPerSweep; batch++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var removed = await purge(cutoff);

            total += removed;

            if (removed < retention.PurgeBatchSize)
            {
                break;
            }
        }

        if (total > 0)
        {
            OutboxTelemetry.Purged.Add(
                total, new KeyValuePair<string, object?>("population", population));
        }

        return total;
    }
}
