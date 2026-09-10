namespace Diaspora.Modules.Automation.Contracts.V1.Responses;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// The wire shapes, and they are the frontend's.
//
// Every property below matches a field on `src/types/automation.ts` by name and by case. The client's
// `automation-gap-api.ts` declares each route against `/automation-gap/v0` with the note that "when a
// backend phase ships automation, the hook swaps which arm it calls and the request shapes already
// match". They match. Only the prefix changes.
//
// Status and enum values are the client's strings — `ACTIVE`, `PARTIALLY_COMPLETED`, `succeeded` —
// rather than this module's enum names, because the client switches on them and a case difference
// would map every one to an unknown state with nothing failing anywhere. The platform has paid for
// that once already: B6 publishes `listing` and a case-sensitive match mapped every event to Unknown.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Who an automation belongs to and what it acts over.</summary>
public sealed record AutomationScopeResponse
{
    public required string OwnerKind { get; init; }

    public required string OwnerId { get; init; }

    public required string OwnerLabel { get; init; }

    public required IReadOnlyList<string> CommunityIds { get; init; }

    public required bool IsCrossCommunity { get; init; }

    public required IReadOnlyList<string> CountryCodes { get; init; }

    public required IReadOnlyList<string> NeighbourhoodIds { get; init; }
}

public sealed record AutomationTriggerResponse
{
    public required string Key { get; init; }

    public required IReadOnlyDictionary<string, string?> Parameters { get; init; }
}

public sealed record AutomationConditionResponse
{
    public required string Id { get; init; }

    public required string Key { get; init; }

    public required string Operator { get; init; }

    public string? Value { get; init; }

    public string? SecondaryValue { get; init; }
}

public sealed record AutomationActionResponse
{
    public required string Id { get; init; }

    public required string Key { get; init; }

    public required IReadOnlyDictionary<string, string?> Parameters { get; init; }
}

public sealed record AutomationTimezoneResponse
{
    public required string Mode { get; init; }

    public string? IanaId { get; init; }

    public required string Label { get; init; }

    /// <summary>False when the runtime could not resolve the zone. A silent hour is what §42 forbids.</summary>
    public required bool IsExact { get; init; }
}

public sealed record AutomationScheduleResponse
{
    public required string Frequency { get; init; }

    public required AutomationTimezoneResponse Timezone { get; init; }

    public int? Hour { get; init; }

    public int? Minute { get; init; }

    public required IReadOnlyList<int> DaysOfWeek { get; init; }

    public int? DayOfMonth { get; init; }

    public string? StartsAt { get; init; }

    public string? EndsAt { get; init; }

    public int? MaxExecutions { get; init; }

    public AutomationEventOffsetResponse? EventOffset { get; init; }

    public string? CustomDescription { get; init; }
}

public sealed record AutomationEventOffsetResponse
{
    public required int MinutesBefore { get; init; }

    public required string Anchor { get; init; }
}

public sealed record AutomationAuditEntryResponse
{
    public required string Action { get; init; }

    public required string At { get; init; }

    /// <summary>A display name. Never an email, never an internal identifier.</summary>
    public required string ActorLabel { get; init; }

    /// <summary>Always <c>server</c> here. The client's <c>local</c> is its own optimistic state.</summary>
    public required string Provenance { get; init; }
}

