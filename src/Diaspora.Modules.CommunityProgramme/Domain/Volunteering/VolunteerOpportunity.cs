using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Events;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Memberships;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Domain.Volunteering;

/// <summary>
/// A piece of work an organization is asking its community to do.
/// <para>
/// <b>Why this is not a B15 contribution.</b> <c>POST /financial/contributions</c> already records a
/// non-monetary pledge of time or skills against a fund, and where a fund exists that is still the
/// right route — B18 does not duplicate it. What a contribution cannot express is a <em>shift</em>:
/// four stewards, Saturday 09:00 to 13:00, over eighteens only. It has a quantity and a unit and no
/// date, no place, no capacity, no eligibility and no approval, and every one of those is what makes
/// the difference between a pledge and a rota.
/// </para>
/// <para>
/// <b>Shifts are owned rows.</b> They are always read with the opportunity, and a shift outside its
/// opportunity is not a thing. Their filled counts move independently and are reconciled from a
/// recount rather than incremented, for the reason every counter on this platform is.
/// </para>
/// <para>
/// <b><see cref="IsFull"/> and the eligibility decision are the server's.</b> The register asks for
/// <c>canApply</c> and <c>cannotApplyReason</c> "evaluated server-side and returned as a decision the
/// client renders rather than computes", and <c>canApply</c> depends on facts a device does not have:
/// whether the caller already applied, whether their account is verified, how many places are left.
/// </para>
/// </summary>
public sealed class VolunteerOpportunity
    : AggregateRoot<VolunteerOpportunityId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<VolunteerShift> _shifts = [];
    private readonly List<OpportunitySkill> _requiredSkills = [];

    private VolunteerOpportunity(
        VolunteerOpportunityId id,
        Guid organizationId,
        UserId createdBy,
        string title,
        string description,
        string cause,
        DateTimeOffset now) : base(id)
    {
        OrganizationId = organizationId;
        CreatedByUserId = createdBy;
        Title = title;
        Description = description;
        Cause = cause;
        Status = VolunteerOpportunityStatus.Draft;
        CreatedAt = now;
    }

    private VolunteerOpportunity() { }

    public Guid OrganizationId { get; private set; }

    public UserId CreatedByUserId { get; private set; } = default!;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    /// <summary>What the work is for. Free text the organization chooses; a filter, not a catalogue.</summary>
    public string Cause { get; private set; } = string.Empty;

    public VolunteerOpportunityStatus Status { get; private set; }

    public string? CountryCode { get; private set; }

    public string? CityName { get; private set; }

    public string? AreaName { get; private set; }

    public string? AddressLine { get; private set; }

    /// <summary>Null for an ongoing opportunity with no fixed date.</summary>
    public DateTimeOffset? StartsAt { get; private set; }

    public DateTimeOffset? EndsAt { get; private set; }

    public DateTimeOffset? ApplicationDeadline { get; private set; }

    public int? MinimumAge { get; private set; }

    /// <summary>Null where the opportunity is uncapped.</summary>
    public int? Capacity { get; private set; }

    /// <summary>How many approved volunteers hold a place. Reconciled, never accumulated.</summary>
    public int FilledCount { get; private set; }

    /// <summary>How many are waiting. Reconciled with the above.</summary>
    public int WaitlistCount { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>B15's idempotency primitive, for creation. Uniquely indexed.</summary>
    public string? IdempotencyKey { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public IReadOnlyList<VolunteerShift> Shifts =>
        _shifts.OrderBy(shift => shift.StartsAt).ToList().AsReadOnly();

    public IReadOnlyList<string> RequiredSkills =>
        _requiredSkills.Select(skill => skill.Name).ToList().AsReadOnly();

    // ── Auditing and deletion ───────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    /// <summary>Server-decided. An uncapped opportunity is never full.</summary>
    public bool IsFull => Capacity is { } capacity && FilledCount >= capacity;

    /// <summary>Whether the opportunity is visible to somebody who does not manage it.</summary>
    public bool IsDiscoverable =>
        Status is VolunteerOpportunityStatus.Published
            or VolunteerOpportunityStatus.Full
            or VolunteerOpportunityStatus.Closed
            or VolunteerOpportunityStatus.Completed;

    /// <summary>Whether it is still taking applications at all.</summary>
    public bool AcceptsApplications =>
        Status is VolunteerOpportunityStatus.Published or VolunteerOpportunityStatus.Full;

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation and editing
    // ════════════════════════════════════════════════════════════════════════════════════════

    public static Result<VolunteerOpportunity> Draft(
        VolunteerOpportunityId id,
        Guid organizationId,
        UserId createdBy,
        string title,
        string? description,
        string? cause,
        DateTimeOffset now,
        string? idempotencyKey = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(createdBy);

        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure<VolunteerOpportunity>(
                CommunityProgrammeErrors.Validation("An opportunity needs a title."));
        }

        return Result.Success(new VolunteerOpportunity(
            id,
            organizationId,
            createdBy,
            MembershipProgramme.Clip(title, CommunityProgrammeLimits.MaxNameLength)!,
            MembershipProgramme.Clip(description, CommunityProgrammeLimits.MaxDescriptionLength)
                ?? string.Empty,
            MembershipProgramme.Clip(cause, CommunityProgrammeLimits.MaxLabelLength) ?? string.Empty,
            now)
        {
            IdempotencyKey = MembershipProgramme.Clip(
                idempotencyKey, CommunityProgrammeLimits.MaxIdempotencyKeyLength)
        });
    }

    public void Describe(
        string? title,
        string? description,
        string? cause,
        string? countryCode,
        string? cityName,
        string? areaName,
        string? addressLine,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt,
        DateTimeOffset? applicationDeadline,
        int? minimumAge,
        int? capacity,
        DateTimeOffset now)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            Title = MembershipProgramme.Clip(title, CommunityProgrammeLimits.MaxNameLength)!;
        }

        if (description is not null)
        {
            Description =
                MembershipProgramme.Clip(description, CommunityProgrammeLimits.MaxDescriptionLength)
                ?? string.Empty;
        }

        if (cause is not null)
        {
            Cause = MembershipProgramme.Clip(cause, CommunityProgrammeLimits.MaxLabelLength)
                ?? string.Empty;
        }

        CountryCode = Normalise(countryCode, CommunityProgrammeLimits.MaxCountryLength) ?? CountryCode;
        CityName = MembershipProgramme.Clip(cityName, CommunityProgrammeLimits.MaxNameLength) ?? CityName;
        AreaName = MembershipProgramme.Clip(areaName, CommunityProgrammeLimits.MaxNameLength) ?? AreaName;
        AddressLine =
            MembershipProgramme.Clip(addressLine, CommunityProgrammeLimits.MaxSummaryLength)
            ?? AddressLine;

        StartsAt = startsAt ?? StartsAt;
        EndsAt = endsAt ?? EndsAt;
        ApplicationDeadline = applicationDeadline ?? ApplicationDeadline;
        MinimumAge = minimumAge is > 0 ? minimumAge : MinimumAge;
        Capacity = capacity is > 0 ? capacity : Capacity;

        Revision++;
        ModifiedAt = now;
    }

    /// <summary>
    /// Replaces the shift list wholesale, for the same reason the tier list is replaced wholesale.
    /// <para>
    /// A shift that appears in both the old and the new list keeps its filled count: it is the same
    /// shift, and re-posting the form must not free places the assignments still occupy.
    /// </para>
    /// </summary>
    public Result ReplaceShifts(IReadOnlyList<VolunteerShift> shifts, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(shifts);

        if (shifts.Count > CommunityProgrammeLimits.MaxShiftsPerOpportunity)
        {
            return Result.Failure(CommunityProgrammeErrors.Validation(
                $"An opportunity may have at most "
                + $"{CommunityProgrammeLimits.MaxShiftsPerOpportunity} shifts."));
        }

        var existing = _shifts.ToDictionary(shift => shift.Id);

        foreach (var shift in shifts)
        {
            if (existing.TryGetValue(shift.Id, out var previous))
            {
                shift.CarryFilledCount(previous.FilledCount);
            }
        }

        _shifts.Clear();
        _shifts.AddRange(shifts);

        Revision++;
        ModifiedAt = now;

        return Result.Success();
    }

    public void ReplaceSkills(IEnumerable<string> skills, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(skills);

        _requiredSkills.Clear();

        foreach (var skill in skills
            .Select(name => MembershipProgramme.Clip(name, CommunityProgrammeLimits.MaxSkillLength))
            .Where(name => name is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(CommunityProgrammeLimits.MaxSkillsPerOpportunity))
        {
            _requiredSkills.Add(OpportunitySkill.Create(skill!));
        }

        Revision++;
        ModifiedAt = now;
    }

    public VolunteerShift? FindShift(VolunteerShiftId shiftId) =>
        _shifts.FirstOrDefault(shift => shift.Id == shiftId);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The one lifecycle move, so the table is consulted once and the event is raised once.
    /// <para>
    /// <b>A move to the state it is already in succeeds and does nothing</b>, which is what makes the
    /// sweep that closes opportunities past their deadline idempotent.
    /// </para>
    /// </summary>
    public Result MoveTo(VolunteerOpportunityStatus target, DateTimeOffset now)
    {
        if (Status == target)
        {
            return Result.Success();
        }

        if (!CanTransition(Status, target))
        {
            return Result.Failure(CommunityProgrammeErrors.OpportunityTransitionInvalid);
        }

        var previous = Status;

        Status = target;
        Revision++;
        ModifiedAt = now;

        if (target == VolunteerOpportunityStatus.Published)
        {
            PublishedAt ??= now;
        }

        RaiseDomainEvent(new VolunteerOpportunityStatusChangedDomainEvent
        {
            OpportunityId = Id,
            OrganizationId = OrganizationId,
            PreviousStatus = previous,
            CurrentStatus = target,
            ChangedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Sets the counters from a recount, and moves between Published and Full accordingly.
    /// <para>
    /// <b>Deliberately does not bump the revision.</b> A recount landing while an organizer edits the
    /// shift list must not make the organizer's save fail.
    /// </para>
    /// <para>
    /// <b>Full is a state rather than a computed flag</b> because the register asks for it in the
    /// status enum, and because an opportunity that fills and then frees a place has to become
    /// Published again — which a computed flag would do silently and a state does visibly.
    /// </para>
    /// </summary>
    public void Reconcile(
        int filledCount,
        int waitlistCount,
        IReadOnlyDictionary<VolunteerShiftId, int> filledByShift,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(filledByShift);

        FilledCount = Math.Max(0, filledCount);
        WaitlistCount = Math.Max(0, waitlistCount);

        foreach (var shift in _shifts)
        {
            shift.SetFilledCount(filledByShift.TryGetValue(shift.Id, out var filled) ? filled : 0);
        }

        Status = Status switch
        {
            VolunteerOpportunityStatus.Published when IsFull => VolunteerOpportunityStatus.Full,
            VolunteerOpportunityStatus.Full when !IsFull => VolunteerOpportunityStatus.Published,
            _ => Status
        };

        ModifiedAt = now;
    }

    private static bool CanTransition(
        VolunteerOpportunityStatus from, VolunteerOpportunityStatus to) => from switch
        {
            VolunteerOpportunityStatus.Draft =>
                to is VolunteerOpportunityStatus.Published or VolunteerOpportunityStatus.Cancelled,

            VolunteerOpportunityStatus.Published =>
                to is VolunteerOpportunityStatus.Full
                    or VolunteerOpportunityStatus.Closed
                    or VolunteerOpportunityStatus.Completed
                    or VolunteerOpportunityStatus.Cancelled,

            VolunteerOpportunityStatus.Full =>
                to is VolunteerOpportunityStatus.Published
                    or VolunteerOpportunityStatus.Closed
                    or VolunteerOpportunityStatus.Completed
                    or VolunteerOpportunityStatus.Cancelled,

            VolunteerOpportunityStatus.Closed =>
                to is VolunteerOpportunityStatus.Published
                    or VolunteerOpportunityStatus.Completed
                    or VolunteerOpportunityStatus.Cancelled,

            // Both terminal.
            _ => false
        };

    // ── Auditing and deletion ───────────────────────────────────────────────────────────────

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }

    private static string? Normalise(string? value, int max)
    {
        var clipped = MembershipProgramme.Clip(value, max);

        return clipped?.ToUpperInvariant();
    }
}

/// <summary>
/// One dated slot on an opportunity.
/// <para>
/// <b>Its capacity is the one that binds.</b> An opportunity's own capacity is the total across the
/// work; a shift's is how many bodies fit in that room at that hour, and a volunteer applying for a
/// full shift is waitlisted for it even when the opportunity as a whole has places.
/// </para>
/// </summary>
public sealed class VolunteerShift
{
    private VolunteerShift(
        VolunteerShiftId id,
        string label,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        int capacity)
    {
        Id = id;
        Label = label;
        StartsAt = startsAt;
        EndsAt = endsAt;
        Capacity = capacity;
    }

    private VolunteerShift() { }

    public VolunteerShiftId Id { get; private set; } = default!;

    public string Label { get; private set; } = string.Empty;

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset EndsAt { get; private set; }

    public int Capacity { get; private set; }

    public int FilledCount { get; private set; }

    /// <summary>Server-decided.</summary>
    public bool IsFull => FilledCount >= Capacity;

    public static VolunteerShift Create(
        VolunteerShiftId id,
        string? label,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        int capacity)
    {
        ArgumentNullException.ThrowIfNull(id);

        return new VolunteerShift(
            id,
            MembershipProgramme.Clip(label, CommunityProgrammeLimits.MaxLabelLength) ?? string.Empty,
            startsAt,
            endsAt < startsAt ? startsAt : endsAt,
            Math.Max(1, capacity));
    }

    internal void CarryFilledCount(int filledCount) => FilledCount = Math.Max(0, filledCount);

    internal void SetFilledCount(int filledCount) => FilledCount = Math.Max(0, filledCount);
}

/// <summary>
/// One skill an opportunity asks for.
/// <para>
/// An owned entity with a declared key rather than a bare string collection, because EF needs one —
/// an owned collection without <c>HasKey</c> gets a store-generated shadow integer in a composite
/// primary key, and SQLite refuses the insert the moment a second row exists.
/// </para>
/// </summary>
public sealed class OpportunitySkill
{
    private OpportunitySkill(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    private OpportunitySkill() { }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public static OpportunitySkill Create(string name) => new(Guid.NewGuid(), name);
}
