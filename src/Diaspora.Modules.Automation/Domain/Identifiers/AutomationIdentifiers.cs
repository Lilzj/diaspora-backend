using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Automation.Domain.Identifiers;

/// <summary>One configured rule. Durable, and outlives every run of it.</summary>
public sealed record AutomationId(Guid Value) : StronglyTypedId(Value)
{
    public static AutomationId New() => new(Guid.NewGuid());

    public static AutomationId From(Guid value) => new(value);
}

/// <summary>
/// One attempt.
/// <para>
/// A definition and a run are two things that fail separately, and the frontend's domain model
/// separates them for exactly that reason: an automation is not <c>COMPLETED</c>, its third run is.
/// </para>
/// </summary>
public sealed record AutomationRunId(Guid Value) : StronglyTypedId(Value)
{
    public static AutomationRunId New() => new(Guid.NewGuid());

    public static AutomationRunId From(Guid value) => new(value);
}

/// <summary>One condition on one automation. Stable so the builder can reorder and remove.</summary>
public sealed record AutomationConditionId(Guid Value) : StronglyTypedId(Value)
{
    public static AutomationConditionId New() => new(Guid.NewGuid());

    public static AutomationConditionId From(Guid value) => new(value);
}

/// <summary>One action on one automation.</summary>
public sealed record AutomationActionId(Guid Value) : StronglyTypedId(Value)
{
    public static AutomationActionId New() => new(Guid.NewGuid());

    public static AutomationActionId From(Guid value) => new(value);
}

/// <summary>
/// One due moment for one time-based automation.
/// <para>
/// The durable half of the scheduler. B17's shipped scheduler says of itself that "jobs do not
/// survive a restart" and that "anything that must happen goes through the outbox"; a scheduled
/// automation must happen, so its next moment is a row rather than a timer.
/// </para>
/// </summary>
public sealed record AutomationOccurrenceId(Guid Value) : StronglyTypedId(Value)
{
    public static AutomationOccurrenceId New() => new(Guid.NewGuid());

    public static AutomationOccurrenceId From(Guid value) => new(value);
}

/// <summary>
/// One dated subject the platform will look at again — Wave 12.
/// <para>
/// Distinct from an occurrence: an occurrence is one automation's next moment, and a horizon is one
/// <i>subject's</i> moment that any number of automations may care about. Keeping them apart is what
/// stops a platform-wide publication writing a row per watcher.
/// </para>
/// </summary>
public sealed record AutomationSubjectHorizonId(Guid Value) : StronglyTypedId(Value)
{
    public static AutomationSubjectHorizonId New() => new(Guid.NewGuid());

    public static AutomationSubjectHorizonId From(Guid value) => new(value);
}

/// <summary>One member's reminder on one subject.</summary>
public sealed record AutomationReminderId(Guid Value) : StronglyTypedId(Value)
{
    public static AutomationReminderId New() => new(Guid.NewGuid());

    public static AutomationReminderId From(Guid value) => new(value);
}

/// <summary>One saved search, shared by five subjects and one matcher.</summary>
public sealed record SavedSearchId(Guid Value) : StronglyTypedId(Value)
{
    public static SavedSearchId New() => new(Guid.NewGuid());

    public static SavedSearchId From(Guid value) => new(value);
}

/// <summary>One audit entry on one automation. Append-only.</summary>
public sealed record AutomationAuditId(Guid Value) : StronglyTypedId(Value)
{
    public static AutomationAuditId New() => new(Guid.NewGuid());

    public static AutomationAuditId From(Guid value) => new(value);
}
