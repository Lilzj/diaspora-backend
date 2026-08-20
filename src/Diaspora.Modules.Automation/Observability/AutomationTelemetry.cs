using System.Diagnostics;
using System.Diagnostics.Metrics;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Automation.Observability;

/// <summary>
/// The module's activity source and meters.
/// <para>
/// <b>Nothing here carries a rule's contents, and the enforcement is the tag set rather than a
/// redaction filter.</b> Every tag value emitted below is either a catalogue key — a trigger, an
/// action, a condition — or one of the closed vocabularies in <see cref="Tags"/>. There is no tag
/// for a member, an owner, an automation name, an automation id, a condition <i>value</i>, a
/// recipient count or a schedule time. §52 forbids rule contents entering a telemetry stream with a
/// different retention policy from the record they came from, and a counter tagged with an
/// automation id would be a per-member index of what people automate, with a metrics bill attached.
/// </para>
/// <para>
/// <b>B24 put the identifiers on the span instead, and the distinction is the whole design.</b> An
/// automation id, a run id and a chain id are on <c>automation.execute</c>: sampled, retained on
/// B17's terms, and read one trace at a time by an operator who already has infrastructure access. A
/// metric is unsampled, separately retained and queryable in aggregate, which is what would make the
/// same values a searchable index of automation activity per rule.
/// </para>
/// <para>
/// <b>The pairings are how this file is read.</b> Three of them, and each answers a question no
/// single counter can:
/// </para>
/// <list type="bullet">
///   <item>
///     <see cref="Skipped"/> against <see cref="Executions"/>. A healthy automation platform skips
///     constantly — the trigger fired and a condition said no — and a deployment whose skip rate
///     suddenly collapses has either lost a condition source or started notifying people it should
///     not. Neither is visible from a failure count, because neither is a failure.
///   </item>
///   <item>
///     <see cref="TriggersObserved"/> against <see cref="Triggered"/>. "Did the automation trigger?"
///     has three answers — the fact never arrived, it arrived and nothing watched it, or it arrived
///     and ran — and before B24 the first two were the same silence.
///   </item>
///   <item>
///     <see cref="Settled"/> against <see cref="Executions"/>. Starts are not outcomes. A run whose
///     last action is a notification settles minutes later on another thread, so a platform measured
///     only by starts reports every notify automation as permanently in flight.
///   </item>
/// </list>
/// </summary>
public static class AutomationTelemetry
{
    /// <summary>The name the observability contributor subscribes the host's tracer to.</summary>
    public const string SourceName = "Diaspora.Automation";

    public static ActivitySource Source { get; } = new(SourceName);

    private static readonly Meter Meter = new(SourceName);

    // ── The pipeline ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Every fact the engine looked at, whether or not anything watched it. Tagged by trigger key.
    /// <para>
    /// <b>B24: the denominator <see cref="Triggered"/> never had.</b> "Did the automation trigger?"
    /// has three answers — the fact never arrived, it arrived and nothing watched it, or it arrived
    /// and ran — and until this counter existed the first two were the same silence. A consumer that
    /// stops publishing and a catalogue key nobody uses look identical from a matched count.
    /// </para>
    /// </summary>
    public static Counter<long> TriggersObserved { get; } =
        Meter.CreateCounter<long>(
            "diaspora.automation.triggers.observed", "event",
            "Facts the engine observed, whether or not any automation watched them.");

    /// <summary>Events observed that matched at least one automation. Tagged by trigger key.</summary>
    public static Counter<long> Triggered { get; } =
        Meter.CreateCounter<long>(
            "diaspora.automation.triggered", "event",
            "Facts observed that matched at least one active automation.");

    /// <summary>Runs started. Tagged by trigger key and reason.</summary>
    public static Counter<long> Executions { get; } =
        Meter.CreateCounter<long>(
            "diaspora.automation.executions", "run", "AutomationDefinition runs started.");

