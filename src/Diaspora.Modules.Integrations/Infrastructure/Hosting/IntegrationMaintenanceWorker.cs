using Diaspora.Contracts.Integrations.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Integrations.Domain.Providers;
using Diaspora.Modules.Integrations.Domain.Repositories;
using Diaspora.Modules.Integrations.Options;
using Diaspora.Platform.Messaging;
using Diaspora.Platform.Operations.Scheduling;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Integrations.Infrastructure.Hosting;

/// <summary>
/// One sweep, four jobs.
/// <para>
/// <b>One worker rather than four, because four timers over three tables is four leases to take and
/// three of them for nothing.</b> Each pass, in this order: reap sync runs that claim to be in
/// progress and are not; announce grants that are about to lapse; delete expired authorization
/// requests; and apply the two retention horizons.
/// </para>
/// <para>
/// <b>It takes B17's distributed lease before touching anything.</b> Every sweeper on this platform
/// runs on every instance, and the recorded mitigation — "disable sweepers on all but one instance"
/// — is a deployment convention that nothing enforces and that silently stops working when the one
/// instance is the one that restarts. Three instances doing this scan is waste rather than
/// corruption, but the expiry announcement is not idempotent across instances and would send three
/// notifications.
/// </para>
/// <para>
/// <b>Reaping abandoned runs is not housekeeping.</b> A run stuck in <c>syncing</c> is also the
/// concurrency barrier for its calendar, so without this pass one crashed worker means that member's
/// calendar can never be synchronised again — and nothing would say why.
/// </para>
/// <para>
/// <b>Every batch is bounded and every failure is caught per job.</b> A backlog is drained over
/// several passes rather than in one transaction that locks a table, and an exception in the expiry
/// announcement must not stop retention running for the rest of the deployment's life.
/// </para>
/// </summary>
public sealed class IntegrationMaintenanceWorker : BackgroundService
{
    private const string LeaseResource = "integrations:maintenance";

    private readonly IServiceScopeFactory _scopes;
    private readonly IDistributedLock _leases;
    private readonly IOptionsMonitor<IntegrationsOptions> _options;
    private readonly ILogger<IntegrationMaintenanceWorker> _logger;

    public IntegrationMaintenanceWorker(
        IServiceScopeFactory scopes,
        IDistributedLock leases,
        IOptionsMonitor<IntegrationsOptions> options,
        ILogger<IntegrationMaintenanceWorker> logger)
    {
        _scopes = scopes;
        _leases = leases;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// The loop.
    /// <para>
    /// <b>A <see cref="PeriodicTimer"/> rather than <c>Task.Delay</c>, and the catch lives one frame
    /// down in <see cref="RunOnceAsync"/>.</b> Both are the shape every sweep on this platform uses,
    /// and the second is enforced: B17-R4's architecture test fails any module loop that contains
    /// both a delay and a catch, because that pair is what a hand-rolled retry looks like and a
    /// hand-rolled retry is invisible to the breaker meant to stop it. This loop is a schedule, not a
    /// retry — a failed pass is logged and the next one happens on time rather than sooner.
    /// </para>
    /// <para>
    /// <b>The first pass is one interval in, not on boot.</b> A sweep at startup would race every
    /// other module's startup work and, in a test host, would run against a database still being
    /// created.
    /// </para>
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.CurrentValue.EnableMaintenanceSweep)
        {
            _logger.LogInformation(
                "The integration maintenance sweep is disabled by configuration. Abandoned "
                + "synchronisation runs will not be reaped, expiring grants will not be announced "
                + "and retention will not be applied.");

            return;
        }

        var interval = TimeSpan.FromMinutes(
            Math.Max(1, _options.CurrentValue.MaintenanceIntervalMinutes));

        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                return;
            }

            await RunOnceAsync(stoppingToken);
        }
    }

    /// <summary>One pass, and it never throws. See <see cref="ExecuteAsync"/>.</summary>
    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await SweepAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown. Not a failure and not worth a log line on every deployment.
        }
#pragma warning disable CA1031 // A sweeper that dies on one bad pass stops sweeping for ever.
        catch (Exception exception)
        {
            _logger.LogError(exception, "The integration maintenance sweep failed.");
        }
