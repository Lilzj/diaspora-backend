using Diaspora.Platform.Operations.Alerting;

namespace Diaspora.Modules.Automation.Observability;

/// <summary>
/// What is worth waking somebody for, and what is worth looking at on Monday.
/// <para>
/// <b>B24-G65 · §39.</b> B17 owns operational alerting and this module owns none of it. What a
/// contributor supplies is the rule — which metric, which threshold, how long for, how serious, and
/// what to do about it — declared next to the code that emits the metric, which is
/// <c>AlertContracts</c>'s own stated reason for existing. Nothing here is evaluated in this process;
/// an alerting system that runs inside the thing it monitors goes quiet at exactly the moment it is
/// needed.
/// </para>
/// <para>
/// <b>Every rule below has a runbook line, and that is a requirement rather than a courtesy.</b> The
/// platform's own contract says an alert with no stated response gets acknowledged, then ignored,
/// then muted.
/// </para>
///
/// <para><b>Two of these watch for silence rather than for noise, and they are the important ones.</b></para>
/// <para>
/// An automation platform fails quietly. A consumer that stops subscribing, a lease that never
/// releases, a schedule sweep that dies — none of them raises an error, and all of them look
/// identical to a platform where nobody happens to have automated anything. A failure-rate alert
/// cannot see any of it, because the failure rate of zero executions is zero.
/// </para>
/// </summary>
public sealed class AutomationAlertRuleContributor : IAlertRuleContributor
{
    public string Module => AutomationModuleMetadata.Name;