    /// <summary>
    /// Runs that reached a terminal state. Tagged by trigger key and terminal status.
    /// <para>
    /// <b>B24: <see cref="Executions"/> counts starts, and a start is not an outcome.</b> A run whose
    /// last action is a notification settles minutes later, from B13's receipt, on a different thread
    /// — so a platform measured only by starts reports every notify automation as permanently
    /// in-flight. Recorded once per run, from the run's own terminal status, by
    /// <c>AutomationRunReporter</c>; every path that settles a run goes through it.
    /// </para>
    /// </summary>
    public static Counter<long> Settled { get; } =
        Meter.CreateCounter<long>(
            "diaspora.automation.settled", "run",
            "AutomationDefinition runs that reached a terminal state.");

    /// <summary>Runs that ended other than completed. Tagged by failure kind.</summary>
    public static Counter<long> Failures { get; } =
        Meter.CreateCounter<long>(
            "diaspora.automation.failures", "run", "AutomationDefinition runs that ended in a failure.");

    /// <summary>
    /// Runs that did nothing because a condition said so.
    /// <para>
    /// Not a failure and never alerted on as one. Tagged with <c>reason</c> so the two kinds stay
    /// apart: a condition that did not match is the feature working, and a condition the platform
    /// could not answer is a gap.
    /// </para>
    /// </summary>
    public static Counter<long> Skipped { get; } =
        Meter.CreateCounter<long>(
            "diaspora.automation.skipped", "run",
            "AutomationDefinition runs where the trigger fired and no action was taken.");

    /// <summary>End-to-end duration from fact to last action. Tagged by trigger key.</summary>
    public static Histogram<double> Duration { get; } =
        Meter.CreateHistogram<double>(
            "diaspora.automation.duration", "ms",
            "Time from the observed fact to the last dispatched action.");

    /// <summary>
    /// How long a fact waited between happening and a run starting on it. Tagged by trigger and reason.
    /// <para>
    /// <b>B24 §16: this is what separates "the platform is slow" from "delivery is slow".</b>
    /// <see cref="Duration"/> is measured from the fact, so outbox backlog and a slow action are one
    /// number in it. Splitting the queue delay out means an operator can tell a struggling engine
    /// from a struggling relay without reading two dashboards.
    /// </para>
    /// </summary>
    public static Histogram<double> TriggerLatency { get; } =
        Meter.CreateHistogram<double>(
            "diaspora.automation.trigger.latency", "ms",
            "Time between an observed fact occurring and a run starting on it.");

    /// <summary>
    /// Conditions evaluated. Tagged by condition key and outcome.
    /// <para>
    /// Outcome is <c>matched</c>, <c>unmatched</c> or <c>unevaluable</c>, and the third is the one
    /// worth watching: a condition the platform cannot answer is a gap wearing a skip's clothes.
    /// </para>
    /// </summary>
    public static Counter<long> ConditionEvaluations { get; } =
        Meter.CreateCounter<long>(
            "diaspora.automation.conditions", "condition",
            "AutomationDefinition conditions evaluated.");

    /// <summary>
    /// How long a run's whole condition pass took, resolution included. Tagged by trigger key.
    /// <para>
    /// Wave 12 gave conditions the ability to ask other modules for answers a payload does not carry.
    /// That turned condition evaluation from arithmetic into I/O, and I/O that nobody times is I/O
    /// that becomes the platform's slowest step without anybody noticing.
    /// </para>
    /// </summary>
    public static Histogram<double> ConditionDuration { get; } =
        Meter.CreateHistogram<double>(
            "diaspora.automation.conditions.duration", "ms",
            "Time to resolve and evaluate one run's conditions.");

    /// <summary>Actions attempted. Tagged by action key and outcome.</summary>
    public static Counter<long> Actions { get; } =
        Meter.CreateCounter<long>(
            "diaspora.automation.actions", "action", "AutomationDefinition actions attempted.");

    /// <summary>How long one action took. Tagged by action key.</summary>
    public static Histogram<double> ActionDuration { get; } =
        Meter.CreateHistogram<double>(
            "diaspora.automation.actions.duration", "ms",
            "Time to dispatch one automation action.");

    /// <summary>
    /// Retried attempts at a scheduled unit of work. Tagged by what was retried and why.
    /// <para>
    /// <b>Retry here is per occurrence, not per action.</b> An action is attempted once inside a run;
    /// what retries is the scheduled row that produced the run, and the platform's other retry — an
    /// outbox redelivery — belongs to <c>diaspora.outbox.retried</c> and is not counted twice here.
    /// </para>
    /// </summary>
    public static Counter<long> Retries { get; } =
        Meter.CreateCounter<long>(
            "diaspora.automation.retries", "attempt",
            "Scheduled automation work retried after a failure.");

