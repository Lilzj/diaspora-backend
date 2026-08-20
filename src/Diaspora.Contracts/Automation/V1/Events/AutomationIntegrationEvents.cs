using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Automation.V1.Events;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// The Automation Platform's published contracts.
//
// Four, and the count is the point. An automation platform that published a contract per trigger
// would be a second event bus with a different name; what it actually announces is only what other
// modules genuinely need: that a delivery was asked for, that a delivery was settled, that a rule
// ran, and that a rule stopped working.
//
// ── Why the delivery request is a contract rather than a call ────────────────────────────────────
//
// FRONTEND-PHASE18-GAP-024 asks for "an endpoint or internal contract by which the automation
// platform asks B13 to deliver". The platform's standing rule — asserted by six architecture suites
// and by DSP2003 — is that a business module publishes a fact and stops, and the Communication
// Platform decides whether, how and to whom it is delivered. An automation asking for a delivery is
// exactly that shape: it names a recipient, an audience and the values to substitute, and it names
// no channel, no template, no priority and no category. Those are B13's routing rules, which are
// reference data.
//
// It also means the request joins the run's transaction. The run record and the promise to deliver
// commit together or not at all, which is what stops a run that says "sent" beside a message nobody
// received.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// An automation decided somebody should be told something.
/// <para>
/// <b>One recipient per event.</b> A community broadcast produces one of these per member, published
/// in batches by the engine, because B13's dispatcher is per-recipient and because a single event
/// carrying a recipient list would be a recipient list on a bus — which is the shape §50 forbids and
/// the shape a mass-messaging abuse would take.
/// </para>
/// <para>
/// <b>It carries no channel, no template and no priority.</b> The engine names an audience;
/// <c>AutomationNotificationRuleContributor</c> maps that audience to the rest. An automation that
/// could name its own channel would be an automation that could route around a member's preferences.
/// </para>
/// </summary>
public sealed record AutomationDeliveryRequestedIntegrationEvent : IntegrationEvent
{
    /// <summary>The automation that asked. Carried so the inbox can offer to pause it.</summary>
    public required Guid AutomationId { get; init; }

    /// <summary>The run that asked. The idempotency subject, with the action.</summary>
    public required Guid RunId { get; init; }

    /// <summary>Which action within that run. One run may notify more than once.</summary>
    public required Guid ActionId { get; init; }

    /// <summary>Who is to be told.</summary>
    public required Guid RecipientUserId { get; init; }

    /// <summary>
    /// Which rule applies — <c>owner</c>, <c>reminder</c>, <c>community</c>, <c>organization</c>,
    /// <c>follower</c>, <c>saved-search</c>. The catalogue is keyed on it.
    /// </summary>
    public required string Audience { get; init; }

    /// <summary>The automation's own name, for the message's title.</summary>
    public required string AutomationName { get; init; }

    /// <summary>
    /// What set the automation off, in the member's words.
    /// <para>
    /// <b>Written from the trigger's public subject, never from a condition comparison.</b> "A job
    /// matching Lagos · Software", not "your balance is £412.60". A message in an inbox has a longer
    /// retention than the data a sensitive condition compared, and quoting one here would be the
    /// place that leaked.
    /// </para>
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>The owning scope's display name, for a broadcast's title. Null for a personal one.</summary>
    public string? ScopeName { get; init; }

    /// <summary>What the message is about — <c>event</c>, <c>listing</c>, <c>job</c>.</summary>
    public string? SubjectType { get; init; }

    public string? SubjectId { get; init; }

    /// <summary>The subject's own title, for a reminder's wording.</summary>
    public string? SubjectTitle { get; init; }

    /// <summary>
    /// The moment a scheduled rule computes its offset against — the event start, the viewing.
    /// <para>
    /// Absolute, and already resolved into the subject's own zone by the engine. B13 renders it in
    /// the reader's wall clock.
    /// </para>
    /// </summary>
    public DateTimeOffset? SubjectMoment { get; init; }

    /// <summary>Chooses the plural form, for a digest of matches. Null when it is not about a count.</summary>
    public int? Count { get; init; }

    /// <summary>
    /// Asks B13 for a settlement receipt.
    /// <para>
    /// <b>The reason this field exists.</b> An automation's run record has to be able to say what
    /// happened, and "nothing happened" has four meanings a member needs told apart: no rule matched,
    /// the member is opted out, quiet hours held it, or it was a duplicate. B13 already publishes
    /// <c>NotificationCreated</c> for the positive case and published nothing at all for the others,
    /// so a run's action outcome would have hung on <c>pending</c> forever whenever a member's own
    /// preferences worked correctly — which is the most common outcome in a healthy system.
    /// </para>
    /// <para>
    /// Opt-in rather than always-on, so no other module's dispatch volume changes.
    /// </para>
    /// </summary>
    public bool RequiresSettlementReceipt { get; init; } = true;
}

/// <summary>
/// What became of an automation's delivery request.
/// <para>
/// Published by the Communication Platform, consumed by the automation engine, and the reason a run
/// record can distinguish "your settings meant nothing was sent" from "it failed". §36 and §38 both
/// depend on that difference and neither can be honoured without a fact carrying it.
/// </para>
/// </summary>
public sealed record AutomationDeliverySettledIntegrationEvent : IntegrationEvent
{
    public required Guid AutomationId { get; init; }

    public required Guid RunId { get; init; }

    public required Guid ActionId { get; init; }

    public required Guid RecipientUserId { get; init; }

