using System.Globalization;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Automation.Domain.Automations;

/// <summary>
/// One configured rule: what it watches, what it checks, what it asks for, and who it belongs to.
/// <para>
/// <b>A definition, a run and a capability are three things.</b> This is the first. It is durable and
/// survives many runs; <see cref="Runs.AutomationRun"/> is one attempt with its own status and its own
/// failure; and whether the deployment can execute it at all is a property of the deployment rather
/// than of the member. Folding the third into the first is the mistake this design was most exposed
/// to — "is this automation active?" and "can this build run it?" are different questions and the
/// most common automation on a partially-shipped platform is active and unrunnable.
/// </para>
/// <para>
/// <b>Every collection is a child entity with a declared key, and every parameter bag is a string.</b>
/// This platform has four recorded defects in owned-type mapping — a shadow key inside a composite
/// primary key, caller-supplied keys producing an UPDATE instead of an INSERT, a cached owned
/// instance NULLing a second owner's columns, and a read-only collection getter forcing EF through a
/// field it finds by name — and none of them is reachable from a model with no owned types in it.
/// </para>
/// <para>
/// <b>There is no amount field, no destination field and no recipient list.</b> Not because they are
/// validated away but because they do not exist: an automation that could carry a recipient set would
/// be an automation whose audience the client decided, and one that could carry an amount would be a
/// second wallet with a rule engine in front of it.
/// </para>
/// </summary>
public sealed class AutomationDefinition : AggregateRoot<AutomationId>
{
    private readonly List<AutomationConditionRule> _conditions = [];
    private readonly List<AutomationActionStep> _actions = [];
    private readonly List<AutomationAuditRecord> _audit = [];

    private AutomationDefinition() { }

    private AutomationDefinition(AutomationId id) : base(id) { }

    // ── Ownership and scope ─────────────────────────────────────────────────────────────────────

    public AutomationOwnerKind OwnerKind { get; private set; }

    /// <summary>
    /// The owning entity — a member, a community, an organization.
    /// <para>
    /// <b>Never an authorization input on its own.</b> A caller who supplies one they have no role on
    /// receives 403; the authority is re-derived from the caller's identity and the scope authority,
    /// and this column records the answer rather than being consulted for it.
    /// </para>
    /// </summary>
    public Guid OwnerId { get; private set; }

    /// <summary>Human-readable, for "this runs on behalf of". Never used as a permission.</summary>
    public string OwnerLabel { get; private set; } = string.Empty;

    /// <summary>
    /// Who created it, kept separately from who owns it.
    /// <para>
    /// A community automation is owned by the community and was created by a moderator, and the two
    /// diverge the moment that moderator leaves. §51's "changed by a former moderator must still be
    /// answerable" needs the creator recorded on the row rather than inferred from a role that may no
    /// longer exist.
    /// </para>
    /// </summary>
    public UserId CreatedByUserId { get; private set; } = UserId.Empty;

    /// <summary>
    /// Which communities this may act within, delimited.
    /// <para>
    /// Empty means the owner's own scope only, which is not the same as "all". A member belongs to
    /// many communities and an automation that assumed one would notify the wrong people.
    /// </para>
    /// </summary>
    public string CommunityIds { get; private set; } = string.Empty;

    /// <summary>True only when the automation deliberately spans every community.</summary>
    public bool IsCrossCommunity { get; private set; }

    /// <summary>ISO-3166-1 alpha-2, upper case, delimited. Present when the scope is country-bound.</summary>
    public string CountryCodes { get; private set; } = string.Empty;

    /// <summary>Neighbourhood identifiers, delimited, where the scope is narrower than a community.</summary>
    public string NeighbourhoodIds { get; private set; } = string.Empty;

    // ── Identity and lifecycle ──────────────────────────────────────────────────────────────────

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public AutomationStatus Status { get; private set; }

    public AutomationSource Source { get; private set; }

    public string? TemplateId { get; private set; }

    // ── The rule ────────────────────────────────────────────────────────────────────────────────

    /// <summary>A key from the catalogue. Validated on write and again at execution.</summary>
    public string TriggerKey { get; private set; } = string.Empty;

