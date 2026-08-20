using Diaspora.Operations.RateLimiting;
using Diaspora.Operations.Security;
using Diaspora.Operations.Telemetry;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Telemetry;
using Microsoft.Extensions.Logging;

namespace Diaspora.Operations.Workers;

/// <summary>
/// The Production Platform's own housekeeping: reclaim limiter partitions, and publish how many
/// secrets are due for rotation.
/// <para>
/// Both jobs are here rather than in their own workers because both are cheap, both run on the same
/// cadence, and a platform that answers every small periodic job with another background loop ends up
/// with twenty threads doing nothing most of the time.
/// </para>
/// </summary>
public sealed class OperationsMaintenanceWorker : PlatformWorker
{
    /// <summary>A limiter partition untouched for this long is reclaimed.</summary>
    private static readonly TimeSpan LimiterIdleWindow = TimeSpan.FromMinutes(15);

    /// <summary>How far ahead the secret gauge looks.</summary>
    private static readonly TimeSpan SecretExpiryWindow = TimeSpan.FromDays(14);

    private readonly PlatformRateLimiter _limiter;
    private readonly ConfigurationSecretStore _secrets;

    public OperationsMaintenanceWorker(
        PlatformRateLimiter limiter,
        ConfigurationSecretStore secrets,
        IOperationalPolicyCatalog policies,
        ILogger<OperationsMaintenanceWorker> logger)
        : base("operations-maintenance", "Platform", policies, logger)
    {
        ArgumentNullException.ThrowIfNull(limiter);
        ArgumentNullException.ThrowIfNull(secrets);

        _limiter = limiter;
        _secrets = secrets;

        // A gauge rather than a counter: the number of overdue secrets is a level, not an event, and a
        // counter would only ever go up.
        //
        // Registered once per process and reading static state, not a closure over this worker. The
        // Meter is a static singleton and holds every callback it is given — so a gauge registered per
        // instance leaks one dead worker per host, which is invisible in a deployed process and
        // unbounded in a test suite that builds a host per class.
        OperationsTelemetry.RegisterExpiringSecretsGauge();
    }

    protected override TimeSpan Interval => TimeSpan.FromMinutes(5);

    protected override async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var reclaimed = _limiter.Compact(LimiterIdleWindow);

        if (reclaimed > 0)
        {
            Logger.LogDebug(
                "Reclaimed {Count} idle rate-limit partition(s). {Remaining} remain.",
                reclaimed, _limiter.BucketCount);
        }

        var expiring = await _secrets
            .ExpiringWithinAsync(SecretExpiryWindow, cancellationToken)
            .ConfigureAwait(false);

        OperationsTelemetry.ReportExpiringSecrets(expiring.Count);

        foreach (var secret in expiring)
        {
            Logger.LogWarning(
                "Secret {Secret} (version {Version}) is due for rotation by {ExpiresAt:O}.",
                secret.Name, secret.Version, secret.ExpiresAt);
        }

        // Zero, always: this worker never drains a backlog, so returning the reclaimed count would make
        // the base class treat a large reclamation as "there is more to do" and skip its interval.
        return 0;
    }
}