    public IEnumerable<AlertRule> Rules =>
    [
        // ── Failure ──────────────────────────────────────────────────────────────────────────────

        new AlertRule(
            Name: "automation.failure_rate.elevated",
            Metric: "diaspora.automation.failures",
            Comparison: AlertComparison.GreaterThan,
            Threshold: 10,
            For: TimeSpan.FromMinutes(15),
            Severity: AlertSeverity.Warning,
            Module: AutomationModuleMetadata.Name,
            Description:
                "Automation runs are failing faster than ten in fifteen minutes. Break down by "
                + "`kind` before anything else: one failure kind dominating is a dependency, and an "
                + "even spread is the engine.",
            Runbook:
                "Group `diaspora.automation.failures` by `kind`. ProviderUnavailable or "
                + "BackendUnavailable means follow the dependency, not this module. PermissionDenied "
                + "in volume means a role change swept a scope — check whether a community's "
                + "moderators changed. ActionFailed in volume is this module's."),

        new AlertRule(
            Name: "automation.dead_letters.any",
            Metric: "diaspora.automation.dead_letters",
            Comparison: AlertComparison.GreaterThan,
            Threshold: 0,
            For: TimeSpan.FromMinutes(30),
            Severity: AlertSeverity.Warning,
            Module: AutomationModuleMetadata.Name,
            Description:
                "Scheduled work was given up on. A dead-lettered occurrence is a member's reminder "
                + "that will never arrive, and nothing retries it.",
            Runbook:
                "Break down by `kind` and `trigger`. Read the failed rows in `automation_occurrences` "
                + "or `automation_subject_horizons` for the recorded error. There is no replay: a "
                + "dead letter is kept for inspection, so the decision is whether the underlying "
                + "fault is fixed and whether members need telling."),

        new AlertRule(
            Name: "automation.history.write_failures.any",
            Metric: "diaspora.automation.history.write_failures",
            Comparison: AlertComparison.GreaterThan,
            Threshold: 0,
            For: TimeSpan.FromMinutes(5),
            Severity: AlertSeverity.Critical,
            Module: AutomationModuleMetadata.Name,
            Description:
                "A run happened and could not be written down. FRONTEND-PHASE18-GAP-004's own "
                + "words: a run that happened and was not recorded is a member telling support "
                + "\"it did not fire\" and nobody able to say otherwise.",
            Runbook:
                "Check the `automation` schema's health and the database's. The outbox will "
                + "redeliver the triggering fact and the run will be written on a later attempt, so "
                + "a brief spike during a failover is expected; a sustained one means history is "
                + "being lost and the module should be shed rather than left half-recording."),

        // ── Refusal ──────────────────────────────────────────────────────────────────────────────

        new AlertRule(
            Name: "automation.chain_refusals.any",
            Metric: "diaspora.automation.chain_refusals",
            Comparison: AlertComparison.GreaterThan,
            Threshold: 0,
            For: TimeSpan.FromMinutes(10),
            Severity: AlertSeverity.Warning,
            Module: AutomationModuleMetadata.Name,
            Description:
                "Two automations are reacting to each other's actions. The depth ceiling has "
                + "stopped it becoming an outage; this is how anybody finds out it happened.",
            Runbook:
                "Take the `trigger` label and find runs on it with a shared `ChainId` in "
                + "`automation_runs`. The chain's first run names the rule that starts it. Pausing "
                + "either automation breaks the loop; the member needs telling which and why."),

        new AlertRule(
            Name: "automation.rate_refusals.elevated",
            Metric: "diaspora.automation.rate_refusals",
            Comparison: AlertComparison.GreaterThan,
            Threshold: 50,
            For: TimeSpan.FromMinutes(10),
            Severity: AlertSeverity.Warning,
            Module: AutomationModuleMetadata.Name,
            Description:
                "Automations are hitting their execution ceilings in volume. Usually a bulk import "
                + "matching a rule that notifies people.",
            Runbook:
                "Identify the publishing module from the `trigger` label. If a backfill is running, "
                + "the ceiling is doing its job and the alert is informational. If not, a single "
                + "owner may be generating the volume — `MaximumRunsPerOwnerPerWindow` is the "
                + "backstop and is configurable without a deploy."),

        new AlertRule(
            Name: "automation.denied.elevated",
            Metric: "diaspora.automation.denied",
            Comparison: AlertComparison.GreaterThan,
            Threshold: 20,
            For: TimeSpan.FromMinutes(15),
            Severity: AlertSeverity.Warning,
            Module: AutomationModuleMetadata.Name,
            Description:
                "Actions are being refused at dispatch because their owners no longer hold the "
                + "capability. A spike means either a role change swept a scope, or a client is "
                + "offering something it should not.",
            Runbook:
                "Break down by `capability`. One capability dominating points at a role or "
                + "entitlement change; a spread across capabilities points at the client offering "
                + "actions it has not checked."),

        // ── Silence ──────────────────────────────────────────────────────────────────────────────

        new AlertRule(
            Name: "automation.triggers.silent",
            Metric: "diaspora.automation.triggers.observed",
            Comparison: AlertComparison.LessThan,
            Threshold: 1,
            For: TimeSpan.FromHours(2),
            Severity: AlertSeverity.Critical,
            Module: AutomationModuleMetadata.Name,
            Description:
                "The engine has observed no facts at all for two hours. On a platform with active "
                + "automations this is not quiet, it is disconnected — a consumer that stopped "
                + "subscribing, or an outbox that stopped dispatching.",
            Runbook:
                "Check `diaspora.outbox.published` first: if the outbox is also silent the fault is "
                + "there and this module is a symptom. If the outbox is publishing and this is "
                + "zero, the automation consumers are not being resolved — check the module's "
                + "registration and the integration-event dispatcher."),

        new AlertRule(
            Name: "automation.scheduler.lag.high",
            Metric: "diaspora.automation.scheduler.lag",
            Comparison: AlertComparison.GreaterThan,
            Threshold: 300_000,
            For: TimeSpan.FromMinutes(15),
            Severity: AlertSeverity.Warning,
            Module: AutomationModuleMetadata.Name,
            Description:
                "Due moments are being picked up more than five minutes late. A member's 07:45 "
                + "reminder arriving at 07:52 is a reminder that failed.",
            Runbook:
                "Break down by `zone`. A single zone an hour late twice a year is a DST fault in "
                + "the schedule calculator. Every zone late together is the sweep falling behind — "
                + "check `SweepBatchSize` against the due backlog, and whether the B17 lease is "
                + "being held by an instance that has stopped."),

        new AlertRule(
            Name: "automation.missed.any",
            Metric: "diaspora.automation.missed",
            Comparison: AlertComparison.GreaterThan,
            Threshold: 0,
            For: TimeSpan.FromMinutes(30),
            Severity: AlertSeverity.Warning,
            Module: AutomationModuleMetadata.Name,
            Description:
                "Scheduled moments passed without being run. These leave no trace in the lag "
                + "histogram, because the samples they would have contributed were never taken.",
            Runbook:
                "Break down by `zone`. Concentrated in one zone means a DST transition; spread "
                + "across zones means the sweep was down for longer than the catch-up window and "
                + "the affected members will need telling that a reminder did not arrive."),

        // ── The one that is not about failure at all ──────────────────────────────────────────────

        new AlertRule(
            Name: "automation.skips.collapsed",
            Metric: "diaspora.automation.skipped",
            Comparison: AlertComparison.LessThan,
            Threshold: 1,
            For: TimeSpan.FromHours(6),
            Severity: AlertSeverity.Info,
            Module: AutomationModuleMetadata.Name,
            Description:
                "A healthy automation platform skips constantly — the trigger fires and a condition "
                + "says no. A deployment whose skip rate collapses to nothing has either lost a "
                + "condition source or started notifying people it should not, and neither shows up "
                + "in a failure count because neither is a failure.",
            Runbook:
                "Compare against `diaspora.automation.executions` over the same window. Executions "
                + "steady with skips at zero means conditions have stopped blocking: check whether "
                + "`diaspora.automation.conditions` is still recording `unmatched`. Both at zero is "
                + "the silence alert above, not this one.")
    ];
}
