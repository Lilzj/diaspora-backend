namespace Diaspora.Modules.Automation.Domain.Enums;

/// <summary>
/// The state of a configured automation.
/// <para>
/// Six values describing a definition that persists across many runs. The five that describe one
/// attempt are <see cref="AutomationRunStatus"/> — one vocabulary, two unions, no member appearing
/// in both meaning different things.
/// </para>
/// <para>
/// <c>Paused</c> and <c>Disabled</c> are genuinely different and members use both. <c>Paused</c> is
/// "stop for now, I will turn it back on" and keeps the schedule. <c>Disabled</c> is "stop, and stop
/// counting", and it is what the platform sets when something an automation depends on is revoked
/// and cannot be silently resumed.
/// </para>
/// </summary>
public enum AutomationStatus
{
    Draft = 0,
    Active = 1,
    Paused = 2,
    Disabled = 3,
    Expired = 4,
    NeedsAttention = 5
}

/// <summary>
/// The state of one attempt.
/// <para>
/// <c>PartiallyCompleted</c> is not a nicety: an automation with three actions where the push landed
/// and the calendar write failed is neither a success nor a failure. <c>Skipped</c> is the most
/// common non-success in a healthy system — the trigger fired and a condition said no — and must
/// never be rendered as a failure.
/// </para>
/// </summary>
public enum AutomationRunStatus
{
    Running = 0,
    Completed = 1,
    PartiallyCompleted = 2,
    Failed = 3,
    Skipped = 4,
    Cancelled = 5,
    TimedOut = 6
}

/// <summary>How a run was set off.</summary>
public enum AutomationRunReason
{
    /// <summary>A published contract this module consumed.</summary>
    DomainEvent = 0,

    /// <summary>A due occurrence the sweeper claimed.</summary>
    Schedule = 1,

    /// <summary>The member pressed the button. The only reason a client may supply.</summary>
    Manual = 2,

    /// <summary>A dry run. Dispatches nothing, and the dispatcher enforces it.</summary>
    Simulation = 3
}

/// <summary>What one action produced.</summary>
public enum AutomationActionOutcomeStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,

    /// <summary>A condition or the executor meant it was not attempted.</summary>
    Skipped = 3,

    /// <summary>
    /// The correct, healthy outcome of a member's own preferences or of idempotency.
    /// <para>
    /// Its own status because rendering either as a failure would teach members to distrust the
    /// feature.
    /// </para>
    /// </summary>
    Suppressed = 4
}

/// <summary>Why a run or an action failed, in the frontend's own vocabulary.</summary>
public enum AutomationFailureKind
{
    None = 0,
    TriggerUnavailable = 1,
    ConditionEvaluationFailed = 2,
    ActionFailed = 3,
    ProviderUnavailable = 4,
    BackendUnavailable = 5,
    PermissionDenied = 6,
    RateLimited = 7,
    IntegrationExpired = 8,
    InvalidConfiguration = 9,
    Timeout = 10,
    DuplicateSuppressed = 11,
    BackendGap = 12
}

/// <summary>How a trigger reaches the engine.</summary>
public enum AutomationTriggerDelivery
{
    /// <summary>A published integration contract.</summary>
    DomainEvent = 0,

    /// <summary>A due occurrence.</summary>
    Schedule = 1,

    /// <summary>The device does it. The server never claims to.</summary>
    DeviceLocal = 2,

    /// <summary>The member runs it.</summary>
    Manual = 3
}

/// <summary>Who must carry an action out.</summary>
public enum AutomationActionExecutor
{
    Server = 0,
    Client = 1
}

/// <summary>How dangerous an action is to repeat. Drives the idempotency contract.</summary>
public enum AutomationRepeatRisk
{
    /// <summary>Repeating changes nothing.</summary>
    Safe = 0,

    /// <summary>Repeating is prevented by a key; a second attempt returns the first result.</summary>
    Deduplicated = 1,

    /// <summary>Repeating produces a second visible thing. Never retried automatically.</summary>
    Visible = 2
}

/// <summary>How often a scheduled automation runs.</summary>
public enum AutomationFrequency
{
    Once = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3,

    /// <summary>
    /// Held so the UI can show what was asked for. Never parsed and never executed — a member-authored
    /// recurrence expression the platform interpreted would be a rules language exposed to members.
    /// </summary>
    Custom = 4,

    /// <summary>Relative to a subject's own moment, in the subject's own zone.</summary>
    EventRelative = 5
}

/// <summary>Which clock a schedule is expressed in.</summary>
public enum AutomationTimezoneMode
{
    Device = 0,
    UserSelected = 1,

    /// <summary>The subject's own zone. An event in Lagos does not move because the member flew.</summary>
    Event = 2,
    Community = 3,
    Country = 4
}

/// <summary>Where an automation came from.</summary>
public enum AutomationSource
{
    Template = 0,
    Builder = 1,
    Contextual = 2,
    Platform = 3
}

/// <summary>Where a due occurrence is.</summary>
public enum AutomationOccurrenceStatus
{
    Pending = 0,
    Claimed = 1,
    Completed = 2,

    /// <summary>Cancelled by a pause, a delete or a subject that moved.</summary>
    Cancelled = 3,

    /// <summary>Too late to be worth firing. Recorded rather than dropped silently.</summary>
    Skipped = 4,

    /// <summary>Failed enough times to be given up on. Kept for inspection.</summary>
    DeadLettered = 5
}

/// <summary>The five states §12 requires to stay distinct.</summary>
public enum AutomationReminderState
{
    /// <summary>What the member did.</summary>
    Configured = 0,

    /// <summary>What the platform accepted.</summary>
    Scheduled = 1,
    Sent = 2,
    Failed = 3,
    Cancelled = 4
}

/// <summary>The surfaces a saved search can watch. Every one has a real listing route.</summary>
public enum SavedSearchSubject
{
    Jobs = 0,
    Marketplace = 1,
    Housing = 2,
    Providers = 3,
    Events = 4
}

/// <summary>How often a saved search's alerts are delivered.</summary>
public enum SavedSearchAlertFrequency
{
    Instant = 0,
    Daily = 1,
    Weekly = 2,
    Off = 3
}

/// <summary>What an operator did to an automation. Append-only.</summary>
public enum AutomationAuditAction
{
    Created = 0,
    Updated = 1,
    Activated = 2,
    Paused = 3,
    Disabled = 4,
    Deleted = 5,
    Expired = 6,
    FlaggedForAttention = 7
}