public sealed record AutomationDefinitionResponse
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string Status { get; init; }

    public required AutomationScopeResponse Scope { get; init; }

    public required string Source { get; init; }

    public string? TemplateId { get; init; }

    public required AutomationTriggerResponse Trigger { get; init; }

    public required IReadOnlyList<AutomationConditionResponse> Conditions { get; init; }

    public required IReadOnlyList<AutomationActionResponse> Actions { get; init; }

    public AutomationScheduleResponse? Schedule { get; init; }

    /// <summary>
    /// Derived from the parts, never declared.
    /// <para>
    /// <c>CONNECTED</c> when every part of the rule is one this deployment can observe and perform;
    /// <c>PARTIAL</c> when some are; <c>BACKEND_GAP</c> when the trigger is not observable at all. The
    /// client renders it and refuses to promise more than the weakest part.
    /// </para>
    /// </summary>
    public required string Support { get; init; }

    /// <summary>Which parts are not available, named so the client can explain rather than fail.</summary>
    public required IReadOnlyList<string> GapIds { get; init; }

    public required string CreatedAt { get; init; }

    /// <summary>The concurrency precondition the client sends back as <c>expectedUpdatedAt</c>.</summary>
    public required string UpdatedAt { get; init; }

    public string? LastRunAt { get; init; }

    public string? NextRunAt { get; init; }

    public required int ExecutionCount { get; init; }

    public string? LastRunStatus { get; init; }

    public string? AttentionReason { get; init; }

    public required IReadOnlyList<AutomationAuditEntryResponse> Audit { get; init; }

    /// <summary>Always false from this module. Nothing here can produce a simulated definition.</summary>
    public required bool IsSimulated { get; init; }
}

public sealed record AutomationConditionOutcomeResponse
{
    public required string ConditionId { get; init; }

    public required string Label { get; init; }

    public required bool Matched { get; init; }

    /// <summary>Always <c>server</c> from a real run. A client answer here would be a defect.</summary>
    public required string EvaluatedBy { get; init; }

    /// <summary>Why. Never the member's value when the condition is sensitive.</summary>
    public required string Explanation { get; init; }
}

public sealed record AutomationActionOutcomeResponse
{
    public required string ActionId { get; init; }

    public required string ActionKey { get; init; }

    public required string Label { get; init; }

    /// <summary><c>succeeded</c>, <c>failed</c>, <c>skipped</c>, <c>suppressed</c>, <c>pending</c>.</summary>
    public required string Status { get; init; }

    public string? SuppressionReason { get; init; }

    public string? FailureKind { get; init; }

    public string? Message { get; init; }

    public AutomationResultRefResponse? ResultRef { get; init; }

    public string? CompletedAt { get; init; }
}

public sealed record AutomationResultRefResponse
{
    public required string Kind { get; init; }

    public required string Id { get; init; }

    public string? DeepLink { get; init; }
}

public sealed record AutomationRunResponse
{
    public required string Id { get; init; }

    public required string AutomationId { get; init; }

    public required string AutomationName { get; init; }

    public required string Status { get; init; }

    public required string StartedAt { get; init; }

    public string? CompletedAt { get; init; }

    public long? DurationMs { get; init; }

    public required string TriggerSummary { get; init; }

    public required string TriggeredAt { get; init; }

    public required IReadOnlyList<AutomationConditionOutcomeResponse> ConditionOutcomes { get; init; }

    public required IReadOnlyList<AutomationActionOutcomeResponse> ActionOutcomes { get; init; }

    public string? FailureKind { get; init; }

    public string? FailureMessage { get; init; }

    public string? RecoveryHint { get; init; }

    /// <summary>Opaque, and never an internal identifier. For a support conversation.</summary>
    public string? CorrelationId { get; init; }

    public required bool IsSimulated { get; init; }

    public required bool WasDeduplicated { get; init; }
}

/// <summary>The server's answer to what this member may do. Every field a boolean it decided.</summary>
public sealed record AutomationAccessResponse
{
    public required bool CanViewAutomations { get; init; }

    public required bool CanCreatePersonal { get; init; }

    public required bool CanCreateForCommunity { get; init; }

    public required bool CanCreateForOrganization { get; init; }

    public required bool CanCreateForCreator { get; init; }

    public required bool CanCreateForBusiness { get; init; }

    public required bool CanCreatePlatformWide { get; init; }

    public required bool CanNotifyOtherMembers { get; init; }

    public required bool CanUseFinancialTriggers { get; init; }

    public required bool CanUseLocationTriggers { get; init; }

    public required bool CanViewOthersRuns { get; init; }

    public required bool CanManageOthersAutomations { get; init; }

    public int? MaxActiveAutomations { get; init; }

    public required int ActiveAutomations { get; init; }

    public required string Provenance { get; init; }

    public required IReadOnlyList<string> GrantedCapabilities { get; init; }
}