    /// <summary>Due occurrences given up on. Kept for inspection, never dropped.</summary>
    public static Counter<long> DeadLetters { get; } =
        Meter.CreateCounter<long>(
            "diaspora.automation.dead_letters", "occurrence",
            "Scheduled occurrences that failed enough times to be given up on.");

    /// <summary>
    /// Due moments that were never run. Tagged by kind and timezone.
    /// <para>
    /// <b>The DST signal.</b> A catch-up window that silently drops a zone's occurrences twice a year
    /// is invisible from a lag histogram — the occurrences that were dropped are precisely the ones
    /// that never recorded a lag. This counter is how anybody finds out.
    /// </para>
    /// </summary>
    public static Counter<long> Missed { get; } =
        Meter.CreateCounter<long>(
            "diaspora.automation.missed", "occurrence",
            "Scheduled moments that passed without being run.");

    /// <summary>
    /// Changes to a rule's configuration. Tagged by change and owner kind.
    /// <para>
    /// The denominator for every execution figure. A failure rate that climbs after an activation
    /// spike is a template people just discovered; the same rate with no activations is a regression,
    /// and without a lifecycle count the two are the same graph.
    /// </para>
    /// </summary>
    public static Counter<long> Lifecycle { get; } =
        Meter.CreateCounter<long>(
            "diaspora.automation.lifecycle", "change",
            "Automations created, changed, activated, paused, disabled or deleted.");

    /// <summary>
    /// A run that happened and could not be written down. Tagged by the stage that failed.
    /// <para>
    /// FRONTEND-PHASE18-GAP-004 asks for exactly this and calls it alertable: <i>"a run that happened
    /// and was not recorded is a member telling support 'it did not fire' and nobody able to say
    /// otherwise."</i> Any non-zero value is a finding.
    /// </para>
    /// </summary>
    public static Counter<long> HistoryWriteFailures { get; } =
        Meter.CreateCounter<long>(
            "diaspora.automation.history.write_failures", "failure",
            "Runs whose history or announcement could not be written.");

    /// <summary>
    /// Redeliveries the engine recognised.
    /// <para>
    /// A non-zero rate is the outbox working, not a defect. A rate that reaches most of the execution
    /// count is a consumer that is not acknowledging.
    /// </para>
    /// </summary>
    public static Counter<long> Deduplicated { get; } =
        Meter.CreateCounter<long>(
            "diaspora.automation.deduplicated", "event",
            "Redelivered facts that found their existing run rather than starting a second.");

    // ── The refusals, which are findings rather than metrics ─────────────────────────────────────

    /// <summary>
    /// Actions refused because the owner no longer holds the capability.
    /// <para>
    /// Worth its own counter and worth alerting on. A spike means either a role change swept a
    /// community, or somebody is running rules they wrote before they lost the authority to.
    /// </para>
    /// </summary>
    public static Counter<long> Denied { get; } =
        Meter.CreateCounter<long>(
            "diaspora.automation.denied", "action",
            "Actions refused at dispatch because the owner's capability had gone.");

    /// <summary>
    /// Runs refused because the chain was too deep.
    /// <para>
    /// Any non-zero value is a finding: two automations reacting to each other's actions. The ceiling
    /// stops it becoming an outage; this counter is how anybody learns it happened.
    /// </para>
    /// </summary>
    public static Counter<long> ChainRefusals { get; } =
        Meter.CreateCounter<long>(
            "diaspora.automation.chain_refusals", "run",
            "Runs refused because the automation chain reached its depth ceiling.");

    /// <summary>Runs refused by the per-automation or per-owner rate ceiling.</summary>
    public static Counter<long> RateRefusals { get; } =
        Meter.CreateCounter<long>(
            "diaspora.automation.rate_refusals", "run",
            "Runs refused because an automation or an owner exceeded its execution ceiling.");

    /// <summary>Scheduler lag: how late a due occurrence was picked up. Tagged by timezone.</summary>
    public static Histogram<double> SchedulerLag { get; } =
        Meter.CreateHistogram<double>(
            "diaspora.automation.scheduler.lag", "ms",
            "Time between a scheduled occurrence being due and being claimed.");

