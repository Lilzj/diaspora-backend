using Diaspora.Contracts.Automation.V1.Events;
using Diaspora.Modules.Automation.Domain.Automations;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Repositories;
using Diaspora.Modules.Automation.Domain.Runs;
using Diaspora.Platform.Messaging;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Automation.Observability;

/// <summary>
/// The one place a finished run is counted, its definition's counters moved, and its outcome announced.
/// <para>
/// <b>B24-G05 · B24-G08 · B24-G25 · B24-G26 · B24-G48 · B24-G67 · B24-G72.</b> A run reaches a
/// terminal state on three paths and until this class existed only one of them did any of the three
/// things above:
/// </para>
/// <list type="table">
///   <listheader><term>Path</term><description>Before B24</description></listheader>
///   <item>
///     <term><c>AutomationEngine.PersistAsync</c></term>
///     <description>Counted, moved, announced.</description>
///   </item>
///   <item>
///     <term><c>AutomationNotificationSettledConsumer</c></term>
///     <description>None of the three.</description>
///   </item>
///   <item>
///     <term><c>AutomationSchedulerWorker.SweepStaleRunsAsync</c></term>
///     <description>None of the three.</description>
///   </item>
/// </list>
/// <para>
/// <b>The second path is the common one, not an edge case.</b> <c>notify.*</c> is the primary
/// automation action; a notification is recorded <c>Pending</c> the moment its request is published
/// and settled minutes later from the Communication Platform's own receipt. So the engine leaves the
/// run <c>Running</c>, the consumer finishes it, and before this class the most-used automation on
/// the platform was the one whose completion was never counted, never timed, never announced, and
/// whose <c>ExecutionCount</c> never moved — which also meant a <c>MaxExecutions</c> ceiling on such
/// a rule could never be reached.
/// </para>
///
/// <para><b>Three properties this class exists to guarantee.</b></para>
/// <list type="number">
///   <item>
///     <b>Exactly once.</b> <see cref="AutomationRun.TryClaimReport"/> is a persisted claim, not a
///     status check. A redelivered receipt arriving beside the stale-run sweep would pass any
///     status-based test twice, and a double-counted execution is the one outcome §30 forbids
///     outright.
///   </item>
///   <item>
///     <b>Never before the fact.</b> The claim refuses a run that is still <c>Running</c>, so there
///     is no path that announces a completion the platform has not reached. §32.
///   </item>
///   <item>
///     <b>It stages and never saves.</b> The run, the definition's counters and the outbox row commit
///     in the caller's single <c>SaveChanges</c>. A reporter that saved would announce an execution
///     whose transaction then rolled back — §31's whole point.
///   </item>
/// </list>
/// </summary>
public sealed class AutomationRunReporter
{
    private readonly IAutomationRepository _automations;
    private readonly IIntegrationEventPublisher _publisher;
    private readonly ILogger<AutomationRunReporter> _logger;

    public AutomationRunReporter(
        IAutomationRepository automations,
        IIntegrationEventPublisher publisher,
        ILogger<AutomationRunReporter> logger)
    {
        _automations = automations;
        _publisher = publisher;
        _logger = logger;
    }

    /// <summary>
    /// Records, moves and announces one finished run.
    /// </summary>
    /// <param name="run">The run. Ignored unless it has finished and nobody has reported it.</param>
    /// <param name="automation">
    /// The definition, when the caller already has it loaded. The engine does; the settlement
    /// consumer and the sweep do not, and this loads it for them rather than making each remember.
    /// </param>
    /// <param name="now">The reporting instant.</param>
    /// <returns>True when this call was the one that reported it.</returns>
    public async Task<bool> ReportAsync(
        AutomationRun run,
        AutomationDefinition? automation,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        // A dry run performs nothing and is never persisted, so it has no outcome to announce and no
        // counter to move. It is still counted as a start, which is what makes §57's
        // simulations-to-activations ratio answerable.
        if (run.IsSimulated || !run.TryClaimReport(now))
        {
            return false;
        }

        RecordMetrics(run);

        automation ??= await _automations.GetAsync(run.AutomationId, cancellationToken)
            .ConfigureAwait(false);

        if (automation is not null)
        {
            automation.RecordRun(run.Status, now);
        }
        else
        {
            // The rule was deleted while its last run was in flight. The run is still the durable
            // record and is still announced; there is simply no definition left to move a counter on.
            _logger.LogDebug(
                "Run {RunId} settled after its automation was removed; counters were not moved.",
                run.Id.Value);
        }

        await _publisher.PublishAsync(
            new AutomationRunCompletedIntegrationEvent
            {
                AutomationId = run.AutomationId.Value,
                RunId = run.Id.Value,
                OwnerId = run.OwnerId,
                OwnerKind = run.OwnerKind.ToString(),
                TriggerKey = run.TriggerKey,
                Status = run.Status.ToString(),
                ActionsAttempted = run.ActionOutcomes.Count,
                ActionsSucceeded = run.ActionOutcomes.Count(
                    outcome => outcome.Status == AutomationActionOutcomeStatus.Succeeded),
                ActionsSuppressed = run.ActionOutcomes.Count(
                    outcome => outcome.Status == AutomationActionOutcomeStatus.Suppressed),
                DurationMs = run.DurationMs ?? 0,
                FailureKind = run.FailureKind == AutomationFailureKind.None
                    ? null
                    : run.FailureKind.ToString(),
                OccurredAt = run.CompletedAt ?? now
            },
            cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// The instruments, recorded from the run's own terminal state.
    /// <para>
    /// <b>Derived rather than passed in.</b> Every earlier call site chose its own tags at the moment
    /// it decided something, which is how <c>failures</c> came to count two of the twelve failure
    /// kinds: only the two paths that failed a run explicitly ever incremented it, and a run that
    /// failed because its actions did went uncounted. Reading the row is the only way the counter
    /// agrees with the history a member is shown.
    /// </para>
    /// </summary>
    private static void RecordMetrics(AutomationRun run)
    {
        AutomationTelemetry.RunSettled(run.TriggerKey, run.Status.ToString());

        if (run.Status is AutomationRunStatus.Failed
            or AutomationRunStatus.PartiallyCompleted
            or AutomationRunStatus.TimedOut)
        {
            AutomationTelemetry.RunFailed(run.TriggerKey, run.FailureKind.ToString());
        }

        if (run.DurationMs is { } duration)
        {
            AutomationTelemetry.Duration.Record(
                duration, new KeyValuePair<string, object?>("trigger", run.TriggerKey));
        }

        // How long the fact waited before anything started on it. Negative is impossible in principle
        // and possible in practice — a producer's clock against this host's — and a negative sample
        // in a latency histogram is worse than a dropped one.
        var queued = (run.StartedAt - run.TriggeredAt).TotalMilliseconds;

        if (queued >= 0)
        {
            AutomationTelemetry.TriggerLatency.Record(
                queued,
                new KeyValuePair<string, object?>("trigger", run.TriggerKey),
                new KeyValuePair<string, object?>("reason", run.Reason.ToString()));
        }
    }
}
