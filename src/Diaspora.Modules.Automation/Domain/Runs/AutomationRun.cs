using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Automation.Domain.Runs;

/// <summary>
/// One attempt at one automation.
/// <para>
/// <b>Append-only, and the identity that makes it so is <c>(AutomationId, TriggerEventId)</c>.</b>
/// Delivery through the outbox is at-least-once, so a redelivered event must produce the <i>same</i>
/// run record rather than a second one — otherwise execution history becomes a duplicate-delivery
/// log and a member reading it concludes their automation fired twice.
/// </para>
/// <para>
/// <b>It carries no sensitive condition values.</b> "Your balance condition matched", never "your
/// balance is £412.60". A run record is a durable log with a longer retention than the data it would
/// be quoting, and the condition outcomes below hold an explanation rather than a comparison.
/// </para>
/// </summary>
public sealed class AutomationRun : AggregateRoot<AutomationRunId>
{
    private readonly List<AutomationRunConditionOutcome> _conditionOutcomes = [];
    private readonly List<AutomationRunActionOutcome> _actionOutcomes = [];

    private AutomationRun() { }

    private AutomationRun(AutomationRunId id) : base(id) { }

    public AutomationId AutomationId { get; private set; } = null!;

    /// <summary>Denormalised so history renders without joining a definition that may be gone.</summary>
    public string AutomationName { get; private set; } = string.Empty;

    /// <summary>
    /// Whose run this is.
    /// <para>
    /// Copied from the definition rather than joined, and it is the column every read filters on.
    /// §66 requires that a member cannot read another member's runs by supplying a run id, and the
    /// cheapest way to guarantee that is for the owner to be on the row the query already touches.
    /// </para>
    /// </summary>
    public Guid OwnerId { get; private set; }

    public AutomationOwnerKind OwnerKind { get; private set; }

    public AutomationRunStatus Status { get; private set; }

    public AutomationRunReason Reason { get; private set; }

    /// <summary>
    /// The published event's identity, or the occurrence's, or the manual attempt's key.
    /// <para>
    /// The idempotency subject. Unique with the automation, enforced by an index rather than by a
    /// read-then-write, because two instances consuming the same redelivered message would both pass
    /// the read.
    /// </para>
    /// </summary>
    public string TriggerEventId { get; private set; } = string.Empty;

    public string TriggerKey { get; private set; } = string.Empty;

    /// <summary>What set it off, in the member's words. Never a payload, never a value.</summary>
    public string TriggerSummary { get; private set; } = string.Empty;

