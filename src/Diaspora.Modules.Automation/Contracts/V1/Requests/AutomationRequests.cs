namespace Diaspora.Modules.Automation.Contracts.V1.Requests;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// What a client may send.
//
// **There is no recipient field, no amount field and no channel field anywhere below**, and that is
// the enforcement rather than a validation rule. §49 requires that the recipient set is never trusted
// from the client — the automation carries a scope and the server derives who that reaches — and §48
// forbids an automation moving money. Neither is checked here because neither can be expressed.
//
// **Nothing carries a status the client asserts about execution.** A client may ask for DRAFT or
// ACTIVE on creation and may ask for a lifecycle transition; it cannot claim EXPIRED, cannot claim
// NEEDS_ATTENTION and cannot construct a run. §77's rule that the frontend never claims an automation
// ran, expressed as an absence in the request shapes.
// ════════════════════════════════════════════════════════════════════════════════════════════════

public sealed record AutomationScopeRequest
{
    /// <summary><c>user</c>, <c>creator</c>, <c>community</c>… Never <c>platform</c>; that is refused.</summary>
    public string? OwnerKind { get; init; }

    public Guid? OwnerId { get; init; }

    public IReadOnlyList<Guid>? CommunityIds { get; init; }

    public bool IsCrossCommunity { get; init; }

    public IReadOnlyList<string>? CountryCodes { get; init; }

    public IReadOnlyList<Guid>? NeighbourhoodIds { get; init; }
}

public sealed record AutomationTriggerRequest
{
    public string Key { get; init; } = string.Empty;

    /// <summary>Scalars only. A nested structure is refused, because that is where a language starts.</summary>
    public string? Parameters { get; init; }
}

public sealed record AutomationConditionRequest
{
    /// <summary>Present when editing, so a reorder does not orphan the outcomes that referenced it.</summary>
    public Guid? Id { get; init; }

    public string Key { get; init; } = string.Empty;

    public string Operator { get; init; } = string.Empty;

    public string? Value { get; init; }

    public string? SecondaryValue { get; init; }
}

public sealed record AutomationActionRequest
{
    public Guid? Id { get; init; }

    public string Key { get; init; } = string.Empty;

    public string? Parameters { get; init; }
}

public sealed record AutomationScheduleRequest
{
    public string? Frequency { get; init; }

    public string? TimezoneMode { get; init; }

    /// <summary>IANA, required. Never an offset — an offset is wrong for half the year.</summary>
    public string? TimeZoneId { get; init; }

    public int? Hour { get; init; }

    public int? Minute { get; init; }

    public IReadOnlyList<int>? DaysOfWeek { get; init; }

    public int? DayOfMonth { get; init; }

    public DateTimeOffset? StartsAt { get; init; }

    public DateTimeOffset? EndsAt { get; init; }

    public int? MaxExecutions { get; init; }

    public int? EventOffsetMinutes { get; init; }

    public string? EventOffsetAnchor { get; init; }

    /// <summary>Held as written and never parsed. A custom schedule does not produce an occurrence.</summary>
    public string? CustomDescription { get; init; }
}

public sealed record CreateAutomationRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public AutomationScopeRequest? Scope { get; init; }

    public string? Source { get; init; }

    public string? TemplateId { get; init; }

    public AutomationTriggerRequest? Trigger { get; init; }

    public IReadOnlyList<AutomationConditionRequest>? Conditions { get; init; }

    public IReadOnlyList<AutomationActionRequest>? Actions { get; init; }

    public AutomationScheduleRequest? Schedule { get; init; }

    /// <summary><c>DRAFT</c> or <c>ACTIVE</c>. Anything else is refused.</summary>
    public string? Status { get; init; }

    /// <summary>
    /// Mandatory.
    /// <para>
    /// A retried creation without one gives the member two identical job alerts and every notification
    /// twice, with no obvious cause — a duplicate automation fails silently and compounds, which makes
    /// it worse than a visible error.
    /// </para>
    /// </summary>
    public string? IdempotencyKey { get; init; }
}

public sealed record UpdateAutomationRequest
{
    public string? Name { get; init; }

    public string? Description { get; init; }

    public AutomationTriggerRequest? Trigger { get; init; }

    public IReadOnlyList<AutomationConditionRequest>? Conditions { get; init; }

    public IReadOnlyList<AutomationActionRequest>? Actions { get; init; }

    public AutomationScheduleRequest? Schedule { get; init; }

    public AutomationScopeRequest? Scope { get; init; }

    /// <summary>
    /// The concurrency precondition.
    /// <para>
    /// The server answers 409 with the current definition when the automation changed on another
    /// device since this was read, so an edit made on a phone cannot silently overwrite one made on a
    /// tablet.
    /// </para>
    /// </summary>
    public DateTimeOffset? ExpectedUpdatedAt { get; init; }
}

public sealed record ChangeAutomationStatusRequest
{
    /// <summary><c>ACTIVE</c>, <c>PAUSED</c>, <c>DISABLED</c> or <c>DRAFT</c>.</summary>
    public string Status { get; init; } = string.Empty;
}

public sealed record RunAutomationRequest
{
    /// <summary><c>manual</c> — the only reason a client may supply.</summary>
    public string? Reason { get; init; }

    public string? IdempotencyKey { get; init; }
}

public sealed record SimulateAutomationRequest
{
    public string? Name { get; init; }

    public AutomationScopeRequest? Scope { get; init; }

    public AutomationTriggerRequest? Trigger { get; init; }

    public IReadOnlyList<AutomationConditionRequest>? Conditions { get; init; }

    public IReadOnlyList<AutomationActionRequest>? Actions { get; init; }

    public AutomationScheduleRequest? Schedule { get; init; }

    /// <summary>An optional real subject to evaluate against, so a dry run borrows real state.</summary>
    public Guid? SampleSubjectId { get; init; }
}

public sealed record AudienceRequest
{
    public AutomationScopeRequest? Scope { get; init; }

    public IReadOnlyList<string>? ActionKeys { get; init; }
}

public sealed record CreateSavedSearchRequest
{
    public string Subject { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>The query as the module's own list screen would send it, serialized.</summary>
    public string? Criteria { get; init; }

    public string? QuerySummary { get; init; }

    public bool AlertsEnabled { get; init; } = true;

    public string? AlertFrequency { get; init; }

    public IReadOnlyList<string>? WatchFor { get; init; }
}

public sealed record UpdateSavedSearchRequest
{
    public string? Name { get; init; }

    public bool? AlertsEnabled { get; init; }

    public string? AlertFrequency { get; init; }

    public IReadOnlyList<string>? WatchFor { get; init; }
}

public sealed record CreateReminderRequest
{
    /// <summary><c>event</c>, <c>occurrence</c> or <c>series</c>.</summary>
    public string SubjectKind { get; init; } = string.Empty;

    public Guid SubjectId { get; init; }

    public string? SubjectTitle { get; init; }

    /// <summary>The subject's own moment. The offset counts back from this, in the subject's zone.</summary>
    public DateTimeOffset SubjectMoment { get; init; }

    public int OffsetMinutes { get; init; }

    /// <summary><c>start</c> or <c>end</c>.</summary>
    public string? Anchor { get; init; }

    /// <summary>IANA, the subject's own. Never an offset and never the device's.</summary>
    public string? TimeZoneId { get; init; }

    public string? IdempotencyKey { get; init; }
}
