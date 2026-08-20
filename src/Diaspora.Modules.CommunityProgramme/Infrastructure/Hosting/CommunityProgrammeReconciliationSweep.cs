using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Modules.CommunityProgramme.Options;
using Diaspora.Platform.Operations.Scheduling;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.CommunityProgramme.Infrastructure.Hosting;

/// <summary>
/// Recounts the module's counters from the rows that produce them.
/// <para>
/// <b>A recount rather than a delta, and that is the whole reason it exists.</b> Every counter in
/// this module — a tier's filled places, an opportunity's, a shift's, a poll's response total — is a
/// number some other write moved. A sweep applying deltas would compound whatever drift it
/// inherited; a recount is idempotent and repairs whatever went wrong earlier, which is what makes it
/// safe to have been switched off for a week.
/// </para>
/// <para>
/// <b>Concurrency-safe through B17's lock, exactly as the lifecycle sweep is.</b> Two instances
/// recounting at once would not corrupt anything — a recount is not a delta — but they would both
/// write, and the second's optimistic-concurrency failure would fill a log with noise nobody can act
/// on.
/// </para>
/// </summary>
public sealed class CommunityProgrammeReconciliationSweep : BackgroundService
{
    private const string LockKey = "community-programme:reconciliation";

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<CommunityProgrammeOptions> _options;
    private readonly ILogger<CommunityProgrammeReconciliationSweep> _logger;

    public CommunityProgrammeReconciliationSweep(
        IServiceScopeFactory scopes,
        IOptions<CommunityProgrammeOptions> options,
        ILogger<CommunityProgrammeReconciliationSweep> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.EnableReconciliationSweep)
        {
            _logger.LogInformation(
                "CommunityProgrammeReconciliationSweep is disabled by configuration.");

            return;
        }

        var interval = TimeSpan.FromMinutes(
            Math.Max(5, _options.Value.ReconciliationSweepMinutes));

        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>One pass. Public so a test can drive it deterministically.</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();

        var locks = scope.ServiceProvider.GetService<IDistributedLock>();

        var lease = locks is null
            ? null
            : await locks.TryAcquireAsync(
                LockKey,
                TimeSpan.FromMinutes(Math.Max(6, _options.Value.ReconciliationSweepMinutes + 1)),
                cancellationToken);

        if (locks is not null && lease is null)
        {
            _logger.LogDebug(
                "Another instance holds the Community Programme reconciliation lease.");

            return;
        }

        try
        {
            await ReconcileAsync(scope.ServiceProvider, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "The Community Programme reconciliation sweep failed. It will run again.");
        }
        finally
        {
            if (lease is not null)
            {
                await lease.DisposeAsync();
            }
        }
    }

    private async Task ReconcileAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var clock = services.GetRequiredService<IClock>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var programmes = services.GetRequiredService<IMembershipProgrammeRepository>();
        var memberships = services.GetRequiredService<IProgrammeMembershipRepository>();

        var now = clock.UtcNow;
        var batch = _options.Value.SweepBatchSize;

        var organizationIds = await memberships.FindOrganizationsWithMembershipsAsync(
            batch, cancellationToken);

        var reconciled = 0;

        foreach (var organizationId in organizationIds)
        {
            var programme = await programmes.FindByOrganizationAsync(
                organizationId, cancellationToken);

            if (programme is null)
            {
                continue;
            }

            var byTier = await memberships.CountByTierAsync(organizationId, cancellationToken);
            var byStatus = await memberships.CountByStatusAsync(organizationId, cancellationToken);

            var active = byStatus.TryGetValue(
                Domain.Enums.ProgrammeMembershipStatus.Active, out var activeCount)
                    ? activeCount
                    : 0;

            var renewing = byStatus.TryGetValue(
                Domain.Enums.ProgrammeMembershipStatus.RenewalDue, out var renewalCount)
                    ? renewalCount
                    : 0;

            // Active and RenewalDue both, because a member inside their grace period has not stopped
            // being a member — the same rule the dashboard's count and the benefits check apply.
            programme.Reconcile(active + renewing, byTier, now);
            reconciled++;
        }

        if (reconciled == 0)
        {
            return;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Community Programme reconciliation recounted {Count} programme(s).", reconciled);
    }
}