/// <summary>A count and prose. Never a member list.</summary>
public sealed record AutomationAudienceResponse
{
    public required string Kind { get; init; }

    /// <summary>Null when the platform cannot say, which is rendered differently from zero.</summary>
    public int? EstimatedRecipients { get; init; }

    public required string Provenance { get; init; }

    public required string Description { get; init; }

    /// <summary>Eligibility the platform applies on top, so the preview does not over-promise.</summary>
    public required IReadOnlyList<string> ServerSideFilters { get; init; }
}

public sealed record AutomationSimulationResponse
{
    public required AutomationRunResponse Run { get; init; }

    public required IReadOnlyList<AutomationWouldHaveDoneResponse> WouldHaveDone { get; init; }

    public AutomationAudienceResponse? Audience { get; init; }

    public required IReadOnlyList<AutomationDependencyResponse> Dependencies { get; init; }
}

public sealed record AutomationWouldHaveDoneResponse
{
    public required string ActionKey { get; init; }

    public required string Label { get; init; }

    public required string Reason { get; init; }
}

public sealed record AutomationDependencyResponse
{
    public required string Kind { get; init; }

    public required string Label { get; init; }

    /// <summary><c>met</c>, <c>missing</c> or <c>gap</c>. A gap is not the member's problem.</summary>
    public required string State { get; init; }

    public required string Detail { get; init; }

    public string? GapId { get; init; }

    public required bool BlocksActivation { get; init; }
}

public sealed record AutomationScheduleStateResponse
{
    public string? NextRunAt { get; init; }

    public required string NextRunLocal { get; init; }

    public required string TimeZone { get; init; }

    public required IReadOnlyList<string> Upcoming { get; init; }

    public required int MissedCount { get; init; }

    public required bool IsSchedulable { get; init; }

    public string? Reason { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
}

public sealed record SavedSearchResponse
{
    public required string Id { get; init; }

    public required string Subject { get; init; }

    public required string Name { get; init; }

    public required IReadOnlyDictionary<string, string?> Criteria { get; init; }

    public required string QuerySummary { get; init; }

    public required bool AlertsEnabled { get; init; }

    public required string AlertFrequency { get; init; }

    public required IReadOnlyList<string> WatchFor { get; init; }

    public required string CreatedAt { get; init; }

    public string? LastMatchedAt { get; init; }

    public int? NewMatchCount { get; init; }

    public string? AutomationId { get; init; }

    public required bool IsSimulated { get; init; }
}

public sealed record ReminderResponse
{
    public required string ReminderId { get; init; }

    public required string SubjectKind { get; init; }

    public required string SubjectId { get; init; }

    public required int OffsetMinutes { get; init; }

    public required string Anchor { get; init; }

    /// <summary><c>configured</c>, <c>scheduled</c>, <c>sent</c>, <c>failed</c>, <c>cancelled</c>.</summary>
    public required string State { get; init; }

    public string? ScheduledFor { get; init; }

    public required string TimeZoneId { get; init; }

    public required bool IsSimulated { get; init; }
}

/// <summary>What the catalogue offers, so the builder can present a truthful list.</summary>
public sealed record AutomationCatalogResponse
{
    public required IReadOnlyList<AutomationCatalogEntryResponse> Triggers { get; init; }

    public required IReadOnlyList<AutomationCatalogEntryResponse> Conditions { get; init; }

    public required IReadOnlyList<AutomationCatalogEntryResponse> Actions { get; init; }
}

public sealed record AutomationCatalogEntryResponse
{
    public required string Key { get; init; }

    public required string Domain { get; init; }

    /// <summary>Whether this deployment can actually do it. False is a first-class answer.</summary>
    public required bool IsAvailable { get; init; }

    /// <summary>What is missing, in the owning module's own vocabulary. Null when it is available.</summary>
    public string? UnavailableReason { get; init; }

    public string? RequiredCapability { get; init; }

    public required IReadOnlyList<string> AllowedOwners { get; init; }

    /// <summary>Set on conditions: whether the platform can answer it at all.</summary>
    public bool? RequiresServerEvaluation { get; init; }

    public bool? IsSensitive { get; init; }

