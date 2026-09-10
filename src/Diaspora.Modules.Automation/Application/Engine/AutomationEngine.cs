using System.Diagnostics;
using Diaspora.Contracts.Automation.V1.Events;
using Diaspora.Modules.Automation.Domain.Automations;
using Diaspora.Modules.Automation.Domain.Catalog;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Repositories;
using Diaspora.Modules.Automation.Domain.Runs;
using Diaspora.Modules.Automation.Extensibility.Scopes;
using Diaspora.Modules.Automation.Observability;
using Diaspora.Modules.Automation.Options;
using Diaspora.Platform.Messaging;
using Diaspora.Platform.Operations.Scheduling;
using Diaspora.Platform.Operations.Telemetry;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Automation.Application.Engine;

/// <summary>
/// The consumer that observes facts, evaluates conditions, dispatches actions and records the outcome.
/// <para>
/// <b>FRONTEND-PHASE18-GAP-002, and its stated blocker was not accurate.</b> The register says "no
/// observable domain-event stream exists… no outbox, no message bus contract" and concludes that
/// building the engine means building the stream. B1.1 shipped a transactional outbox and the platform
/// publishes 196 contracts through it. This class consumes them; it builds no bus, and an architecture
/// test asserts the assembly contains no transport.
/// </para>
///
/// <para><b>The five guarantees, and where each one lives.</b></para>
///
/// <para>
/// <b>Idempotency.</b> Delivery is at-least-once, so the run's identity is
/// <c>(AutomationId, TriggerEventId)</c> and it is unique in the database. The read below is the fast
/// path; the unique index is the real barrier, because two instances consuming the same redelivered
/// message would both pass a read. A redelivery finds the existing run and marks it deduplicated,
/// which is what §39's "AlreadyProcessed renders as a completed state rather than an error" needs to
/// render.
/// </para>
/// <para>
/// <b>Distributed safety.</b> Every execution takes a B17 lease named for the automation. Two
/// instances receiving the same event, or an event arriving beside a due occurrence, produce one run —
/// the second skips rather than queues, because by the time it acquired the work would be done. The
/// lease is also §40's concurrency requirement: a rule that notifies a community must not run twice
/// concurrently.
/// </para>
/// <para>
/// <b>Loop protection.</b> A run carries a chain depth and a chain id. An action that causes a fact
/// that matches another automation increments the depth, and the engine refuses beyond the ceiling
/// with a recorded refusal rather than discovering the loop from a member's inbox.
/// </para>
/// <para>
/// <b>Rate limiting.</b> Two ceilings — per automation and per owner — over a rolling window, checked
/// before a run is created. An automation is not a mechanism for spamming members, and a rule matching
/// a bulk import is the ordinary way one becomes it.
/// </para>
/// <para>
/// <b>Fail closed.</b> The stored rule is untrusted input. An action key the catalogue no longer has,
/// a trigger that became unobservable, a capability the owner has lost and a condition the platform
/// cannot answer all stop the run — none of them let it through.
/// </para>
/// </summary>
public sealed class AutomationEngine
{
    private readonly IAutomationRepository _automations;
    private readonly IAutomationRunRepository _runs;
    private readonly AutomationActionDispatcher _actions;
    private readonly AutomationConditionResolver _conditions;
    private readonly AutomationScopeAuthorityResolver _scopes;
    private readonly AutomationRunReporter _reporter;
    private readonly AutomationFailureClassifier _failures;
    private readonly IIntegrationEventPublisher _publisher;
    private readonly IDistributedLock _locks;
    private readonly ICorrelationContext _correlation;
    private readonly IClock _clock;
    private readonly IOptions<AutomationOptions> _options;
    private readonly ILogger<AutomationEngine> _logger;

