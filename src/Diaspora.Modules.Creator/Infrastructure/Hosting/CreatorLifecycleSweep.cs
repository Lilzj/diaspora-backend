using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Application.Services;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Modules.Creator.Options;
using Diaspora.Platform.Operations.Scheduling;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Creator.Infrastructure.Hosting;

/// <summary>
/// Moves what the clock decides: verifications that lapsed, referrals that ran out of window, and
/// sign-up bonuses nobody acknowledged.
/// <para>
/// <b>Three facts nobody decides.</b> An approval does not choose to reach its expiry, and a referral
/// does not choose to run out of days. Every state this sweep names is one the domain already has a
/// transition for; the sweep supplies the moment and nothing else.
/// </para>
/// <para>
/// <b>Idempotent, retry-safe and concurrency-safe, in that order.</b> Idempotent because every
/// transition is a no-op when the aggregate is already there. Retry-safe because a failed pass loses
/// only that pass's work and the next one finds the same rows. Concurrency-safe because it takes
/// B17's <see cref="IDistributedLock"/> before touching anything, so two instances do not both expire
/// the same approval and publish two decisions.
/// </para>
/// <para>
/// <b>The lock is B17's seam and is honest about what it is today.</b> <c>InProcessDistributedLock</c>
/// is correct within one process and nothing more — DEBT-015 is open and this sweep does not close
/// it. What matters is that the sweep is written <em>against</em> a lock, so a store-backed
/// implementation makes it safe across instances with no edit here.
/// </para>
/// <para>
/// <b>Off in the integration suite, and switchable from day one.</b> DEBT-049 recorded the cost of a
/// background loop querying the one shared SQLite connection mid-command three times over; the
/// switch exists before the first test rather than four phases in, and the suite drives
/// <see cref="RunOnceAsync"/> directly.
/// </para>
/// </summary>
public sealed class CreatorLifecycleSweep : BackgroundService
{
    private const string LockKey = "creator:lifecycle";
    private const int BatchSize = 200;

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<CreatorOptions> _options;
    private readonly ILogger<CreatorLifecycleSweep> _logger;

    public CreatorLifecycleSweep(
        IServiceScopeFactory scopes,
        IOptions<CreatorOptions> options,
        ILogger<CreatorLifecycleSweep> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.EnableLifecycleSweep)
        {
            _logger.LogInformation("CreatorLifecycleSweep is disabled by configuration.");

            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.Value.LifecycleSweepMinutes));

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

    /// <summary>
    /// One pass.
    /// <para>
    /// <b>Public so a test can drive it deterministically.</b> Asserting on a sweep's side effect
    /// after a timer happens to have fired is a flaky test wearing a passing badge.
    /// </para>
    /// </summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();

        var locks = scope.ServiceProvider.GetService<IDistributedLock>();

        var lease = locks is null
            ? null
            : await locks.TryAcquireAsync(
                LockKey,
                TimeSpan.FromMinutes(Math.Max(2, _options.Value.LifecycleSweepMinutes + 1)),
                cancellationToken);

        if (locks is not null && lease is null)
        {
            _logger.LogDebug("Another instance holds the Creator Platform lifecycle lease.");

            return;
        }

        try
        {
            await SweepAsync(scope.ServiceProvider, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A failed pass is not a failed platform. The next pass finds the same rows, which is
            // the property that makes a reconciliation sweep the right shape for this work.
            _logger.LogError(
                exception, "The Creator Platform lifecycle sweep failed. It will run again.");
        }
        finally
        {
            if (lease is not null)
            {
                await lease.DisposeAsync();
            }
        }
    }

    private static async Task SweepAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var clock = services.GetRequiredService<IClock>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var verifications = services.GetRequiredService<ICreatorVerificationRepository>();
        var referrals = services.GetRequiredService<ICreatorReferralRepository>();
        var creators = services.GetRequiredService<ICreatorProfileRepository>();
        var status = services.GetRequiredService<CreatorStatusService>();

        var now = clock.UtcNow;

        // ── Verifications ──────────────────────────────────────────────────────────────────
        //
        // An approval that lapsed. The status refresh that follows is what takes the standing away —
        // expiring the application without it would leave a Verified Creator whose approval ran out
        // a month ago.
        foreach (var application in
            await verifications.ListLapsedAsync(now, BatchSize, cancellationToken))
        {
            if (!application.IsApprovalLapsed(now))
            {
                continue;
            }

            var expired = application.Expire(now);

            if (expired.IsFailure)
            {
                continue;
            }

            verifications.Update(application);

            var creator = await creators.GetAsync(application.CreatorId, cancellationToken);

            if (creator is not null)
            {
                await status.RefreshAsync(creator, cancellationToken);
            }
        }

        // ── Referrals ──────────────────────────────────────────────────────────────────────
        //
        // A referral whose ninety days ran out without a contribution. Expired rather than rejected:
        // nothing was wrong, the window simply closed.
        foreach (var referral in await referrals.ListLapsedAsync(now, BatchSize, cancellationToken))
        {
            if (!referral.HasLapsed(now))
            {
                continue;
            }

            referral.Expire(now);
            referrals.UpdateReferral(referral);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