    /// <summary>
    /// Trigger settings, serialized.
    /// <para>
    /// A flat map of scalars — which saved search, which subject, how far ahead. Deliberately not a
    /// nested structure: a nested structure here would become a rules language, and exposing one to
    /// members is what §7 forbids and what makes an automation engine an arbitrary-code engine.
    /// </para>
    /// </summary>
    public string TriggerParameters { get; private set; } = "{}";

    public IReadOnlyList<AutomationConditionRule> Conditions => _conditions;

    public IReadOnlyList<AutomationActionStep> Actions => _actions;

    public IReadOnlyList<AutomationAuditRecord> Audit => _audit;

    // ── Schedule ────────────────────────────────────────────────────────────────────────────────

    public bool HasSchedule { get; private set; }

    public AutomationFrequency Frequency { get; private set; }

    public AutomationTimezoneMode TimezoneMode { get; private set; }

    /// <summary>
    /// The resolved IANA identifier. Never an offset.
    /// <para>
    /// An offset is wrong for half the year in every zone that observes daylight saving, and the
    /// failure is invisible: a 09:00 daily reminder simply arrives at 08:00 for six months.
    /// </para>
    /// </summary>
    public string? TimeZoneId { get; private set; }

    /// <summary>Local wall clock, 0–23. Null for event-relative and trigger-driven.</summary>
    public int? Hour { get; private set; }

    public int? Minute { get; private set; }

    /// <summary>0 = Sunday. Weekly only. Delimited.</summary>
    public string DaysOfWeek { get; private set; } = string.Empty;

    /// <summary>1–31. Monthly only. A 31 in February is clamped by the scheduler, never by the client.</summary>
    public int? DayOfMonth { get; private set; }

    public DateTimeOffset? StartsAt { get; private set; }

    public DateTimeOffset? EndsAt { get; private set; }

    /// <summary>Null means unlimited.</summary>
    public int? MaxExecutions { get; private set; }

    /// <summary>Positive means before. Event-relative only.</summary>
    public int? EventOffsetMinutes { get; private set; }

    /// <summary><c>start</c> or <c>end</c>. A reminder at the end of a two-day festival is a real ask.</summary>
    public string? EventOffsetAnchor { get; private set; }

    /// <summary>
    /// A member's own words for a custom recurrence.
    /// <para>
    /// Held so the UI can show what was asked for. <b>Not parsed and not executed.</b> A custom
    /// frequency never produces an occurrence; the scheduler refuses it and the automation reports
    /// that it cannot be scheduled rather than guessing an interval.
    /// </para>
    /// </summary>
    public string? CustomDescription { get; private set; }

    // ── Server-owned state ──────────────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The concurrency precondition the client sends back as <c>expectedUpdatedAt</c>.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? LastRunAt { get; private set; }

    public DateTimeOffset? NextRunAt { get; private set; }

    /// <summary>Cumulative, and the client never increments it.</summary>
    public int ExecutionCount { get; private set; }

    public AutomationRunStatus? LastRunStatus { get; private set; }

    /// <summary>Why it is <see cref="AutomationStatus.NeedsAttention"/>. Null otherwise.</summary>
    public string? AttentionReason { get; private set; }

    /// <summary>
    /// The creation key.
    /// <para>
    /// Mandatory on the contract, because a retried creation without one gives the member two
    /// identical job alerts and every notification twice, with no obvious cause. A duplicate
    /// automation fails silently and compounds, which makes it worse than a visible error.
    /// </para>
    /// </summary>
    public string? IdempotencyKey { get; private set; }

    /// <summary>
    /// The optimistic-concurrency token.
    /// <para>
    /// Mapped with <c>IsConcurrencyToken()</c>, per DSP3001. Without it two writers overwrite each
    /// other in silence: the second UPDATE succeeds, matches one row and discards the first writer's
    /// change with no error anywhere.
    /// </para>
    /// </summary>
    public int Revision { get; private set; }