    public AutomationEngine(
        IAutomationRepository automations,
        IAutomationRunRepository runs,
        AutomationActionDispatcher actions,
        AutomationConditionResolver conditions,
        AutomationScopeAuthorityResolver scopes,
        AutomationRunReporter reporter,
        AutomationFailureClassifier failures,
        IIntegrationEventPublisher publisher,
        IDistributedLock locks,
        ICorrelationContext correlation,
        IClock clock,
        IOptions<AutomationOptions> options,
        ILogger<AutomationEngine> logger)
    {
        _automations = automations;
        _runs = runs;
        _actions = actions;
        _conditions = conditions;
        _scopes = scopes;
        _reporter = reporter;
        _failures = failures;
        _publisher = publisher;
        _locks = locks;
        _correlation = correlation;
        _clock = clock;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Runs every automation that matches one observed fact.
    /// </summary>
    public async Task ObserveAsync(
        AutomationTriggerContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // B24-G20: counted before anything can decide not to run, so "the fact never arrived" and
        // "the fact arrived and nothing watched it" stop being the same silence. Every early return
        // below is invisible without it, and three of them are the ordinary healthy case.
        AutomationTelemetry.TriggerObserved(context.TriggerKey);

        var trigger = AutomationCatalog.FindTrigger(context.TriggerKey);

        if (trigger is null || !trigger.IsAvailable)
        {
            // A consumer built a context for a trigger the catalogue does not offer. Not an error to
            // shout about — a catalogue can shrink — but nothing runs.
            return;
        }

        if (context.ChainDepth > _options.Value.MaximumChainDepth)
        {
            AutomationTelemetry.ChainRefusals.Add(
                1, new KeyValuePair<string, object?>("trigger", context.TriggerKey));

            _logger.LogWarning(
                "AutomationDefinition chain {ChainId} reached depth {Depth} and was refused.",
                context.ChainId, context.ChainDepth);

            return;
        }

        var candidates = await _automations.ListRunnableForTriggerAsync(
            context.TriggerKey, context.ConcernedOwnerIds, cancellationToken);

        if (candidates.Count == 0)
        {
            return;
        }

        AutomationTelemetry.Triggered.Add(
            1, new KeyValuePair<string, object?>("trigger", context.TriggerKey));

        foreach (var automation in candidates)
        {
            await RunOneAsync(
                automation, context, AutomationRunReason.DomainEvent, isSimulated: false,
                cancellationToken);
        }
    }

    /// <summary>
    /// Runs one automation the member asked to run, or one a due occurrence woke.
    /// <para>
    /// Public so the manual and the scheduled paths take the same route through conditions, dispatch,
    /// idempotency and history. An engine with two execution models would need two of everything and
    /// would eventually disagree with itself.
    /// </para>
    /// </summary>
    public Task<AutomationRun?> RunOneAsync(
        AutomationDefinition automation,
        AutomationTriggerContext context,
        AutomationRunReason reason,
        bool isSimulated,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(automation, context, reason, isSimulated, cancellationToken);

    private async Task<AutomationRun?> ExecuteAsync(
        AutomationDefinition automation,
        AutomationTriggerContext contextAsGiven,
        AutomationRunReason reason,
        bool isSimulated,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        // Phase 5. Stamped here rather than trusted from the caller, so a context's idea of whether
        // it is a preview cannot disagree with the run's. It changes nothing about what runs; it
        // changes what a member is told when a condition has no value to compare against.
        var context = contextAsGiven.IsPreview == isSimulated
            ? contextAsGiven
            : contextAsGiven with { IsPreview = isSimulated };

        using var activity = AutomationTelemetry.Source.StartActivity("automation.execute");

        // ── B24-G33: the span is where an operator joins a run to the request that caused it ──────
        //
        // The platform already carries a correlation and a causation across the outbox — the
        // transport restores both onto the consuming scope — and until now the automation span threw
        // both away. The trace id needs no attribute: this activity is a child of the consuming
        // span, so it inherits one. What it could not inherit is which rule ran and which row
        // recorded it, and without those an operator holding a trace has a timeline and no subject.
        //
        // **None of this is a metric tag.** An automation id is an unbounded dimension and §12 bans
        // it outright; a span attribute is sampled, is retained on B17's terms, and is read one trace
        // at a time. The distinction is the whole reason the ids appear here and nowhere else.
        activity?.SetTag("automation.trigger", context.TriggerKey);
        activity?.SetTag("automation.reason", reason.ToString());
        activity?.SetTag("automation.id", automation.Id.Value);
        activity?.SetTag("automation.owner_kind", automation.OwnerKind.ToString());
        activity?.SetTag("automation.simulated", isSimulated);
        activity?.SetTag("automation.trigger_event_id", context.TriggerEventId);
        activity?.SetTag("automation.chain_depth", context.ChainDepth);
        activity?.SetTag(
            PlatformTelemetryNames.Attributes.Module, AutomationModuleMetadata.Name);

        if (_correlation.CorrelationId is { Length: > 0 } inboundCorrelation)
        {
            activity?.SetTag(
                PlatformTelemetryNames.Attributes.CorrelationId, inboundCorrelation);
        }

        if (_correlation.CausationId is { Length: > 0 } inboundCausation)
        {
            activity?.SetTag("diaspora.causation_id", inboundCausation);
        }

        // ── Idempotency, the fast path ──────────────────────────────────────────────────────────
        //
        // A simulation is never deduplicated against a real run and never recorded as one: it has its
        // own trigger identity and it is not persisted, so a member testing a rule twice gets two
        // honest answers rather than the first one twice.
        if (!isSimulated)
        {
            var existing = await _runs.FindByTriggerAsync(
                automation.Id, context.TriggerEventId, cancellationToken);

            if (existing is not null)
            {
                existing.MarkDeduplicated();
                await _runs.SaveChangesAsync(cancellationToken);

                AutomationTelemetry.Deduplicated.Add(
                    1, new KeyValuePair<string, object?>("trigger", context.TriggerKey));

                return existing;
            }
        }

        if (!automation.IsRunnable(now) && reason != AutomationRunReason.Simulation)
        {
            // B24-G42. Paused, disabled, expired or past its run limit. Not a failure and not an
            // error — but until it was counted, a member whose rule silently stopped firing and a
            // platform that had stopped delivering their trigger produced identical telemetry.
            AutomationTelemetry.RunSkipped(
                context.TriggerKey, AutomationTelemetry.Tags.Skip.NotRunnable);

            activity?.SetTag("automation.outcome", AutomationTelemetry.Tags.Skip.NotRunnable);

            return null;
        }

        // ── The rate ceilings ───────────────────────────────────────────────────────────────────
        if (!isSimulated && !await WithinRateCeilingsAsync(automation, now, cancellationToken))
        {
            AutomationTelemetry.RateRefusals.Add(
                1, new KeyValuePair<string, object?>("trigger", context.TriggerKey));

            return null;
        }

        // ── The per-automation lease ────────────────────────────────────────────────────────────
        //
        // Non-blocking. A second instance that cannot take it skips rather than queues: by the time it
        // acquired, the first would have finished and the second would produce a duplicate run that
        // the idempotency check would then have to unpick.
        await using var lease = isSimulated
            ? null
            : await _locks.TryAcquireAsync(
                $"{AutomationModuleMetadata.ExecutionLeasePrefix}:{automation.Id.Value:N}",
                TimeSpan.FromMinutes(_options.Value.RunTimeoutMinutes),
                cancellationToken);

        if (!isSimulated && lease is null)
        {
            // B24-G46. Contention, not failure — and the counter matters because a lease that is
            // never released looks exactly like a quiet platform from every other instrument. A skip
            // rate on this reason that climbs and stays climbed is a stuck lease.
            AutomationTelemetry.RunSkipped(
                context.TriggerKey, AutomationTelemetry.Tags.Skip.Contended);

            activity?.SetTag("automation.outcome", AutomationTelemetry.Tags.Skip.Contended);

            _logger.LogDebug(
                "AutomationDefinition {AutomationId} is already running on another instance; skipping.",
                automation.Id.Value);

            return null;
        }

        var rule = await _automations.GetWithRuleAsync(automation.Id, cancellationToken)
                   ?? automation;

        var run = AutomationRun.Start(
            rule.Id,
            rule.Name,
            rule.OwnerId,
            rule.OwnerKind,
            reason,
            context.TriggerEventId,
            context.TriggerKey,
            context.Summary,
            context.OccurredAt,
            now,
            NewCorrelationId(),
            isSimulated,
            context.ChainDepth,
            string.IsNullOrEmpty(context.ChainId) ? NewCorrelationId() : context.ChainId);

        AutomationTelemetry.RunStarted(context.TriggerKey, reason.ToString());

        // The execution identity, and the member-readable correlation id beside it. Both are span
        // attributes rather than metric tags, for the reason set out where the span is started: a run
        // id is unbounded, and a support conversation that begins "my automation did not fire" begins
        // with the string on the member's screen.
        activity?.SetTag("automation.run_id", run.Id.Value);
        activity?.SetTag("automation.correlation_id", run.CorrelationId);
        activity?.SetTag("automation.chain_id", run.ChainId);

        // ── The authority re-check, at execution rather than at configuration ───────────────────
        //
        // A moderator who created a community automation and then lost the role must stop being able
        // to broadcast through it. This is the only place that can be enforced, so it is enforced here
        // even though the write path already checked it — and a rule whose owner has lost the
        // authority outright is flagged rather than merely skipped, so the owner is told.
        var authority = _scopes.For(rule.OwnerKind);
        var grant = await authority.EvaluateAsync(
            ResolveActor(rule), rule.OwnerKind, rule.OwnerId, cancellationToken);

        if (!grant.CanOwn)
        {
            run.Fail(
                AutomationFailureKind.PermissionDenied,
                "Whoever set this up no longer has permission to run it.",
                "Ask somebody with the right role to set it up again.",
                now);

            // No `RunFailed` here any more. The reporter derives it from the run's own terminal state,
            // and counting it twice — once at the decision, once at the settle — was how the two
            // instrumented failure kinds came to look like the only two that ever happened.
            if (!isSimulated)
            {
                await PersistAsync(rule, run, now, cancellationToken);
                await FlagAsync(rule, "Whoever set this up no longer has permission to run it.",
                    disable: true, now, cancellationToken);
            }

            return run;
        }

        // ── Conditions ──────────────────────────────────────────────────────────────────────────
        //
        // Wave 12: the answers a payload cannot give are fetched first, from the modules that own
        // them, keyed on the owner this run was authorized as. Nothing is asked unless the rule asks
        // it, so a rule with no such condition makes no call — which is almost all of them. The
        // evaluator itself stays pure, static and synchronous: everything it compares is already in a
        // dictionary by the time it runs.
        // B24-G23 · B24-G36. Timed as one pass rather than per condition, because resolution is one
        // batched call and attributing its cost to whichever condition happened to be first would
        // make the per-condition figure a lie. Wave 12 turned this step from arithmetic into I/O, and
        // I/O nobody times is how a platform's slowest step stays invisible.
        var conditionStarted = Stopwatch.GetTimestamp();

        var resolved = await _conditions.ResolveAsync(
            rule.Conditions, ResolveActor(rule), context.SubjectId, cancellationToken);

        var evaluated = resolved.Count == 0
            ? context
            : context with { ResolvedValues = resolved };

        var verdicts = AutomationConditionEvaluator.EvaluateAll(rule.Conditions, evaluated);

        AutomationTelemetry.ConditionDuration.Record(
            Stopwatch.GetElapsedTime(conditionStarted).TotalMilliseconds,
            new KeyValuePair<string, object?>("trigger", context.TriggerKey));

        foreach (var (condition, verdict) in verdicts)
        {
            run.RecordCondition(
                condition.Id,
                condition.Key,
                verdict.Matched,
                evaluatedByServer: true,
                verdict.Explanation);

            // The condition *key*, which is a catalogue entry and therefore bounded. Never the
            // comparison, never the value: §8 and §52 both, and the run record makes the same
            // promise two lines above.
            AutomationTelemetry.ConditionEvaluated(
                condition.Key,
                verdict.Matched
                    ? AutomationTelemetry.Tags.Condition.Matched
                    : verdict.Evaluable
                        ? AutomationTelemetry.Tags.Condition.Unmatched
                        : AutomationTelemetry.Tags.Condition.Unevaluable);
        }

        var blocking = verdicts.FirstOrDefault(entry => !entry.Verdict.Matched);

        if (blocking.Condition is not null)
        {
            run.CompleteAsSkipped(blocking.Verdict.Explanation, _clock.UtcNow);

            AutomationTelemetry.RunSkipped(
                context.TriggerKey,
                blocking.Verdict.Evaluable
                    ? AutomationTelemetry.Tags.Skip.Condition
                    : AutomationTelemetry.Tags.Skip.Unevaluable);

            activity?.SetTag("automation.outcome", run.Status.ToString());

            if (!isSimulated)
            {
                await PersistAsync(rule, run, _clock.UtcNow, cancellationToken);
            }

            return run;
        }

        // ── Actions ─────────────────────────────────────────────────────────────────────────────
        //
        // Every action is attempted even after one fails. Retry is per action rather than per run,
        // because retrying a whole run re-sends the actions that already succeeded — and a run with a
        // mix is PartiallyCompleted, which is a state the member can act on.
        foreach (var action in rule.Actions.OrderBy(step => step.Position))
        {
            try
            {
                await _actions.DispatchAsync(
                    rule, action, run, context, grant, isSimulated, _clock.UtcNow, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A dispatcher that threw must not lose the actions already recorded, and must not
                // take down the consumer — a redelivered message would only reproduce it.
                _logger.LogError(
                    exception,
                    "AutomationDefinition action {ActionKey} on run {RunId} threw.",
                    action.Key, run.Id.Value);

                // B24-G40: classified rather than labelled. Before this, a timeout, a provider
                // outage, a cancelled shutdown and a genuine bug were all `ActionFailed`, which is
                // §17's "do not classify everything as Unknown" wearing a more specific name. The
                // taxonomy is the platform's own, not a new one.
                run.RecordAction(
                    action.Id, action.Key, action.Key,
                    AutomationActionOutcomeStatus.Failed, null,
                    _failures.Classify(exception),
                    "Something went wrong carrying this out.",
                    null, null, null, _clock.UtcNow);
            }
        }

        if (rule.Actions.Count == 0)
        {
            run.CompleteAsSkipped("This automation has nothing to do.", _clock.UtcNow);

            AutomationTelemetry.RunSkipped(
                context.TriggerKey, AutomationTelemetry.Tags.Skip.NothingToDo);
        }
        else
        {
            run.CompleteFromOutcomes(_clock.UtcNow);
        }

        activity?.SetTag("automation.outcome", run.Status.ToString());

        if (!isSimulated)
        {
            await PersistAsync(rule, run, _clock.UtcNow, cancellationToken);
        }

        return run;
    }

    /// <summary>
    /// Writes the run, and reports it when it has finished.
    /// <para>
    /// One save. The run, the definition's execution count and the outbox row for the analytics
    /// contract all commit together — a run recorded without its count moved would make a maximum-runs
    /// limit unenforceable, and a count moved without its run would make history lie.
    /// </para>
    /// <para>
    /// <b>B24: the counting and the announcement moved out of here.</b> They were correct in this
    /// method and absent from the two other paths a run can finish on, which is a property no
    /// reviewer of this method could have seen. <c>AutomationRunReporter</c> owns both now, and a
    /// still-<c>Running</c> run — the notify case, waiting on a settlement receipt — reports nothing,
    /// which is what its claim refuses.
    /// </para>
    /// <para>
    /// <b>A failure to write history is itself a finding</b>, and FRONTEND-PHASE18-GAP-004 says so:
    /// "a run that happened and was not recorded is a member telling support 'it did not fire' and
    /// nobody able to say otherwise." It is counted and rethrown — counted so it is alertable,
    /// rethrown so the outbox redelivers and the run is written on the next attempt.
    /// </para>
    /// </summary>
    private async Task PersistAsync(
        AutomationDefinition automation, AutomationRun run, DateTimeOffset now, CancellationToken cancellationToken)
    {
        _runs.Add(run);

        await _reporter.ReportAsync(run, automation, now, cancellationToken);

        try
        {
            await _runs.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AutomationTelemetry.HistoryWriteFailed(AutomationTelemetry.Tags.Stage.Run);
            throw;
        }
    }

    private async Task FlagAsync(
        AutomationDefinition automation,
        string reason,
        bool disable,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        automation.FlagForAttention(reason, disable, now);

        // The platform stopping a rule, not its owner. Its own lifecycle value because the two need
        // telling apart: a member pausing something is the feature working, and the platform
        // switching something off is a rule that has quietly stopped being usable.
        AutomationTelemetry.LifecycleChanged(
            AutomationTelemetry.Tags.Change.Flagged, automation.OwnerKind.ToString());

        await _publisher.PublishAsync(
            new AutomationNeedsAttentionIntegrationEvent
            {
                AutomationId = automation.Id.Value,
                OwnerId = automation.OwnerId,
                OwnerKind = automation.OwnerKind.ToString(),
                AutomationName = automation.Name,
                Reason = reason,
                WasDisabled = disable,
                OccurredAt = now
            },
            cancellationToken);

        await _automations.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> WithinRateCeilingsAsync(
        AutomationDefinition automation, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var since = now.AddMinutes(-_options.Value.RateWindowMinutes);

        var perAutomation = await _runs.CountSinceAsync(automation.Id, since, cancellationToken);

        if (perAutomation >= _options.Value.MaximumRunsPerAutomationPerWindow)
        {
            _logger.LogWarning(
                "AutomationDefinition {AutomationId} reached its execution ceiling and was refused.",
                automation.Id.Value);

            return false;
        }

        var perOwner = await _runs.CountForOwnerSinceAsync(
            automation.OwnerId, since, cancellationToken);

        if (perOwner >= _options.Value.MaximumRunsPerOwnerPerWindow)
        {
            _logger.LogWarning(
                "Owner {OwnerId} reached the platform's execution ceiling and was refused.",
                automation.OwnerId);

            return false;
        }

        return true;
    }

    /// <summary>
    /// Whose authority a run is executed under.
    /// <para>
    /// The creator for a scope somebody administers, and the owner where the owner is a person. An
    /// automation is never executed under the platform's own authority: there is no path here that
    /// produces an empty subject and then treats it as permitted.
    /// </para>
    /// </summary>
    private static UserId ResolveActor(AutomationDefinition automation) =>
        automation.OwnerKind switch
        {
            AutomationOwnerKind.Community or AutomationOwnerKind.Organization
                or AutomationOwnerKind.Platform => automation.CreatedByUserId,
            _ => UserId.From(automation.OwnerId)
        };

    /// <summary>
    /// A correlation id a member can read out to support.
    /// <para>
    /// Opaque and not derived from any internal identifier: one that encoded a row id would be an
    /// internal identifier the member could read and another member could guess.
    /// </para>
    /// </summary>
    private static string NewCorrelationId() => Guid.NewGuid().ToString("N")[..16];
}