    /// <summary>Set on actions: whether it reaches somebody other than the owner.</summary>
    public bool? ReachesOtherPeople { get; init; }
}

public sealed record AutomationTemplateResponse
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string Category { get; init; }

    public required string Domain { get; init; }

    public required AutomationTriggerResponse Trigger { get; init; }

    public required IReadOnlyList<AutomationConditionResponse> Conditions { get; init; }

    public required IReadOnlyList<AutomationActionResponse> Actions { get; init; }

    public required IReadOnlyList<string> AllowedOwners { get; init; }

    public required IReadOnlyList<string> RequiredCapabilities { get; init; }

    public required string Support { get; init; }

    public required IReadOnlyList<string> GapKeys { get; init; }
}

public sealed record AutomationDeletedResponse
{
    public required string Id { get; init; }

    public required bool Deleted { get; init; }

    /// <summary>From the server's count, never a guess. §40 requires the UI to say what happened.</summary>
    public required int CancelledRuns { get; init; }
}

public sealed record AutomationStatusChangedResponse
{
    /// <summary>
    /// Named <c>automation</c> on the wire because that is what the client reads.
    /// <para>
    /// <c>setAutomationStatus</c> returns <c>{ automation, cancelledRuns }</c>; a property named for
    /// this module's aggregate type would serialise as <c>automationDefinition</c> and the client
    /// would read undefined with nothing failing anywhere.
    /// </para>
    /// </summary>
    public required AutomationDefinitionResponse Automation { get; init; }

    public required int CancelledRuns { get; init; }
}

public sealed record SavedSearchDeletedResponse
{
    public required string Id { get; init; }

    public required bool Deleted { get; init; }
}

// ── B24 · Platform & Automation Telemetry ────────────────────────────────────────────────────────

/// <summary>
/// How one owner's automations have been behaving over a window.
/// <para>
/// <b>Counts and durations, for one owner, over a bounded window.</b> Nothing here is a trend, a
/// comparison period or a segmentation — those are B19's, are answered from the analytics event this
/// wave registers, and are deliberately not answerable from this route. §2's boundary, expressed as
/// a response shape rather than as a promise.
/// </para>
/// <para>
/// <b>Nothing here names anybody.</b> No member, no recipient, no automation name, no condition
/// value. An action key and a failure kind are catalogue vocabulary; a count is a count. A summary is
/// read more widely than a run detail — a community's moderators all see the same one — and is the
/// wrong surface on which to widen what a reader learns.
/// </para>
/// </summary>
public sealed record AutomationDiagnosticsResponse
{
    /// <summary>Inclusive start of the window, ISO-8601.</summary>
    public required string From { get; init; }

    /// <summary>Inclusive end of the window, ISO-8601.</summary>
    public required string To { get; init; }

    /// <summary>How many days the window covers, after clamping.</summary>
    public required int Days { get; init; }

    /// <summary>Runs that started in the window, whatever became of them.</summary>
    public required int Total { get; init; }

    /// <summary>
    /// How they ended, keyed by the client's own run-status strings.
    /// <para>
    /// <c>RUNNING</c> appears and is not an anomaly: a run awaiting a notification receipt is
    /// genuinely still going. Reporting it as anything else would claim a terminal state the platform
    /// has not reached.
    /// </para>
    /// </summary>
    public required IReadOnlyDictionary<string, int> ByStatus { get; init; }

    /// <summary>
    /// Why the failures failed, keyed by the client's camel-case failure kinds.
    /// <para>
    /// Empty when nothing failed, rather than a map of zeroes. A caller rendering "0 timeouts, 0
    /// rate-limited, 0 provider-unavailable" is noise dressed as data.
    /// </para>
    /// </summary>
    public required IReadOnlyDictionary<string, int> ByFailureKind { get; init; }

    /// <summary>
    /// Runs that finished successfully or with some actions suppressed, over runs that finished.
    /// <para>
    /// <b>Derived on the server so two clients cannot derive it differently</b>, and null when
    /// nothing has settled — because a success rate over zero runs is not 0% or 100%, it is a
    /// question with no answer, and both of the available lies are ones a member would act on.
    /// </para>
    /// </summary>
    public double? SuccessRate { get; init; }

