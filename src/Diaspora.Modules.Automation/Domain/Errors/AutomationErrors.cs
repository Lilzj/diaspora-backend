using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Automation.Domain.Errors;

/// <summary>
/// The refusals this module returns.
/// <para>
/// One place, so a code and the sentence beside it cannot drift.
/// </para>
/// <para>
/// <b>Every refusal is the same for a thing that does not exist and a thing the caller may not
/// see.</b> <see cref="NotFound"/> is returned for both, because a 403 on somebody else's automation
/// id confirms that the id exists — which is how an enumeration tool is built out of an
/// authorization check. The one deliberate exception is a caller naming an <i>owner scope</i> they
/// have no role on: the register requires a 403 rather than an empty list there, because the caller
/// already knows the community exists and an empty list would teach the client the wrong thing.
/// </para>
/// </summary>
public static class AutomationErrors
{
    public static Error NotAuthenticated() => Error.Unauthorized(
        AutomationErrorCodes.NotAuthenticated,
        "You need to be signed in to manage automations.");

    public static Error NotFound() => Error.NotFound(
        AutomationErrorCodes.NotFound,
        "That automation does not exist, or is not yours.");

    public static Error ScopeForbidden(string scope) => Error.Forbidden(
        AutomationErrorCodes.ScopeForbidden,
        $"You do not have a role that lets you create automations for this {scope}.");

    public static Error CapabilityRequired(string capability) => Error.Forbidden(
        AutomationErrorCodes.CapabilityRequired,
        $"This automation needs the '{capability}' capability, which you do not currently hold.");

    public static Error UnknownTrigger(string key) => Error.Validation(
        AutomationErrorCodes.UnknownTrigger,
        $"'{key}' is not a trigger this platform knows about.");

    public static Error UnknownCondition(string key) => Error.Validation(
        AutomationErrorCodes.UnknownCondition,
        $"'{key}' is not a condition this platform knows about.");

    public static Error UnknownAction(string key) => Error.Validation(
        AutomationErrorCodes.UnknownAction,
        $"'{key}' is not an action this platform knows about.");

    public static Error UnknownOperator(string conditionKey, string @operator) => Error.Validation(
        AutomationErrorCodes.UnknownOperator,
        $"'{@operator}' is not something '{conditionKey}' can be compared with.");

    public static Error TriggerUnavailable(string key, string? reason) => Error.Validation(
        AutomationErrorCodes.TriggerUnavailable,
        reason is null
            ? $"The platform cannot observe '{key}' yet, so an automation cannot be switched on for it."
            : $"The platform cannot observe '{key}' yet. {reason}");

    public static Error ActionUnavailable(string key, string? reason) => Error.Validation(
        AutomationErrorCodes.ActionUnavailable,
        reason is null
            ? $"The platform cannot perform '{key}' yet."
            : $"The platform cannot perform '{key}' yet. {reason}");

    public static Error OwnerNotAllowed(string key, string ownerKind) => Error.Validation(
        AutomationErrorCodes.OwnerNotAllowed,
        $"'{key}' cannot be used by a {ownerKind.ToLowerInvariant()} automation.");

    public static Error NoActions() => Error.Validation(
        AutomationErrorCodes.NoActions,
        "An automation needs at least one thing to do.");

    public static Error TooManyActive(int ceiling) => Error.Validation(
        AutomationErrorCodes.TooManyActive,
        $"You already have {ceiling} active automations, which is the most this platform allows. "
        + "Pause one to switch another on.");

    public static Error InvalidSchedule(string detail) => Error.Validation(
        AutomationErrorCodes.InvalidSchedule, detail);

    public static Error UnknownTimeZone(string id) => Error.Validation(
        AutomationErrorCodes.UnknownTimeZone,
        $"'{id}' is not a time zone this platform recognises. Use an IANA identifier such as "
        + "Africa/Lagos.");

    public static Error ConcurrencyConflict() => Error.Conflict(
        AutomationErrorCodes.ConcurrencyConflict,
        "This automation changed somewhere else since you opened it. Reload it and try again.");

    public static Error InvalidStatusTransition(string from, string to) => Error.Conflict(
        AutomationErrorCodes.InvalidStatusTransition,
        $"An automation that is {from.ToLowerInvariant()} cannot be moved to "
        + $"{to.ToLowerInvariant()}.");

    public static Error NotManuallyRunnable() => Error.Validation(
        AutomationErrorCodes.NotManuallyRunnable,
        "This automation runs when its trigger happens. It cannot be run by hand.");

    public static Error RateLimited(string detail) => Error.RateLimited(
        AutomationErrorCodes.RateLimited, detail);

    public static Error PayloadTooLarge(string what) => Error.Validation(
        AutomationErrorCodes.PayloadTooLarge,
        $"The {what} on this automation is larger than the platform accepts.");

    public static Error ReminderSubjectUnknown() => Error.Validation(
        AutomationErrorCodes.ReminderSubjectUnknown,
        "A reminder needs a subject the platform can see and a moment to count back from.");

    public static Error ReminderInPast() => Error.Validation(
        AutomationErrorCodes.ReminderInPast,
        "That reminder would already have passed.");

    public static Error SavedSearchNotFound() => Error.NotFound(
        AutomationErrorCodes.SavedSearchNotFound,
        "That saved search does not exist, or is not yours.");

    public static Error SavedSearchCriteriaInvalid(string detail) => Error.Validation(
        AutomationErrorCodes.SavedSearchCriteriaInvalid, detail);

    public static Error TemplateNotFound(string id) => Error.NotFound(
        AutomationErrorCodes.TemplateNotFound,
        $"'{id}' is not a template this platform publishes.");
}
