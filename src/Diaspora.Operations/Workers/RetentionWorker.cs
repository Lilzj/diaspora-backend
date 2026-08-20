using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Recovery;
using Diaspora.Platform.Operations.Scheduling;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Diaspora.Operations.Workers;

/// <summary>
/// Applies every module's retention policy.
/// <para>
/// The cutoff comes from the <see cref="RecoveryPolicy"/> the sweeper names, not from the sweeper. That
/// separation is what makes retention auditable: a data set's retention period is declared once, in the
/// policy catalogue, where the privacy audit can read it — rather than as a constant inside whichever
/// sweep happens to enforce it.
/// </para>
/// <para>
/// <b>Runs under a distributed lease.</b> Retention is the one sweep where several instances doing the
/// same work is not merely wasteful: three instances each deleting a batch of the same rows is three
/// times the lock contention on a statement that is already the heaviest thing the platform runs. The
/// shipped lock is per-process (DEBT-015), so today the lease only serializes within an instance — the
/// call site is correct and the implementation is the part waiting on a registration.
/// </para>
/// </summary>
public sealed class RetentionWorker : PlatformWorker
{
    /// <summary>Rows per sweeper per pass. A retention sweep that deletes a year in one statement is an outage.</summary>
    private const int BatchSize = 500;

    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(10);

    private readonly IServiceProvider _services;
    private readonly IDistributedLock _lock;
    private readonly IClock _clock;

    public RetentionWorker(
        IServiceProvider services,
        IDistributedLock distributedLock,
        IOperationalPolicyCatalog policies,
        IClock clock,
        ILogger<RetentionWorker> logger)
        : base("retention", "Platform", policies, logger)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(distributedLock);
        ArgumentNullException.ThrowIfNull(clock);

        _services = services;
        _lock = distributedLock;
        _clock = clock;
    }

    protected override TimeSpan Interval => TimeSpan.FromHours(1);

    protected override async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var lease = await _lock
            .TryAcquireAsync("platform.retention", LeaseDuration, cancellationToken)
            .ConfigureAwait(false);

        if (lease is null)
        {
            Logger.LogDebug("Another holder has the retention lease. Skipping this pass.");
            return 0;
        }

        using var scope = _services.CreateScope();

        var sweepers = scope.ServiceProvider.GetServices<IRetentionSweeper>().ToArray();

        if (sweepers.Length == 0)
        {
            return 0;
        }

        var swept = 0;

        foreach (var sweeper in sweepers)
        {
            var policy = Policies.Find<RecoveryPolicy>(sweeper.PolicyName);

            if (policy?.RetentionPeriod is not { } retention)
            {
                // Reported at warning rather than skipped quietly. A sweeper whose policy declares no
                // retention period does nothing, forever, and the data it was written to age out is
                // kept indefinitely — which is exactly the finding the privacy audit exists to surface.
                Logger.LogWarning(
                    "The retention sweeper for {Module}/{DataSet} names policy '{Policy}', which " +
                    "declares no retention period. Nothing will be aged out.",
                    sweeper.Module, sweeper.DataSet, sweeper.PolicyName);

                continue;
            }

            try
            {
                var removed = await sweeper
                    .SweepAsync(_clock.UtcNow - retention, BatchSize, cancellationToken)
                    .ConfigureAwait(false);

                swept += removed;

                if (removed > 0)
                {
                    Logger.LogInformation(
                        "Retention swept {Count} record(s) from {Module}/{DataSet} older than {Retention}.",
                        removed, sweeper.Module, sweeper.DataSet, retention);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    exception,
                    "The retention sweeper for {Module}/{DataSet} failed. Other sweepers continue.",
                    sweeper.Module, sweeper.DataSet);
            }

            // Renewed between sweepers rather than once at the start. A pass over a dozen data sets can
            // outlive the lease, and a lease that lapsed mid-pass would let a second instance start
            // deleting the same rows this one is still working through.
            if (!await lease.RenewAsync(LeaseDuration, cancellationToken).ConfigureAwait(false))
            {
                Logger.LogWarning(
                    "The retention lease was lost mid-pass. Stopping; another holder has taken over.");

                break;
            }
        }

        return swept;
    }
}