    /// <summary>Mean duration over settled runs, milliseconds. Null when none settled.</summary>
    public double? AverageDurationMs { get; init; }

    /// <summary>The slowest settled run, milliseconds. Null when none settled.</summary>
    public long? SlowestDurationMs { get; init; }

    /// <summary>
    /// Runs that were a recognised redelivery rather than a fresh firing.
    /// <para>
    /// A non-zero figure is the outbox working as designed. A figure approaching the total is a
    /// consumer that is not acknowledging, which is worth seeing before it becomes a load problem.
    /// </para>
    /// </summary>
    public required int Deduplicated { get; init; }

    /// <summary>
    /// Scheduled moments given up on in the window.
    /// <para>
    /// Counted from the occurrence store rather than from the runs, because a moment that
    /// dead-lettered never produced a run — which is exactly why it belongs on this response.
    /// </para>
    /// </summary>
    public required int DeadLettered { get; init; }

    /// <summary>Which actions fail most, worst first. The ranked fix list, capped at five.</summary>
    public required IReadOnlyList<AutomationActionFailureResponse> TopFailingActions { get; init; }
}

/// <summary>How often one action key failed.</summary>
public sealed record AutomationActionFailureResponse
{
    /// <summary>A catalogue key. Bounded vocabulary, safe to render and safe to group by.</summary>
    public required string ActionKey { get; init; }

    public required int Failures { get; init; }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Trigger context — Phase 5.
//
// The client's register asked for this as **thirteen** routes, one per domain, each with its own
// response shape — `/triggers/events/{eventId}/context`, `/triggers/jobs/context`,
// `/triggers/marketplace/context` and so on. Six of the thirteen named a path parameter their own
// declared endpoint had no room for, which is how long that list had gone unread.
//
// It is one route, because the thing it describes is one type. Every consumer in this module already
// builds an `AutomationTriggerContext` — subject kind, identifier, title, moment, zone, and the
// values a condition may be answered from — and a preview that invented a per-domain shape would be
// describing the engine's input in a vocabulary the engine does not use.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>What a trigger would carry about one subject, before a member commits to a rule.</summary>
public sealed record AutomationTriggerContextResponse
{
    public required string TriggerKey { get; init; }

    /// <summary>What kind of thing this trigger is about, or null where it is about no one thing.</summary>
    public string? SubjectKind { get; init; }

    public Guid? SubjectId { get; init; }

    /// <summary>The subject's own title. Null when nothing could describe it.</summary>
    public string? Title { get; init; }

    /// <summary>The moment the trigger turns on, where the subject has one.</summary>
    public DateTimeOffset? Moment { get; init; }

    /// <summary>The subject's own zone. §42: the offset is computed where the subject is.</summary>
    public string? TimeZoneId { get; init; }

    /// <summary>
    /// Whether the platform could describe this subject at all.
    /// <para>
    /// <b>False is a real and useful answer, not an error.</b> A subject whose module has no source
    /// registered, one the member cannot see, and one that does not exist all land here — and the
    /// builder says "this cannot be previewed" rather than showing an empty rule that looks tested.
    /// </para>
    /// </summary>
    public required bool IsObservable { get; init; }

    /// <summary>What this would fire on, in the member's words.</summary>
    public required string Summary { get; init; }

    /// <summary>
    /// The condition keys this subject answers, and what it answers them with.
    /// <para>
    /// <b>Sensitive conditions are named and never quoted.</b> A run record does not print the value
    /// of a sensitive condition — <c>amountAbove</c>, <c>priceAbove</c>, <c>walletBalance</c> — and a
    /// preview that did would be a way to read through this route what the run record withholds. The
    /// key appears in <see cref="Answers"/> so a member can see the check will be made; the figure
    /// stays where it was.
    /// </para>
    /// </summary>
    public required IReadOnlyDictionary<string, string?> Values { get; init; }

    /// <summary>Every condition key this subject can answer, sensitive ones included.</summary>
    public required IReadOnlyList<string> Answers { get; init; }
}
