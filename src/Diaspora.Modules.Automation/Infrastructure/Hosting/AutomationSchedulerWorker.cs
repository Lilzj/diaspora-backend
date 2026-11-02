using Diaspora.Contracts.Automation.V1.Events;
using Diaspora.Modules.Automation.Application.Engine;
using Diaspora.Modules.Automation.Application.Services;
using Diaspora.Modules.Automation.Catalog;
using Diaspora.Modules.Automation.Domain.Catalog;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Repositories;
using Diaspora.Modules.Automation.Observability;
using Diaspora.Modules.Automation.Options;
using Diaspora.Platform.Messaging;
using Diaspora.Platform.Operations.Scheduling;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Automation.Infrastructure.Hosting;

/// <summary>
/// The sweep that makes time-based automation happen.
/// <para>
/// <b>Not a second scheduler.</b> B17's shipped <c>InMemoryJobScheduler</c> documents itself as
/// non-durable — "jobs do not survive a restart… anything that must happen goes through the outbox" —
/// and a member's 07:45 reminder must happen. What this owns is a durable due-store with a claim
/// protocol, which is precisely the half that implementation says it lacks. It registers no job type,
/// no cron expression and no handler dispatch, and it takes B17's distributed lease rather than
/// inventing a lock.
/// </para>
/// <para>
/// <b>Five passes, in an order that matters.</b> Due occurrences first, because they are the thing a
/// member is waiting for. Dated subjects next — Wave 12's horizon store — because a horizon that fires
/// may schedule a reminder, and one created this cycle should be swept by this cycle. Reminders third.
/// Stale runs fourth, so a run whose settlement never arrived stops sitting mid-flight. Retention last,
/// because it is the only one that can be skipped without anybody noticing.
/// </para>
/// <para>
/// <b>Every pass is idempotent and every pass is bounded.</b> A sweep that could not take the lease
/// skips the cycle rather than queueing behind the instance that did: by the time it acquired, the
/// work would be done.
/// </para>
/// </summary>
public sealed class AutomationSchedulerWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IDistributedLock _locks;
    private readonly IOptions<AutomationOptions> _options;
    private readonly ILogger<AutomationSchedulerWorker> _logger;

    public AutomationSchedulerWorker(
        IServiceScopeFactory scopes,
        IDistributedLock locks,
        IOptions<AutomationOptions> options,
        ILogger<AutomationSchedulerWorker> logger)
    {
        _scopes = scopes;
        _locks = locks;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// The cadence.
    /// <para>
    /// <b>A <see cref="PeriodicTimer"/> rather than <c>Task.Delay</c>, and the catch lives one frame
    /// down.</b> B22's maintenance worker records the same shape and the same reason: the platform's
    /// guardrail reports any loop containing both a delay and a catch, because that pair is what a
    /// hand-rolled retry looks like — and a sweep is not a retry. There is no backoff here and there
    /// must not be: a failed sweep re-reads the same durable rows on the next tick, so the interval is
    /// a cadence rather than a recovery strategy.
    /// </para>
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.EnableSweep)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.Value.SweepIntervalSeconds));

        // A short pause before the first sweep. A host that has just started is still opening
        // connections and running migrations, and a sweep that raced them would fail once per
        // deployment for no reason anybody could act on.
        using var startup = new PeriodicTimer(TimeSpan.FromSeconds(20));

        if (!await startup.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await SweepOnceAsync(stoppingToken).ConfigureAwait(false);

            if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    /// <summary>
    /// One cycle, with the catch that must not take the host down.
    /// <para>
    /// Its own method so the loop above contains no catch clause at all — see the cadence note.
    /// </para>
    /// </summary>
    private async Task SweepOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await SweepAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown. Not a failure.
        }
        catch (Exception exception)
        {
            // Never fatal. A sweep that threw must not take the host down or stop every later sweep;
            // the next tick re-reads the same durable rows and tries again.
            _logger.LogError(exception, "The automation sweep failed. It will run again.");
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        await using var lease = await _locks.TryAcquireAsync(
            AutomationModuleMetadata.SchedulerLeaseResource,
            TimeSpan.FromMinutes(5),
            cancellationToken);

        if (lease is null)
        {
            return;
        }

        using var scope = _scopes.CreateScope();
        var services = scope.ServiceProvider;

        await SweepOccurrencesAsync(services, cancellationToken);
        await SweepSubjectHorizonsAsync(services, cancellationToken);
        await SweepRemindersAsync(services, cancellationToken);
        await SweepStaleRunsAsync(services, cancellationToken);
        await SweepRetentionAsync(services, cancellationToken);
    }

    /// <summary>
    /// Fans a due dated subject out to every automation that watches its trigger — Wave 12.
    /// <para>
    /// <b>Second in the order, and immediately after occurrences, for the same reason they are first:
    /// a member is waiting for it.</b> Before reminders, because a horizon that fires may itself
    /// schedule one through <c>reminder.schedule</c>, and a reminder created this cycle should be
    /// considered by this cycle's reminder pass rather than waiting a full interval.
    /// </para>
    /// <para>
    /// <b>The fan-out is <c>ObserveAsync</c>, not <c>RunOneAsync</c>.</b> That is the whole reason this
    /// store holds one row per subject rather than one per watcher: matching happens now, against the
    /// automations that exist now, through exactly the path an event-driven trigger takes — the same
    /// idempotency, the same lease, the same chain and rate ceilings, the same conditions, the same
    /// history.
    /// </para>
    /// </summary>
    private async Task SweepSubjectHorizonsAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var horizons = services.GetRequiredService<IAutomationSubjectHorizonRepository>();
        var engine = services.GetRequiredService<AutomationEngine>();
        var clock = services.GetRequiredService<IClock>();

        var now = clock.UtcNow;
        var due = await horizons.ListDueAsync(now, _options.Value.SweepBatchSize, cancellationToken);

        if (due.Count == 0)
        {
            return;
        }

        var owner = $"{Environment.MachineName}:{Environment.ProcessId}";

        foreach (var horizon in due)
        {
            if (!horizon.TryClaim(
                    owner, now, TimeSpan.FromMinutes(_options.Value.OccurrenceClaimMinutes)))
            {
                continue;
            }

            AutomationTelemetry.SchedulerLag.Record(
                (now - horizon.DueAt).TotalMilliseconds,
                new KeyValuePair<string, object?>("zone", horizon.TimeZoneId));

            // The subject's own moment has passed. Telling somebody a job is "about to close" after it
            // closed is worse than silence, and unlike the catch-up window this is not a question of
            // how late the sweep is — the thing itself is over.
            if (horizon.SubjectMoment <= now)
            {
                horizon.Skip(
                    "The moment this was about had already passed, so nothing was run.", now);

                // B24-G11. A moment that passed is the one kind of scheduling failure that leaves no
                // trace in a lag histogram — the samples it would have contributed are precisely the
                // ones never taken. A DST fault shows up here as a spike in one zone and nowhere else.
                AutomationTelemetry.MissedOccurrence(
                    AutomationTelemetry.Tags.Work.Horizon, horizon.TimeZoneId);

                continue;
            }

            try
            {
                await engine.ObserveAsync(
                    new AutomationTriggerContext
                    {
                        TriggerKey = horizon.TriggerKey,

                        // The horizon row is the idempotency subject, exactly as an occurrence is. Two
                        // instances racing, a lapsed claim reclaimed, or a restart mid-fan-out all name
                        // the same identity, so the engine's per-automation barrier catches them.
                        TriggerEventId = $"horizon:{horizon.Id.Value:N}",
                        ConcernedOwnerIds = horizon.Owners(),
                        SubjectType = horizon.SubjectKind,
                        SubjectId = horizon.SubjectId,
                        SubjectTitle = horizon.SubjectTitle,
                        SubjectMoment = horizon.SubjectMoment,
                        SubjectTimeZoneId = horizon.TimeZoneId,
                        Summary = horizon.Summary,
                        OccurredAt = horizon.DueAt,
                        Values = AutomationSubjectHorizonService.ReadValues(horizon.Values)
                    },
                    cancellationToken);

                horizon.Complete(clock.UtcNow);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    exception,
                    "Subject horizon {HorizonId} on {TriggerKey} failed.",
                    horizon.Id.Value, horizon.TriggerKey);

                horizon.RecordFailure(
                    "The platform could not run this.", _options.Value.MaximumOccurrenceAttempts,
                    clock.UtcNow);

                if (horizon.Status == AutomationOccurrenceStatus.DeadLettered)
                {
                    // B24-G30: §19's dimensions, and the attempt count as a bucket rather than a
                    // value so a retry ceiling change cannot widen the metric.
                    AutomationTelemetry.DeadLettered(
                        AutomationTelemetry.Tags.Work.Horizon, horizon.TriggerKey, horizon.Attempts);
                }
                else
                {
                    // B24-G29: the retry the platform has always performed and never counted. A
                    // dead letter is the end of a story whose middle was invisible.
                    AutomationTelemetry.Retried(
                        AutomationTelemetry.Tags.Work.Horizon, horizon.TriggerKey);
                }
            }
        }

        await horizons.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Claims every due occurrence and runs it.
    /// <para>
    /// The claim is what makes this safe across instances: a row moves to <c>Claimed</c> with an
    /// expiry, and a second instance whose lease attempt failed would in any case find nothing to take.
    /// The expiry is what makes it safe across a crash — an instance that dies mid-run leaves a claim
    /// that lapses rather than an occurrence nobody may touch.
    /// </para>
    /// </summary>
    private async Task SweepOccurrencesAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var occurrences = services.GetRequiredService<IAutomationOccurrenceRepository>();
        var automations = services.GetRequiredService<IAutomationRepository>();
        var engine = services.GetRequiredService<AutomationEngine>();
        var schedule = services.GetRequiredService<AutomationScheduleService>();
        var clock = services.GetRequiredService<IClock>();

        var now = clock.UtcNow;
        var due = await occurrences.ListDueAsync(now, _options.Value.SweepBatchSize, cancellationToken);

        if (due.Count == 0)
        {
            return;
        }

        var owner = $"{Environment.MachineName}:{Environment.ProcessId}";

        foreach (var occurrence in due)
        {
            if (!occurrence.TryClaim(
                    owner, now, TimeSpan.FromMinutes(_options.Value.OccurrenceClaimMinutes)))
            {
                continue;
            }

            AutomationTelemetry.SchedulerLag.Record(
                (now - occurrence.DueAt).TotalMilliseconds,
                new KeyValuePair<string, object?>("zone", occurrence.TimeZoneId));

            // The catch-up policy, applied. An occurrence more than one window late is skipped with a
            // record rather than fired: a daily reminder that arrives eleven hours late is worse than
            // one that does not arrive, and history has to show it was skipped and why.
            if (!AutomationScheduleCalculator.IsWithinCatchUp(
                    occurrence.DueAt, now, _options.Value.OccurrenceCatchUpMinutes))
            {
                occurrence.Skip(
                    "This was missed by more than the platform's catch-up window, so it was not run.",
                    now);

                AutomationTelemetry.MissedOccurrence(
                    AutomationTelemetry.Tags.Work.Occurrence, occurrence.TimeZoneId);

                continue;
            }

            var automation = await automations.GetWithRuleAsync(occurrence.AutomationId, cancellationToken);

            if (automation is null || !automation.IsRunnable(now))
            {
                occurrence.Cancel("The automation is no longer running.", now);

                // B24-G45. A scheduled moment cancelled because its rule is gone or stopped. Healthy
                // and expected — a member pauses an automation and its pending moments unwind — and
                // worth counting because a sweep that suddenly cancels everything it claims is a
                // deployment that has lost its definitions, which no other instrument would show.
                AutomationTelemetry.RunSkipped(
                    automation?.TriggerKey ?? AutomationTelemetry.Tags.UnknownTrigger,
                    AutomationTelemetry.Tags.Skip.TargetUnavailable);

                continue;
            }

            try
            {
                await engine.RunOneAsync(
                    automation,
                    new AutomationTriggerContext
                    {
                        TriggerKey = automation.TriggerKey,

                        // The occurrence is the idempotency subject. A restart mid-dispatch, a clock
                        // adjustment or two instances racing produce one run, because they would all
                        // name the same moment.
                        TriggerEventId = $"occurrence:{occurrence.Id.Value:N}",
                        ConcernedOwnerIds = [automation.OwnerId],
                        Summary = $"Scheduled for {occurrence.LocalWallClock} ({occurrence.TimeZoneId})",
                        OccurredAt = occurrence.DueAt
                    },
                    AutomationRunReason.Schedule,
                    isSimulated: false,
                    cancellationToken);

                occurrence.Complete(clock.UtcNow);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    exception,
                    "Occurrence {OccurrenceId} for automation {AutomationId} failed.",
                    occurrence.Id.Value, occurrence.AutomationId.Value);

                occurrence.RecordFailure(
                    "The platform could not run this.", _options.Value.MaximumOccurrenceAttempts,
                    clock.UtcNow);

                if (occurrence.Status == AutomationOccurrenceStatus.DeadLettered)
                {
                    AutomationTelemetry.DeadLettered(
                        AutomationTelemetry.Tags.Work.Occurrence,
                        automation.TriggerKey,
                        occurrence.Attempts);
                }
                else
                {
                    AutomationTelemetry.Retried(
                        AutomationTelemetry.Tags.Work.Occurrence, automation.TriggerKey);
                }
            }

            // The next moment, derived after the run rather than before it, so a schedule that just
            // expired on its run limit does not get one.
            await schedule.SynchroniseAsync(automation, cancellationToken);
        }

        await occurrences.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Delivers every reminder that has come due.
    /// <para>
    /// Publishes a fact and stops, exactly as the engine does. B13 decides the channel, applies the
    /// member's quiet hours in their own timezone and de-duplicates; this pass decides only that the
    /// moment arrived.
    /// </para>
    /// </summary>
    private async Task SweepRemindersAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var reminders = services.GetRequiredService<IAutomationReminderRepository>();
        var publisher = services.GetRequiredService<IIntegrationEventPublisher>();
        var clock = services.GetRequiredService<IClock>();

        var now = clock.UtcNow;
        var due = await reminders.ListDueAsync(now, _options.Value.SweepBatchSize, cancellationToken);

        if (due.Count == 0)
        {
            return;
        }

        foreach (var reminder in due)
        {
            // Too long past the moment it was about (`AutomationReminder.LateGrace`). A late reminder
            // is worse than none, so it is failed with a visible state rather than delivered.
            //
            // The reason names the subject's own kind. Wave 11 said "the event had already started"
            // whatever the reminder was about, which was true while every reminder was an event and
            // became a description of something that did not happen the moment a job closing or a
            // viewing appointment could also be reminded about.
            if (reminder.IsTooLateToSend(now))
            {
                reminder.Abandon(AutomationSubjectKinds.LateReason(reminder.SubjectKind));
                continue;
            }

            try
            {
                await publisher.PublishAsync(
                    new AutomationDeliveryRequestedIntegrationEvent
                    {
                        AutomationId = Guid.Empty,
                        RunId = Guid.Empty,
                        ActionId = reminder.Id.Value,
                        RecipientUserId = reminder.MemberId.Value,
                        Audience = AutomationNotificationKeys.ReminderAudience,
                        AutomationName = "Reminder",
                        Summary = reminder.SubjectTitle,
                        SubjectType = reminder.SubjectKind,
                        SubjectId = reminder.SubjectId.ToString("D"),
                        SubjectTitle = reminder.SubjectTitle,
                        SubjectMoment = reminder.SubjectMoment,
                        OccurredAt = now,

                        // A reminder has no run to settle, so no receipt is asked for. Its own five
                        // states are the observable thing, and they are on the row.
                        RequiresSettlementReceipt = false
                    },
                    cancellationToken);

                reminder.MarkSent(now);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    exception, "Reminder {ReminderId} could not be published.", reminder.Id.Value);

                reminder.MarkFailed("The platform could not send this reminder.");
            }
        }

        await reminders.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Times out runs whose settlement never arrived.
    /// <para>
    /// A run that published a notification request and never heard back would otherwise sit
    /// <c>Running</c> for ever, and a member reading history would see an automation permanently
    /// mid-flight. Timed out rather than failed, because the notification may well have been delivered
    /// — what is missing is the answer, not the delivery, and the two read differently to a member.
    /// </para>
    /// </summary>
    private async Task SweepStaleRunsAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var runs = services.GetRequiredService<IAutomationRunRepository>();
        var reporter = services.GetRequiredService<AutomationRunReporter>();
        var clock = services.GetRequiredService<IClock>();

        var now = clock.UtcNow;
        var cutoff = now.AddMinutes(-_options.Value.RunTimeoutMinutes);
        var stale = await runs.ListStaleRunningAsync(cutoff, _options.Value.SweepBatchSize, cancellationToken);

        if (stale.Count == 0)
        {
            return;
        }

        foreach (var run in stale)
        {
            run.Fail(
                AutomationFailureKind.Timeout,
                "The platform did not hear back about this in time.",
                "Check your notification settings, or run it again.",
                now);

            // B24: a timed-out run is a finished run, and before this it was the third terminal path
            // that counted nothing, moved no counter and announced nothing. The reporter's claim is
            // what makes this safe beside a settlement receipt that arrives at the same moment —
            // whichever gets there first reports, and the other finds the run already claimed.
            await reporter.ReportAsync(run, automation: null, now, cancellationToken);
        }

        await runs.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Applies the retention horizon.
    /// <para>
    /// Finite and stated, because an execution log over a wallet or a location trigger is a history of
    /// somebody's money and movements. Runs older than the horizon go, and their outcomes go with them
    /// through the cascade.
    /// </para>
    /// </summary>
    private async Task SweepRetentionAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var runs = services.GetRequiredService<IAutomationRunRepository>();
        var clock = services.GetRequiredService<IClock>();

        var now = clock.UtcNow;
        var cutoff = now.AddDays(-_options.Value.RunRetentionDays);
        var deleted = await runs.DeleteOlderThanAsync(cutoff, cancellationToken);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "AutomationDefinition retention removed {Count} runs older than {Cutoff:o}.", deleted, cutoff);
        }

        // Settled horizon rows, on their own shorter horizon. Working state rather than history: the
        // run a horizon produced is the durable record, and this table would otherwise grow by one row
        // per published job, listing, reservation and viewing for ever.
        var horizons = services.GetRequiredService<IAutomationSubjectHorizonRepository>();
        var horizonCutoff = now.AddDays(-_options.Value.SubjectHorizonRetentionDays);
        var settled = await horizons.DeleteSettledOlderThanAsync(horizonCutoff, cancellationToken);

        if (settled > 0)
        {
            _logger.LogInformation(
                "AutomationDefinition retention removed {Count} settled subject horizons older than "
                + "{Cutoff:o}.", settled, horizonCutoff);
        }
    }
}