    // ── Recording helpers, so a call site cannot invent a tag ────────────────────────────────────

    public static void TriggerObserved(string triggerKey) =>
        TriggersObserved.Add(1, new KeyValuePair<string, object?>("trigger", triggerKey));

    public static void RunStarted(string triggerKey, string reason) =>
        Executions.Add(1, new KeyValuePair<string, object?>("trigger", triggerKey),
            new KeyValuePair<string, object?>("reason", reason));

    /// <summary>
    /// A run reached a terminal state.
    /// <para>
    /// Called once per run, by <c>AutomationRunReporter</c> and nowhere else. Two call sites would be
    /// two chances to double-count a redelivered settlement, which is the one thing §30 forbids
    /// outright.
    /// </para>
    /// </summary>
    public static void RunSettled(string triggerKey, string status) =>
        Settled.Add(1, new KeyValuePair<string, object?>("trigger", triggerKey),
            new KeyValuePair<string, object?>("status", status));

    public static void RunSkipped(string triggerKey, string reason) =>
        Skipped.Add(1, new KeyValuePair<string, object?>("trigger", triggerKey),
            new KeyValuePair<string, object?>("reason", reason));

    public static void RunFailed(string triggerKey, string failureKind) =>
        Failures.Add(1, new KeyValuePair<string, object?>("trigger", triggerKey),
            new KeyValuePair<string, object?>("kind", failureKind));

    public static void ConditionEvaluated(string conditionKey, string outcome) =>
        ConditionEvaluations.Add(1, new KeyValuePair<string, object?>("condition", conditionKey),
            new KeyValuePair<string, object?>("outcome", outcome));

    public static void ActionCompleted(string actionKey, string outcome) =>
        Actions.Add(1, new KeyValuePair<string, object?>("action", actionKey),
            new KeyValuePair<string, object?>("outcome", outcome));

    public static void ActionTook(string actionKey, double milliseconds) =>
        ActionDuration.Record(milliseconds, new KeyValuePair<string, object?>("action", actionKey));

    public static void LifecycleChanged(string change, string ownerKind) =>
        Lifecycle.Add(1, new KeyValuePair<string, object?>("change", change),
            new KeyValuePair<string, object?>("owner_kind", ownerKind));

    /// <summary>A scheduled unit of work was retried. <paramref name="kind"/> is bounded by the sweeps.</summary>
    public static void Retried(string kind, string triggerKey) =>
        Retries.Add(1, new KeyValuePair<string, object?>("kind", kind),
            new KeyValuePair<string, object?>("trigger", triggerKey));

    /// <summary>
    /// Something scheduled was given up on.
    /// <para>
    /// §19's required dimensions, and no more: what it was, what it was for, and how many attempts it
    /// took. Never the owner, never the subject — a dead letter is still somebody's rule.
    /// </para>
    /// </summary>
    public static void DeadLettered(string kind, string triggerKey, int attempts) =>
        DeadLetters.Add(1, new KeyValuePair<string, object?>("kind", kind),
            new KeyValuePair<string, object?>("trigger", triggerKey),
            new KeyValuePair<string, object?>("attempts", Bucket(attempts)));

    public static void MissedOccurrence(string kind, string timeZoneId) =>
        Missed.Add(1, new KeyValuePair<string, object?>("kind", kind),
            new KeyValuePair<string, object?>("zone", timeZoneId));

    public static void HistoryWriteFailed(string stage) =>
        HistoryWriteFailures.Add(1, new KeyValuePair<string, object?>("stage", stage));

    public static void ActionDenied(string actionKey, string capability) =>
        Denied.Add(1, new KeyValuePair<string, object?>("action", actionKey),
            new KeyValuePair<string, object?>("capability", capability));

    /// <summary>Records a broadcast's size as a bucket rather than as a tag, so cardinality stays flat.</summary>
    public static void BroadcastPublished(string actionKey, int recipients) =>
        Actions.Add(1, new KeyValuePair<string, object?>("action", actionKey),
            new KeyValuePair<string, object?>("outcome", "Broadcast"),
            new KeyValuePair<string, object?>("size", Bucket(recipients)));