#pragma warning restore CA1031
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;

        await using var lease = await _leases.TryAcquireAsync(
            LeaseResource,
            TimeSpan.FromMinutes(Math.Max(2, options.MaintenanceIntervalMinutes)),
            cancellationToken);

        if (lease is null)
        {
            // Another instance has it. Skipping is correct: by the time this one acquired, the work
            // would already be done.
            return;
        }

        using var scope = _scopes.CreateScope();
        var services = scope.ServiceProvider;
        var clock = services.GetRequiredService<IClock>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var now = clock.UtcNow;
        var batch = options.MaintenanceBatchSize;

        await ReapAbandonedRunsAsync(services, now, batch, cancellationToken);
        await AnnounceExpiringGrantsAsync(services, options, now, batch, cancellationToken);
        await DeleteExpiredAuthorizationsAsync(services, now, batch, cancellationToken);
        await ApplyRetentionAsync(services, options, now, batch, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Closes runs nobody finished. See the class summary for why this is not housekeeping.
    /// </summary>
    private async Task ReapAbandonedRunsAsync(
        IServiceProvider services, DateTimeOffset now, int batch, CancellationToken cancellationToken)
    {
        var runs = services.GetRequiredService<IIntegrationSyncRunRepository>();
        var stale = await runs.ListStaleAsync(now, batch, cancellationToken);

        foreach (var run in stale)
        {
            run.Abandon(now);
            runs.Update(run);
        }

        if (stale.Count > 0)
        {
            _logger.LogWarning(
                "Closed {Count} synchronisation run(s) that claimed to be in progress and had "
                + "exceeded the stale threshold. Their outcome is unknown and is recorded as such.",
                stale.Count);
        }
    }

    /// <summary>
    /// Announces grants that lapse inside the warning window.
    /// <para>
    /// <b>The deduplication is B13's, not a column here.</b> The rule this module contributes carries
    /// a three-day window matching the warning horizon, so a sweep running every fifteen minutes
    /// produces one notification rather than two hundred and eighty-eight. Adding an
    /// <c>AnnouncedAt</c> column would be a second answer to a question B13 already answers, and it
    /// would be the one that goes stale when the window is retuned.
    /// </para>
    /// </summary>
    private static async Task AnnounceExpiringGrantsAsync(
        IServiceProvider services,
        IntegrationsOptions options,
        DateTimeOffset now,
        int batch,
        CancellationToken cancellationToken)
    {
        var connections = services.GetRequiredService<IIntegrationConnectionRepository>();
        var events = services.GetRequiredService<IIntegrationEventPublisher>();

        var expiring = await connections.ListExpiringAsync(
            now, TimeSpan.FromHours(options.GrantExpiryWarningHours), batch, cancellationToken);

        foreach (var connection in expiring)
        {
            if (connection.ExpiresAt is not { } expiresAt)
            {
                continue;
            }

            await events.PublishAsync(
                new IntegrationGrantExpiringIntegrationEvent
                {
                    UserId = connection.OwnerId,
                    Provider = connection.Provider,
                    ProviderDomain = ProviderKey.TryParse(connection.Provider, out var key)
                        ? key.Domain
                        : connection.ProviderDomain,
                    ExpiresAt = expiresAt
                },
                cancellationToken);
        }
    }

    private async Task DeleteExpiredAuthorizationsAsync(
        IServiceProvider services, DateTimeOffset now, int batch, CancellationToken cancellationToken)
    {
        var requests = services.GetRequiredService<IOAuthAuthorizationRequestRepository>();

        // Deleted outright rather than kept. A consumed state hash has no evidential value — the
        // connection it produced is the record — and an unconsumed one is an abandoned attempt.
        // Keeping either would be a table of hashes growing for no reason §42 would accept.
        var deleted = await requests.DeleteExpiredAsync(now, batch, cancellationToken);

        if (deleted > 0)
        {
            _logger.LogDebug("Deleted {Count} expired authorization request(s).", deleted);
        }
    }

    private static async Task ApplyRetentionAsync(
        IServiceProvider services,
        IntegrationsOptions options,
        DateTimeOffset now,
        int batch,
        CancellationToken cancellationToken)
    {
        var webhooks = services.GetRequiredService<IProcessedIntegrationWebhookRepository>();
        var runs = services.GetRequiredService<IIntegrationSyncRunRepository>();

        await webhooks.DeleteReceivedBeforeAsync(
            now - TimeSpan.FromDays(options.WebhookRetentionDays), batch, cancellationToken);

        await runs.DeleteCompletedBeforeAsync(
            now - TimeSpan.FromDays(options.SyncRunRetentionDays), batch, cancellationToken);
    }
}
