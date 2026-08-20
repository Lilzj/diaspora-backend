using Diaspora.Platform.Services.Abstractions;
using Diaspora.Platform.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Infrastructure.Transactions;

/// <summary>
/// Collects verification receipts once they can no longer answer anything.
/// <para>
/// A receipt is useful for the length of one execution-strategy retry window — seconds — and is
/// dead weight after that. On a retrying provider the table gains a row per transaction, which
/// makes it the fastest-growing table in the platform and the one that would be most embarrassing
/// to leave unattended.
/// </para>
/// <para>
/// Separate from the outbox's retention sweep on purpose, despite the similarity. The two tables
/// have nothing to do with each other, and coupling their schedules would mean a slow outbox purge
/// delaying the collection of rows that turn over a hundred times faster.
/// </para>
/// </summary>
public sealed class TransactionReceiptRetentionHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TransactionReliabilityOptions _options;
    private readonly ILogger<TransactionReceiptRetentionHostedService> _logger;

    public TransactionReceiptRetentionHostedService(
        IServiceScopeFactory scopes,
        IOptions<TransactionReliabilityOptions> options,
        ILogger<TransactionReceiptRetentionHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableReceiptRetentionSweep)
        {
            _logger.LogInformation(
                "TransactionReceiptRetentionHostedService is disabled by configuration.");

            return;
        }

        // After startup, so the first sweep does not compete with migrations and the opening burst
        // of traffic. Nothing here is urgent: the rows are cheap, they are just numerous.
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.SweepIntervalMinutes));

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
                _logger.LogError(
                    exception, "The transaction receipt sweep failed; it will run again.");
            }
#pragma warning restore CA1031
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>One pass. Public so a test can drive it directly.</summary>
    public async Task<int> SweepAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopes.CreateScope();

        var receipts = scope.ServiceProvider.GetRequiredService<ITransactionReceiptStore>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var cutoff = clock.UtcNow.AddMinutes(-_options.ReceiptRetentionMinutes).UtcTicks;

        var total = 0;

        for (var batch = 0; batch < _options.MaxBatchesPerSweep; batch++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var removed = await receipts.PurgeAsync(cutoff, _options.PurgeBatchSize, cancellationToken);

            total += removed;

            if (removed < _options.PurgeBatchSize)
            {
                break;
            }
        }

        if (total > 0)
        {
            TransactionTelemetry.ReceiptsPurged.Add(total);

            _logger.LogDebug("Collected {Count} spent transaction receipt(s).", total);
        }

        return total;
    }
}
