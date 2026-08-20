using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
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
/// Moves what the clock decides: memberships into their renewal window and out of it, polls open and
/// closed, opportunities past their deadline.
/// <para>
/// <b>Four facts nobody decides, and the reason this exists.</b> A member does not choose to reach
/// their renewal date, and a poll does not close itself. Every state this sweep names is one the
/// domain already has a transition for; the sweep supplies the moment, and nothing else.
/// </para>
/// <para>
/// <b>Idempotent, retry-safe and concurrency-safe, in that order.</b> Idempotent because every
/// transition is a no-op when the aggregate is already there — a second pass over a membership it
/// already expired produces one history entry, not two. Retry-safe because each batch is its own unit
/// of work and a failure loses only that batch's work, which the next pass redoes. Concurrency-safe
/// because it takes B17's <see cref="IDistributedLock"/> before touching anything, so two instances
/// do not both expire the same membership and send two notifications.
/// </para>
/// <para>
/// <b>The lock is B17's seam and is honest about what it is today.</b> <c>InProcessDistributedLock</c>
/// is correct within one process and nothing more — DEBT-015 is open and this sweep does not close it.
/// What matters is that the sweep is <em>written against</em> a lock, so the day a store-backed
/// implementation is registered this becomes safe across instances with no edit here. The alternative
/// — a sweep with no lock at all — would have to be found and rewritten.
/// </para>
/// <para>
/// <b>No scheduler of its own.</b> B17 ships <see cref="IBackgroundJobScheduler"/> and its
/// <c>InMemoryJobScheduler</c> is explicit that a pending job does not survive a restart; work that
/// must happen goes through the outbox instead. A sweep is neither: it is a periodic reconciliation
/// whose whole design is that missing a pass costs nothing, because the next pass finds the same
/// rows. A <c>BackgroundService</c> with a <c>PeriodicTimer</c> is the shape every other sweep on
/// this platform uses, and using a different one here would be a second pattern for one behaviour.
/// </para>
/// </summary>
public sealed class CommunityProgrammeLifecycleSweep : BackgroundService
{
    private const string LockKey = "community-programme:lifecycle";

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<CommunityProgrammeOptions> _options;
    private readonly ILogger<CommunityProgrammeLifecycleSweep> _logger;

    public CommunityProgrammeLifecycleSweep(
        IServiceScopeFactory scopes,
        IOptions<CommunityProgrammeOptions> options,
        ILogger<CommunityProgrammeLifecycleSweep> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.EnableLifecycleSweep)
        {
            _logger.LogInformation(
                "CommunityProgrammeLifecycleSweep is disabled by configuration.");

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
    /// after a timer happens to have fired is a flaky test wearing a passing badge; the integration
    /// suite calls this and gets an answer.
    /// </para>
    /// </summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();

        var locks = scope.ServiceProvider.GetService<IDistributedLock>();

        // A lease for slightly longer than the interval, so a pass that overruns is not overtaken by
        // the next one. Released by the using, and expiring on its own if the process dies mid-pass.
        var lease = locks is null
            ? null
            : await locks.TryAcquireAsync(
                LockKey,
                TimeSpan.FromMinutes(Math.Max(2, _options.Value.LifecycleSweepMinutes + 1)),
                cancellationToken);

        if (locks is not null && lease is null)
        {
            _logger.LogDebug("Another instance holds the Community Programme lifecycle lease.");

            return;
        }

        try
        {
            await SweepAsync(scope.ServiceProvider, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A failed pass is not a failed platform. The next pass finds the same rows, which is the
            // property that makes a reconciliation sweep the right shape for this work.
            _logger.LogError(
                exception, "The Community Programme lifecycle sweep failed. It will run again.");
        }
        finally
        {
            if (lease is not null)
            {
                await lease.DisposeAsync();
            }
        }
    }

    private async Task SweepAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var clock = services.GetRequiredService<IClock>();
        var guids = services.GetRequiredService<IGuidGenerator>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var memberships = services.GetRequiredService<IProgrammeMembershipRepository>();
        var polls = services.GetRequiredService<ICommunityPollRepository>();
        var opportunities = services.GetRequiredService<IVolunteerOpportunityRepository>();

        var now = clock.UtcNow;
        var batch = _options.Value.SweepBatchSize;
        var moved = 0;

        // ── Memberships ────────────────────────────────────────────────────────────────────
        //
        // Expiry first, then renewal-due. The order matters: a membership past both dates should end
        // up Expired rather than pausing at RenewalDue for one interval, and running expiry first
        // means the renewal pass no longer finds it.
        foreach (var membership in await memberships.FindExpiredAsync(now, batch, cancellationToken))
        {
            var result = membership.MoveTo(
                ProgrammeMembershipStatus.Expired,
                guids.NewSequentialGuid(),
                now,
                MembershipActor.System);

            if (result.IsSuccess)
            {
                moved++;
            }
        }

        foreach (var membership in await memberships.FindDueForRenewalAsync(
            now, batch, cancellationToken))
        {
            var result = membership.MoveTo(
                ProgrammeMembershipStatus.RenewalDue,
                guids.NewSequentialGuid(),
                now,
                MembershipActor.System);

            if (result.IsSuccess)
            {
                moved++;
            }
        }

        // ── Polls ──────────────────────────────────────────────────────────────────────────

        foreach (var poll in await polls.FindDueToOpenAsync(now, batch, cancellationToken))
        {
            if (poll.MoveTo(PollStatus.Open, now).IsSuccess)
            {
                moved++;
            }
        }

        foreach (var poll in await polls.FindDueToCloseAsync(now, batch, cancellationToken))
        {
            if (poll.MoveTo(PollStatus.Closed, now).IsSuccess)
            {
                moved++;
            }
        }

        // ── Opportunities ──────────────────────────────────────────────────────────────────

        foreach (var opportunity in await opportunities.FindPastDeadlineAsync(
            now, batch, cancellationToken))
        {
            if (opportunity.MoveTo(VolunteerOpportunityStatus.Closed, now).IsSuccess)
            {
                moved++;
            }
        }

        if (moved == 0)
        {
            return;
        }

        // One save for the whole pass. Every transition raised its domain event, and the platform's
        // dispatcher relays them after this commits — so a pass that fails to save announces nothing,
        // which is the outcome wanted.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Community Programme lifecycle sweep moved {Moved} subject(s).", moved);
    }
}