    private static string Bucket(int value) => value switch
    {
        <= 1 => "1",
        <= 10 => "10",
        <= 100 => "100",
        <= 1000 => "1k",
        _ => "1k+"
    };

    /// <summary>
    /// Every tag <em>value</em> this module may emit that is not already a catalogue key.
    /// <para>
    /// <b>Constants rather than literals, and that is the cardinality control.</b> §12 bans unbounded
    /// metric dimensions; a bounded dimension is only bounded if the set of values is closed
    /// somewhere, and a string literal at a call site closes nothing. Everything else the module tags
    /// with — a trigger, an action, a condition — is already a catalogue key and therefore already
    /// bounded by the catalogue.
    /// </para>
    /// </summary>
    public static class Tags
    {
        /// <summary>
        /// Stands in for a trigger key that cannot be read.
        /// <para>
        /// A scheduled row whose automation has been deleted has no trigger to name, and the
        /// alternative — omitting the tag — produces a second, differently-shaped time series for the
        /// same counter, which is worse for a dashboard than a value that says "we do not know".
        /// </para>
        /// </summary>
        public const string UnknownTrigger = "unknown";

        /// <summary>Why a run did nothing. Paired with <see cref="Skipped"/>.</summary>
        public static class Skip
        {
            /// <summary>A condition said no. The commonest outcome in a healthy platform.</summary>
            public const string Condition = "condition";

            /// <summary>A condition the platform could not answer. A gap, not a decision.</summary>
            public const string Unevaluable = "unevaluable";

            /// <summary>The rule has no actions to perform.</summary>
            public const string NothingToDo = "nothing-to-do";

            /// <summary>Paused, disabled, expired or past its run limit when the fact arrived.</summary>
            public const string NotRunnable = "not-runnable";

            /// <summary>Another instance held the automation's lease. Contention, not failure.</summary>
            public const string Contended = "contended";

            /// <summary>The moment this was about had already passed.</summary>
            public const string Expired = "expired";

            /// <summary>The automation a scheduled row pointed at is gone or stopped.</summary>
            public const string TargetUnavailable = "target-unavailable";
        }

        /// <summary>What a condition decided. Paired with <see cref="ConditionEvaluations"/>.</summary>
        public static class Condition
        {
            public const string Matched = "matched";
            public const string Unmatched = "unmatched";
            public const string Unevaluable = "unevaluable";
        }

        /// <summary>What changed about a rule. Paired with <see cref="Lifecycle"/>.</summary>
        public static class Change
        {
            public const string Created = "created";
            public const string Updated = "updated";
            public const string Activated = "activated";
            public const string Paused = "paused";
            public const string Disabled = "disabled";
            public const string Deleted = "deleted";

            /// <summary>The platform stopped it, rather than its owner. Worth its own value.</summary>
            public const string Flagged = "flagged";
        }

        /// <summary>What kind of scheduled work retried, missed or dead-lettered.</summary>
        public static class Work
        {
            /// <summary>A recurring schedule's due moment.</summary>
            public const string Occurrence = "occurrence";

            /// <summary>A dated subject the platform is watching towards — Wave 12.</summary>
            public const string Horizon = "horizon";

            /// <summary>A member's reminder.</summary>
            public const string Reminder = "reminder";
        }

        /// <summary>Which write failed. Paired with <see cref="HistoryWriteFailures"/>.</summary>
        public static class Stage
        {
            /// <summary>The run row itself, and with it the whole record that it happened.</summary>
            public const string Run = "run";

            /// <summary>The terminal announcement, after the run was safely written.</summary>
            public const string Report = "report";
        }
    }
}

/// <summary>
/// Subscribes the host's tracer and meter to this module's source.
/// <para>
/// A contributor rather than an edit to the host, so the module's telemetry appears wherever the
/// platform's does without <c>Program.cs</c> naming it.
/// </para>
/// </summary>
public sealed class AutomationObservabilityContributor : IObservabilityContributor
{
    public string Module => AutomationModuleMetadata.Name;

    public IEnumerable<string> ActivitySources => [AutomationTelemetry.SourceName];

    public IEnumerable<string> Meters => [AutomationTelemetry.SourceName];
}