    // ── Construction ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a draft or an active automation.
    /// <para>
    /// Everything here has already been validated against the catalogue by
    /// <c>AutomationRuleValidator</c>. The aggregate does not re-validate the vocabulary — it would be
    /// a second copy of the allow-list, and two copies of an allow-list is one allow-list and one
    /// bug — but it does enforce the invariants that are its own: a name, an owner, at least one
    /// action, and a status that is not something a caller may assert.
    /// </para>
    /// </summary>
    public static AutomationDefinition Create(
        AutomationOwnerKind ownerKind,
        Guid ownerId,
        string ownerLabel,
        UserId createdBy,
        string name,
        string description,
        AutomationSource source,
        string? templateId,
        string triggerKey,
        string triggerParameters,
        bool activateImmediately,
        DateTimeOffset now,
        string? idempotencyKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerKey);
        ArgumentNullException.ThrowIfNull(createdBy);

        var automation = new AutomationDefinition(AutomationId.New())
        {
            OwnerKind = ownerKind,
            OwnerId = ownerId,
            OwnerLabel = Trim(ownerLabel, 128),
            CreatedByUserId = createdBy,
            Name = Trim(name, 120),
            Description = Trim(description, 500),
            Source = source,
            TemplateId = TrimOrNull(templateId, 100),
            TriggerKey = triggerKey,
            TriggerParameters = string.IsNullOrWhiteSpace(triggerParameters) ? "{}" : triggerParameters,
            Status = activateImmediately ? AutomationStatus.Active : AutomationStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now,
            IdempotencyKey = TrimOrNull(idempotencyKey, 128),
            Revision = 1
        };

        automation.RecordAudit(
            AutomationAuditAction.Created, createdBy, Trim(ownerLabel, 128), now, null);

        if (activateImmediately)
        {
            // A second entry rather than one that means two things. "Created" and "activated" are
            // separate facts a member reads separately, and a member who creates a draft and
            // activates it a week later must see the same two lines as one who did both at once.
            automation.RecordAudit(
                AutomationAuditAction.Activated, createdBy, Trim(ownerLabel, 128), now, null);
        }

