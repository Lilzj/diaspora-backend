namespace Diaspora.Modules.Automation;

/// <summary>The error codes this module returns. Stable, and the client switches on them.</summary>
public static class AutomationErrorCodes
{
    public const string NotAuthenticated = "automation.not_authenticated";
    public const string NotFound = "automation.not_found";
    public const string Forbidden = "automation.forbidden";
    public const string ScopeForbidden = "automation.scope_forbidden";
    public const string CapabilityRequired = "automation.capability_required";
    public const string UnknownTrigger = "automation.unknown_trigger";
    public const string UnknownCondition = "automation.unknown_condition";
    public const string UnknownAction = "automation.unknown_action";
    public const string UnknownOperator = "automation.unknown_operator";
    public const string TriggerUnavailable = "automation.trigger_unavailable";
    public const string ActionUnavailable = "automation.action_unavailable";
    public const string OwnerNotAllowed = "automation.owner_not_allowed";
    public const string NoActions = "automation.no_actions";
    public const string TooManyActive = "automation.too_many_active";
    public const string InvalidSchedule = "automation.invalid_schedule";
    public const string UnknownTimeZone = "automation.unknown_time_zone";
    public const string ConcurrencyConflict = "automation.concurrency_conflict";
    public const string InvalidStatusTransition = "automation.invalid_status_transition";
    public const string NotManuallyRunnable = "automation.not_manually_runnable";
    public const string RateLimited = "automation.rate_limited";
    public const string PayloadTooLarge = "automation.payload_too_large";
    public const string ReminderSubjectUnknown = "automation.reminder_subject_unknown";
    public const string ReminderInPast = "automation.reminder_in_past";
    public const string SavedSearchNotFound = "automation.saved_search_not_found";
    public const string SavedSearchCriteriaInvalid = "automation.saved_search_criteria_invalid";
    public const string TemplateNotFound = "automation.template_not_found";
}