    public DateTimeOffset TriggeredAt { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public long? DurationMs { get; private set; }

    public AutomationFailureKind FailureKind { get; private set; }

    /// <summary>Written for a member. Never a stack trace, never an internal host name.</summary>
    public string? FailureMessage { get; private set; }

    /// <summary>What the member can do about it. Null when there is nothing.</summary>
    public string? RecoveryHint { get; private set; }

    /// <summary>
    /// For a support conversation.
    /// <para>
    /// Opaque, and deliberately not derived from any internal identifier: a correlation id that
    /// encoded a row id would be an internal identifier the member could read and another member
    /// could guess.
    /// </para>
    /// </summary>
    public string CorrelationId { get; private set; } = string.Empty;

    /// <summary>Always true for a dry run, and the dispatcher — not the caller — decides it.</summary>
    public bool IsSimulated { get; private set; }

    /// <summary>True when this run is the platform recognising a redelivery rather than a new firing.</summary>
    public bool WasDeduplicated { get; private set; }

    /// <summary>
    /// How deep in a chain of automations this run sits.
    /// <para>
    /// An action that causes an event that matches another automation is a legitimate and useful
    /// thing; a cycle of two such automations is a runaway. The depth travels with the causation and
    /// the engine refuses to start a run beyond the ceiling, which turns an infinite chain into a
    /// bounded one with a recorded refusal.
    /// </para>
    /// </summary>
    public int ChainDepth { get; private set; }

    /// <summary>The identity of the whole chain, so an operator can see all of it at once.</summary>
    public string ChainId { get; private set; } = string.Empty;

    /// <summary>
    /// When this run's terminal telemetry was recorded and announced. Null until it settles.
    /// <para>
    /// <b>B24: the exactly-once marker, and it is a column rather than a convention.</b> A run reaches
    /// a terminal state on three paths — the engine, a settlement receipt from the Communication
    /// Platform, and the stale-run sweep — and two of them run against a row another thread may also
    /// be holding. Deciding "have we already counted this?" from the status alone loses to a
    /// redelivered receipt arriving beside the sweep, and the cost of losing is a double-counted
    /// execution in every downstream figure. §30 forbids exactly that, so the claim is persisted.
    /// </para>
    /// </summary>
    public DateTimeOffset? ReportedAt { get; private set; }

    public IReadOnlyList<AutomationRunConditionOutcome> ConditionOutcomes => _conditionOutcomes;

    public IReadOnlyList<AutomationRunActionOutcome> ActionOutcomes => _actionOutcomes;

    public static AutomationRun Start(
        AutomationId automationId,
        string automationName,
        Guid ownerId,
        AutomationOwnerKind ownerKind,
        AutomationRunReason reason,
        string triggerEventId,
        string triggerKey,
        string triggerSummary,
        DateTimeOffset triggeredAt,
        DateTimeOffset startedAt,
        string correlationId,
        bool isSimulated,
        int chainDepth,
        string chainId) =>
        new(AutomationRunId.New())
        {
            AutomationId = automationId,
            AutomationName = Cap(automationName, 120) ?? string.Empty,
            OwnerId = ownerId,
            OwnerKind = ownerKind,
            Status = AutomationRunStatus.Running,
            Reason = reason,
            TriggerEventId = Cap(triggerEventId, 128) ?? string.Empty,
            TriggerKey = triggerKey,
            TriggerSummary = Cap(triggerSummary, 300) ?? string.Empty,
            TriggeredAt = triggeredAt,
            StartedAt = startedAt,
            CorrelationId = correlationId,
            IsSimulated = isSimulated,
            FailureKind = AutomationFailureKind.None,
            ChainDepth = chainDepth,
            ChainId = Cap(chainId, 64) ?? string.Empty
        };

    public void RecordCondition(
        AutomationConditionId conditionId,
        string label,
        bool matched,
        bool evaluatedByServer,
        string explanation) =>
        _conditionOutcomes.Add(AutomationRunConditionOutcome.Create(
            Id, conditionId, label, matched, evaluatedByServer, explanation));

    public AutomationRunActionOutcome RecordAction(
        AutomationActionId actionId,
        string actionKey,
        string label,
        AutomationActionOutcomeStatus status,
        string? suppressionReason,
        AutomationFailureKind failureKind,
        string? message,
        string? resultKind,
        string? resultId,
        string? deepLink,
        DateTimeOffset? completedAt)
    {
        var outcome = AutomationRunActionOutcome.Create(
            Id, actionId, actionKey, label, status, suppressionReason, failureKind, message,
            resultKind, resultId, deepLink, completedAt);

        _actionOutcomes.Add(outcome);
        return outcome;
    }

    /// <summary>
    /// Settles a pending notification action from the Communication Platform's own receipt.
    /// <para>
    /// Returns false when the action is not on this run or has already settled — which is the normal
    /// answer to a redelivered receipt, and must not be an error.
    /// </para>
    /// </summary>
    public bool SettleAction(
        AutomationActionId actionId,
        AutomationActionOutcomeStatus status,
        string? suppressionReason,
        DateTimeOffset completedAt)
    {
        var outcome = _actionOutcomes.FirstOrDefault(
            candidate => candidate.ActionId == actionId &&
                         candidate.Status == AutomationActionOutcomeStatus.Pending);

        if (outcome is null)
        {
            return false;
        }

        outcome.Settle(status, suppressionReason, completedAt);
        return true;
    }

    /// <summary>The trigger fired and a condition said no. Not a failure, and never rendered as one.</summary>
    public void CompleteAsSkipped(string reason, DateTimeOffset completedAt)
    {
        Status = AutomationRunStatus.Skipped;
        FailureMessage = Cap(reason, 300);
        Finish(completedAt);
    }

    public void CompleteAsCancelled(string reason, DateTimeOffset completedAt)
    {
        Status = AutomationRunStatus.Cancelled;
        FailureMessage = Cap(reason, 300);
        Finish(completedAt);
    }

    public void Fail(
        AutomationFailureKind kind, string message, string? recoveryHint, DateTimeOffset completedAt)
    {
        Status = kind == AutomationFailureKind.Timeout
            ? AutomationRunStatus.TimedOut
            : AutomationRunStatus.Failed;
        FailureKind = kind;
        FailureMessage = Cap(message, 300);
        RecoveryHint = Cap(recoveryHint, 300);
        Finish(completedAt);
    }

    public void MarkDeduplicated() => WasDeduplicated = true;

    /// <summary>
    /// Claims the right to report this run's outcome, exactly once.
    /// <para>
    /// Returns false when the run has not finished, or when somebody has already reported it — both
    /// of which are ordinary answers rather than errors. A still-running run has no outcome to
    /// announce, and a second claim is what a redelivered settlement receipt looks like.
    /// </para>
    /// </summary>
    public bool TryClaimReport(DateTimeOffset now)
    {
        if (ReportedAt is not null || Status == AutomationRunStatus.Running)
        {
            return false;
        }

        ReportedAt = now;
        return true;
    }

    /// <summary>
    /// Derives the terminal status from what the actions actually did.
    /// <para>
    /// <b>Suppressed counts as done.</b> A member's own preferences deciding nothing should be sent is
    /// the feature working, and a run reported as failed because quiet hours held a message would
    /// teach members to distrust it. Only a genuine failure makes a run partial or failed, and a run
    /// with a mix is <c>PartiallyCompleted</c> — a boolean here would force one of two lies.
    /// </para>
    /// <para>
    /// A run with a still-pending action stays <c>Running</c>: the notification platform has not
    /// answered yet, and claiming a terminal status before it does is the frontend's §77 violation
    /// committed on the server.
    /// </para>
    /// </summary>
    public void CompleteFromOutcomes(DateTimeOffset completedAt)
    {
        if (_actionOutcomes.Any(outcome => outcome.Status == AutomationActionOutcomeStatus.Pending))
        {
            return;
        }

        var failed = _actionOutcomes.Count(
            outcome => outcome.Status == AutomationActionOutcomeStatus.Failed);
        var done = _actionOutcomes.Count(
            outcome => outcome.Status is AutomationActionOutcomeStatus.Succeeded
                or AutomationActionOutcomeStatus.Suppressed
                or AutomationActionOutcomeStatus.Skipped);

        Status = failed switch
        {
            0 => AutomationRunStatus.Completed,
            _ when done == 0 => AutomationRunStatus.Failed,
            _ => AutomationRunStatus.PartiallyCompleted
        };

        if (failed > 0)
        {
            var firstFailure = _actionOutcomes.First(
                outcome => outcome.Status == AutomationActionOutcomeStatus.Failed);

            FailureKind = firstFailure.FailureKind;
            FailureMessage = firstFailure.Message;
        }

        Finish(completedAt);
    }

    private void Finish(DateTimeOffset completedAt)
    {
        CompletedAt = completedAt;
        DurationMs = (long)(completedAt - StartedAt).TotalMilliseconds;
    }

    private static string? Cap(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null
            : value.Length <= max ? value : value[..max];
}

/// <summary>
/// What one condition decided, and who decided it.
/// <para>
/// <c>EvaluatedByServer</c> is §9's requirement made observable rather than assumed: a sensitive
/// condition that reports the client in a real run is a defect, and the run detail shows it instead
/// of hiding it.
/// </para>
/// </summary>
public sealed class AutomationRunConditionOutcome : Entity<Guid>
{
    private AutomationRunConditionOutcome() { }

    private AutomationRunConditionOutcome(Guid id) : base(id) { }

    public AutomationRunId RunId { get; private set; } = null!;

    public AutomationConditionId ConditionId { get; private set; } = null!;

    /// <summary>The condition's member-facing label at the time it ran.</summary>
    public string Label { get; private set; } = string.Empty;

    public bool Matched { get; private set; }

    public bool EvaluatedByServer { get; private set; }

    /// <summary>Why, in plain words. <b>Never the member's actual value when the condition is sensitive.</b></summary>
    public string Explanation { get; private set; } = string.Empty;

    internal static AutomationRunConditionOutcome Create(
        AutomationRunId runId,
        AutomationConditionId conditionId,
        string label,
        bool matched,
        bool evaluatedByServer,
        string explanation) =>
        new(Guid.NewGuid())
        {
            RunId = runId,
            ConditionId = conditionId,
            Label = label.Length <= 120 ? label : label[..120],
            Matched = matched,
            EvaluatedByServer = evaluatedByServer,
            Explanation = explanation.Length <= 300 ? explanation : explanation[..300]
        };
}

/// <summary>What one action produced.</summary>
public sealed class AutomationRunActionOutcome : Entity<Guid>
{
    private AutomationRunActionOutcome() { }

    private AutomationRunActionOutcome(Guid id) : base(id) { }

    public AutomationRunId RunId { get; private set; } = null!;

    public AutomationActionId ActionId { get; private set; } = null!;

    public string ActionKey { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public AutomationActionOutcomeStatus Status { get; private set; }

    /// <summary>Why nothing was sent, when nothing was. Its own field, not a failure message.</summary>
    public string? SuppressionReason { get; private set; }

    public AutomationFailureKind FailureKind { get; private set; }

    public string? Message { get; private set; }

    /// <summary>The notification, calendar entry or screen this produced.</summary>
    public string? ResultKind { get; private set; }

    public string? ResultId { get; private set; }

    public string? DeepLink { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>How many times this action has been attempted. Retry is per action, never per run.</summary>
    public int Attempts { get; private set; }

    internal static AutomationRunActionOutcome Create(
        AutomationRunId runId,
        AutomationActionId actionId,
        string actionKey,
        string label,
        AutomationActionOutcomeStatus status,
        string? suppressionReason,
        AutomationFailureKind failureKind,
        string? message,
        string? resultKind,
        string? resultId,
        string? deepLink,
        DateTimeOffset? completedAt) =>
        new(Guid.NewGuid())
        {
            RunId = runId,
            ActionId = actionId,
            ActionKey = actionKey,
            Label = Cap(label, 120) ?? string.Empty,
            Status = status,
            SuppressionReason = Cap(suppressionReason, 300),
            FailureKind = failureKind,
            Message = Cap(message, 300),
            ResultKind = Cap(resultKind, 32),
            ResultId = Cap(resultId, 64),
            DeepLink = Cap(deepLink, 300),
            CompletedAt = completedAt,
            Attempts = 1
        };

    internal void Settle(
        AutomationActionOutcomeStatus status, string? suppressionReason, DateTimeOffset completedAt)
    {
        Status = status;
        SuppressionReason = Cap(suppressionReason, 300);
        CompletedAt = completedAt;
    }

    internal void RecordAttempt() => Attempts++;

    private static string? Cap(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null
            : value.Length <= max ? value : value[..max];
}