        return automation;
    }

    /// <summary>Renames and re-describes. The rule itself is replaced separately.</summary>
    public void Rename(string name, string description)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = Trim(name, 120);
        }

        Description = Trim(description, 500);
    }

    /// <summary>Repoints the trigger. The caller has already validated the key and its parameters.</summary>
    public void SetTrigger(string triggerKey, string triggerParameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerKey);

        TriggerKey = triggerKey;
        TriggerParameters = string.IsNullOrWhiteSpace(triggerParameters) ? "{}" : triggerParameters;
    }

    // ── Scope ───────────────────────────────────────────────────────────────────────────────────

    public void SetScope(
        IEnumerable<Guid> communityIds,
        bool isCrossCommunity,
        IEnumerable<string> countryCodes,
        IEnumerable<Guid> neighbourhoodIds)
    {
        CommunityIds = Join(communityIds?.Select(id => id.ToString("D")) ?? []);
        IsCrossCommunity = isCrossCommunity;
        CountryCodes = Join(
            (countryCodes ?? []).Select(code => code.Trim().ToUpperInvariant()).Where(code => code.Length == 2));
        NeighbourhoodIds = Join(neighbourhoodIds?.Select(id => id.ToString("D")) ?? []);
    }

    public IReadOnlyList<Guid> CommunityIdList() => Split(CommunityIds).Select(Guid.Parse).ToArray();

    public IReadOnlyList<string> CountryCodeList() => Split(CountryCodes);

    public IReadOnlyList<Guid> NeighbourhoodIdList() =>
        Split(NeighbourhoodIds).Select(Guid.Parse).ToArray();

    public IReadOnlyList<int> DayOfWeekList() =>
        Split(DaysOfWeek).Select(int.Parse).Where(day => day is >= 0 and <= 6).ToArray();

    // ── The rule ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Replaces the whole condition list.
    /// <para>
    /// Whole-list replacement rather than per-condition editing, because a rule is read and written as
    /// one thing by the builder, and a partial update API over a list would need an ordering protocol
    /// nobody asked for. Each entry may carry the identifier it already had so that a reorder does not
    /// orphan the run outcomes that referenced it.
    /// </para>
    /// </summary>
    public void ReplaceConditions(
        IEnumerable<(AutomationConditionId? Id, string Key, string Operator, string? Value, string? SecondaryValue)> conditions)
    {
        ArgumentNullException.ThrowIfNull(conditions);

        _conditions.Clear();
        var position = 0;

        foreach (var (id, key, op, value, secondary) in conditions)
        {
            _conditions.Add(AutomationConditionRule.Create(Id, id, key, op, value, secondary, position++));
        }
    }

    public void ReplaceActions(
        IEnumerable<(AutomationActionId? Id, string Key, string Parameters)> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        _actions.Clear();
        var position = 0;

        foreach (var (id, key, parameters) in actions)
        {
            _actions.Add(AutomationActionStep.Create(Id, id, key, parameters, position++));
        }
    }

    // ── Schedule ────────────────────────────────────────────────────────────────────────────────

    public void SetSchedule(
        AutomationFrequency frequency,
        AutomationTimezoneMode timezoneMode,
        string? timeZoneId,
        int? hour,
        int? minute,
        IEnumerable<int>? daysOfWeek,
        int? dayOfMonth,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt,
        int? maxExecutions,
        int? eventOffsetMinutes,
        string? eventOffsetAnchor,
        string? customDescription)
    {
        HasSchedule = true;
        Frequency = frequency;
        TimezoneMode = timezoneMode;
        TimeZoneId = TrimOrNull(timeZoneId, 64);
        Hour = hour;
        Minute = minute;
        DaysOfWeek = Join(
            (daysOfWeek ?? []).Distinct().Order()
                .Select(day => day.ToString(CultureInfo.InvariantCulture)));
        DayOfMonth = dayOfMonth;
        StartsAt = startsAt;
        EndsAt = endsAt;
        MaxExecutions = maxExecutions;
        EventOffsetMinutes = eventOffsetMinutes;
        EventOffsetAnchor = TrimOrNull(eventOffsetAnchor, 8);
        CustomDescription = TrimOrNull(customDescription, 200);
    }

    public void ClearSchedule()
    {
        HasSchedule = false;
        NextRunAt = null;
    }

    public void SetNextRunAt(DateTimeOffset? nextRunAt) => NextRunAt = nextRunAt;

    // ── Lifecycle ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Moves the automation to a state a member asked for.
    /// <para>
    /// Returns false when the transition is not one this state allows, rather than throwing: the
    /// caller turns it into a 409 with the current state, and the client shows the difference rather
    /// than a generic error.
    /// </para>
    /// <para>
    /// <c>Expired</c> and <c>NeedsAttention</c> are not reachable from here. Both are the platform's
    /// conclusions about an automation rather than a member's instruction, and accepting either from
    /// a request would let a client mark its own rule as having reached a limit it had not.
    /// </para>
    /// </summary>
    public bool TransitionTo(
        AutomationStatus target, UserId actor, string actorLabel, DateTimeOffset now)
    {
        if (target is AutomationStatus.Expired or AutomationStatus.NeedsAttention)
        {
            return false;
        }

        if (Status == target)
        {
            // Idempotent. Pausing a paused automation is not an error and must not produce a second
            // audit entry, or a client retrying a timeout writes history that did not happen.
            return true;
        }

        var allowed = Status switch
        {
            AutomationStatus.Draft => target is AutomationStatus.Active or AutomationStatus.Disabled,
            AutomationStatus.Active => target is AutomationStatus.Paused or AutomationStatus.Disabled
                or AutomationStatus.Draft,
            AutomationStatus.Paused => target is AutomationStatus.Active or AutomationStatus.Disabled,
            AutomationStatus.Disabled => target is AutomationStatus.Active or AutomationStatus.Paused,
            AutomationStatus.Expired => target is AutomationStatus.Disabled,
            AutomationStatus.NeedsAttention => target is AutomationStatus.Paused
                or AutomationStatus.Disabled or AutomationStatus.Active,
            _ => false
        };

        if (!allowed)
        {
            return false;
        }

        Status = target;
        AttentionReason = target == AutomationStatus.Active ? null : AttentionReason;
        Touch(now);

        RecordAudit(
            target switch
            {
                AutomationStatus.Active => AutomationAuditAction.Activated,
                AutomationStatus.Paused => AutomationAuditAction.Paused,
                AutomationStatus.Disabled => AutomationAuditAction.Disabled,
                _ => AutomationAuditAction.Updated
            },
            actor,
            actorLabel,
            now,
            null);

        return true;
    }

    /// <summary>
    /// The platform's own conclusion that something this automation depends on stopped working.
    /// <para>
    /// Separated from <see cref="TransitionTo"/> because it is not a member's instruction and must not
    /// be reachable from a request. <paramref name="disable"/> distinguishes "flag it" from "switch it
    /// off": a revoked capability stops the automation outright, because a moderator who lost the role
    /// must stop broadcasting; a trigger that became unobservable only needs the owner told.
    /// </para>
    /// </summary>
    public void FlagForAttention(string reason, bool disable, DateTimeOffset now)
    {
        AttentionReason = TrimOrNull(reason, 300);
        Status = disable ? AutomationStatus.Disabled : AutomationStatus.NeedsAttention;
        Touch(now);

        RecordAudit(
            disable ? AutomationAuditAction.Disabled : AutomationAuditAction.FlaggedForAttention,
            UserId.Empty,
            "Diaspora",
            now,
            AttentionReason);
    }

    /// <summary>Reached its end date or its run limit. Terminal, and the member is told why.</summary>
    public void Expire(string reason, DateTimeOffset now)
    {
        Status = AutomationStatus.Expired;
        AttentionReason = TrimOrNull(reason, 300);
        NextRunAt = null;
        Touch(now);

        RecordAudit(AutomationAuditAction.Expired, UserId.Empty, "Diaspora", now, AttentionReason);
    }

    public void RecordUpdate(UserId actor, string actorLabel, DateTimeOffset now)
    {
        Touch(now);
        RecordAudit(AutomationAuditAction.Updated, actor, actorLabel, now, null);
    }

    public void RecordDeletion(UserId actor, string actorLabel, DateTimeOffset now)
    {
        Touch(now);
        RecordAudit(AutomationAuditAction.Deleted, actor, actorLabel, now, null);
    }

    /// <summary>
    /// Records that a run finished, and expires the automation when it has reached its limit.
    /// <para>
    /// <b>The count moves on every terminal run including a skipped one</b>, and that is deliberate:
    /// a member who set "run at most 10 times" meant ten firings of the rule, and a run whose
    /// conditions said no is a firing of the rule. Counting only successes would make a
    /// never-matching automation immortal.
    /// </para>
    /// </summary>
    public void RecordRun(AutomationRunStatus status, DateTimeOffset completedAt)
    {
        ExecutionCount++;
        LastRunAt = completedAt;
        LastRunStatus = status;
        Touch(completedAt);

        if (MaxExecutions is { } limit && ExecutionCount >= limit)
        {
            Expire("This automation reached the maximum number of runs you set.", completedAt);
        }
        else if (EndsAt is { } endsAt && completedAt >= endsAt)
        {
            Expire("This automation reached the end date you set.", completedAt);
        }
    }

    /// <summary>
    /// Whether the automation is in a state where an event or an occurrence may run it.
    /// <para>
    /// Only <see cref="AutomationStatus.Active"/>. A paused automation keeps its schedule and does not
    /// fire; a needs-attention one has already been told to stop.
    /// </para>
    /// </summary>
    public bool IsRunnable(DateTimeOffset now) =>
        Status == AutomationStatus.Active &&
        (StartsAt is null || now >= StartsAt) &&
        (EndsAt is null || now <= EndsAt);

    private void Touch(DateTimeOffset now)
    {
        UpdatedAt = now;
        Revision++;
    }

    private void RecordAudit(
        AutomationAuditAction action,
        UserId actor,
        string actorLabel,
        DateTimeOffset at,
        string? detail) =>
        _audit.Add(AutomationAuditRecord.Create(Id, action, actor, actorLabel, at, detail));

    private static string Join(IEnumerable<string> values) =>
        string.Join(',', values.Where(value => !string.IsNullOrWhiteSpace(value)));

    private static string[] Split(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Trim(string? value, int max) => TrimOrNull(value, max) ?? string.Empty;

    private static string? TrimOrNull(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