    /// <summary>How many notifications were created for this recipient. Zero is an ordinary answer.</summary>
    public required int Accepted { get; init; }

    /// <summary>True when a rule matched but an equivalent message already existed in the window.</summary>
    public bool WasDeduplicated { get; init; }

    /// <summary>True when no rule in the catalogue matches the event and audience.</summary>
    public bool NoRuleMatched { get; init; }

    /// <summary>
    /// Why nothing was sent, in words a member can read.
    /// <para>
    /// Null when something was. Never a queue name, never a host, never a stack trace — a run record
    /// is a member-facing surface and §36 forbids internal infrastructure detail on it.
    /// </para>
    /// </summary>
    public string? SuppressionReason { get; init; }
}

/// <summary>
/// An automation ran.
/// <para>
/// Published on every terminal run so B19 can count executions, failures and durations without this
/// module holding a second analytics store. It carries keys and counts and no rule contents: §52
/// forbids a member's parameters entering a telemetry stream with a different retention policy.
/// </para>
/// </summary>
public sealed record AutomationRunCompletedIntegrationEvent : IntegrationEvent
{
    public required Guid AutomationId { get; init; }

    public required Guid RunId { get; init; }

    public required Guid OwnerId { get; init; }

    /// <summary>The owner kind's name. <c>User</c>, <c>Community</c>.</summary>
    public required string OwnerKind { get; init; }

    /// <summary>The trigger's catalogue key. A key, never a parameter.</summary>
    public required string TriggerKey { get; init; }

    /// <summary><c>Completed</c>, <c>PartiallyCompleted</c>, <c>Failed</c>, <c>Skipped</c>, <c>TimedOut</c>.</summary>
    public required string Status { get; init; }

    public required int ActionsAttempted { get; init; }

    public required int ActionsSucceeded { get; init; }

    public required int ActionsSuppressed { get; init; }

    public required long DurationMs { get; init; }

    /// <summary>The failure's kind, never its message. Null on success.</summary>
    public string? FailureKind { get; init; }
}

/// <summary>
/// An automation was created, switched on, paused, switched off or removed.
/// <para>
/// <b>The fifth contract, added by B24, and the count going up needs defending.</b> The four above
/// were chosen because an automation platform that published a contract per trigger would be a second
/// event bus with a different name. This one is not a trigger — it is the platform's own lifecycle,
/// and it is the denominator every execution figure is read against. A failure rate that climbs after
/// an activation spike is a template people have just discovered; the same rate with no activations
/// is a regression. Without this fact those are the same graph, and B19 has no way to tell them apart.
/// </para>
/// <para>
/// <b>Six changes on one contract rather than six contracts.</b> They share a subject, a shape and an
/// audience; splitting them would give the analytics platform six consumers that differ only in a
/// string, and would make "how many automations exist" a six-way union.
/// </para>
/// <para>
/// <b>It carries keys and counts and no rule contents.</b> No automation name — members write those
/// freely and sometimes personally — no condition values, no schedule times, no recipient counts.
/// The trigger key answers every product question worth asking and identifies nobody. §52, and the
/// same rule the client's own allow-list has enforced since Phase 7.
/// </para>
/// </summary>
public sealed record AutomationLifecycleChangedIntegrationEvent : IntegrationEvent
{
    public required Guid AutomationId { get; init; }

    public required Guid OwnerId { get; init; }

    /// <summary>The owner kind's name. <c>User</c>, <c>Community</c>, <c>Organization</c>.</summary>
    public required string OwnerKind { get; init; }

    /// <summary>
    /// What happened: <c>created</c>, <c>activated</c>, <c>paused</c>, <c>disabled</c>,
    /// <c>deleted</c>.
    /// <para>
    /// <c>paused</c> and <c>disabled</c> stay apart because members mean different things by them —
    /// "stop for now" against "stop, and stop counting" — and merging them would answer neither of
    /// the two questions anybody asks.
    /// </para>
    /// </summary>
    public required string Change { get; init; }

    /// <summary>The trigger's catalogue key. A key, never a parameter.</summary>
    public required string TriggerKey { get; init; }

    /// <summary>How many conditions the rule has. A shape, never their contents.</summary>
    public required int ConditionCount { get; init; }

    /// <summary>How many actions the rule has.</summary>
    public required int ActionCount { get; init; }

    /// <summary>
    /// Which catalogue template it was built from, when it was built from one.
    /// <para>
    /// The one identifier here that is not the automation's own, and it is safe because a template id
    /// names a platform-published catalogue entry rather than anything a member wrote. It is what
    /// makes "which templates actually help people" answerable, which the template gallery needs in
    /// order to order itself by something better than an author's guess.
    /// </para>
    /// </summary>
    public string? TemplateId { get; init; }
}

/// <summary>
/// An automation stopped being able to run and needs its owner.
/// <para>
/// Published when the engine disables or flags a rule for a reason the owner has to act on: a
/// capability they no longer hold, a trigger whose contract stopped existing, a chain that ran away.
/// B13 routes it; this module decides only that it happened.
/// </para>
/// </summary>
public sealed record AutomationNeedsAttentionIntegrationEvent : IntegrationEvent
{
    public required Guid AutomationId { get; init; }

    public required Guid OwnerId { get; init; }

    public required string OwnerKind { get; init; }

    public required string AutomationName { get; init; }

    /// <summary>What stopped working, in the member's words.</summary>
    public required string Reason { get; init; }

    /// <summary>True when the platform switched it off rather than merely flagging it.</summary>
    public required bool WasDisabled { get; init; }
}
