using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Domain.Inboxes;
using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.Modules.Notifications.Observability;
using Diaspora.Modules.Notifications.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Infrastructure.Hosting;

/// <summary>
/// Recounts inbox counters and repairs whatever drifted.
/// <para>
/// <b>This sweep is the honest half of the decision to maintain counters rather than compute
/// them.</b> A maintained counter is only correct if every write path settles it, and a platform
/// that promised that would be a platform whose badge is quietly wrong for a subset of members
/// forever. A periodic recount costs a handful of aggregate queries per member per half-day and makes
/// the drift bounded and measurable — <c>CounterDriftRepaired</c> is the number that says whether a
/// write path was added without a settlement.
/// </para>
/// </summary>
public sealed class InboxReconciliationSweep : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<NotificationOptions> _options;
    private readonly ILogger<InboxReconciliationSweep> _logger;

    public InboxReconciliationSweep(
        IServiceScopeFactory scopes,
        IOptions<NotificationOptions> options,
        ILogger<InboxReconciliationSweep> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;

        if (!options.EnableCounterReconciliation)
        {
            return;
        }

        using var timer = new PeriodicTimer(
            TimeSpan.FromMinutes(options.ReconciliationSweepMinutes));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    /// <summary>Recounts one batch and returns how many counters it had to correct.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var services = scope.ServiceProvider;

            var inboxes = services.GetRequiredService<IUserInboxRepository>();
            var notifications = services.GetRequiredService<INotificationRepository>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();
            var guids = services.GetRequiredService<IGuidGenerator>();
            var clock = services.GetRequiredService<IClock>();
            var options = _options.Value;

            var stale = await inboxes.ListForReconciliationAsync(
                clock.UtcNow.AddMinutes(-options.ReconciliationSweepMinutes),
                options.ReconciliationBatchSize,
                cancellationToken);

            var repaired = 0;

            foreach (var inbox in stale)
            {
                var measured = await notifications.CountForReconciliationAsync(
                    inbox.UserId, cancellationToken);

                if (measured.Unread != inbox.UnreadCount || measured.Total != inbox.TotalCount)
                {
                    repaired++;
                }

                inbox.Reconcile(
                    measured.Unread,
                    measured.Total,
                    measured.Archived,
                    measured.Pinned,
                    measured.Starred,
                    measured.ByCategory.ToDictionary(
                        entry => entry.Key,
                        entry => (entry.Value.Unread, entry.Value.Total),
                        StringComparer.Ordinal),
                    _ => guids.NewSequentialGuid());

                inboxes.Update(inbox);
            }

            if (stale.Count > 0)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            if (repaired > 0)
            {
                NotificationTelemetry.CounterDriftRepaired.Add(repaired);

                // Warning rather than information: a non-zero count means a write path is not
                // settling its counters, which is a defect somebody should go and find.
                _logger.LogWarning(
                    "Repaired inbox counters for {Count} member(s). A write path may not be settling "
                    + "its counters.",
                    repaired);
            }

            return repaired;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception exception)
#pragma warning restore CA1031
        {
            _logger.LogError(exception, "An inbox reconciliation pass failed. The loop continues.");

            return 0;
        }
    }
}
