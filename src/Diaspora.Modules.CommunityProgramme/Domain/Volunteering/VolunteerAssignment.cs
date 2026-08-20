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
/// An approved volunteer placed on a shift, and the hours they were confirmed for.
/// <para>
/// <b>Confirmed hours are the whole reason this aggregate exists.</b> A check-in and a check-out are
/// what the volunteer says happened; <see cref="ConfirmedHours"/> is what an organizer stood behind.
/// The register is explicit — §34 forbids inventing an impact metric, and a self-reported hour on a
/// member's profile is exactly that — so nothing downstream counts the timestamps and everything
/// counts this field, which is null until somebody with the capability sets it.
/// </para>
/// <para>
/// <b>Checking in does not confirm hours, even though the arithmetic is available.</b> A volunteer
/// who checked in and went home has a check-in and a check-out and did not do the work. The gap
/// between "the phone says four hours" and "the organizer says four hours" is the only thing that
/// makes a volunteering record worth putting on a profile.
/// </para>
/// </summary>
public sealed class VolunteerAssignment
    : AggregateRoot<VolunteerAssignmentId>, IAuditableEntity
{
    private VolunteerAssignment(
        VolunteerAssignmentId id,
        VolunteerApplicationId applicationId,
        VolunteerOpportunityId opportunityId,
        Guid organizationId,
        UserId volunteerUserId,
        VolunteerShiftId? shiftId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        DateTimeOffset now) : base(id)
    {
        ApplicationId = applicationId;
        OpportunityId = opportunityId;
        OrganizationId = organizationId;
        VolunteerUserId = volunteerUserId;
        ShiftId = shiftId;
        StartsAt = startsAt;
        EndsAt = endsAt;
        Status = VolunteerAssignmentStatus.Assigned;
        CreatedAt = now;
    }

    private VolunteerAssignment() { }

    public VolunteerApplicationId ApplicationId { get; private set; } = default!;

    public VolunteerOpportunityId OpportunityId { get; private set; } = default!;

    /// <summary>Copied from the opportunity, for the same reason the application copies it.</summary>
    public Guid OrganizationId { get; private set; }

    public UserId VolunteerUserId { get; private set; } = default!;

    public VolunteerShiftId? ShiftId { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset EndsAt { get; private set; }

    public VolunteerAssignmentStatus Status { get; private set; }

    public DateTimeOffset? CheckedInAt { get; private set; }

    public DateTimeOffset? CheckedOutAt { get; private set; }

    /// <summary>
    /// What an organizer stood behind. Null until they do — never self-reported, never derived from
    /// the timestamps above.
    /// </summary>
    public decimal? ConfirmedHours { get; private set; }

    public UserId? ConfirmedByUserId { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    // ── Auditing ────────────────────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    public bool IsTerminal => Status is VolunteerAssignmentStatus.Completed
        or VolunteerAssignmentStatus.NoShow
        or VolunteerAssignmentStatus.Cancelled;

    /// <summary>Whether the assignment counts toward the opportunity's filled places.</summary>
    public bool OccupiesPlace => Status is VolunteerAssignmentStatus.Assigned
        or VolunteerAssignmentStatus.Confirmed
        or VolunteerAssignmentStatus.CheckedIn
        or VolunteerAssignmentStatus.Completed;

    public static Result<VolunteerAssignment> Create(
        VolunteerAssignmentId id,
        VolunteerApplicationId applicationId,
        VolunteerOpportunityId opportunityId,
        Guid organizationId,
        UserId volunteerUserId,
        VolunteerShiftId? shiftId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(applicationId);
        ArgumentNullException.ThrowIfNull(opportunityId);
        ArgumentNullException.ThrowIfNull(volunteerUserId);

        var assignment = new VolunteerAssignment(
            id,
            applicationId,
            opportunityId,
            organizationId,
            volunteerUserId,
            shiftId,
            startsAt,
            endsAt < startsAt ? startsAt : endsAt,
            now);

        assignment.RaiseDomainEvent(new VolunteerAssignmentCreatedDomainEvent
        {
            AssignmentId = id,
            ApplicationId = applicationId,
            OpportunityId = opportunityId,
            OrganizationId = organizationId,
            VolunteerUserId = volunteerUserId,
            ShiftId = shiftId,
            StartsAt = startsAt,
            CreatedAt = now
        });

        return Result.Success(assignment);
    }

    public Result MoveTo(VolunteerAssignmentStatus target, DateTimeOffset now)
    {
        if (Status == target)
        {
            return Result.Success();
        }

        if (!CanTransition(Status, target))
        {
            return Result.Failure(CommunityProgrammeErrors.AssignmentTransitionInvalid);
        }

        Status = target;
        Revision++;
        ModifiedAt = now;

        return Result.Success();
    }

    /// <summary>The volunteer arrives.</summary>
    public Result CheckIn(DateTimeOffset now)
    {
        var moved = MoveTo(VolunteerAssignmentStatus.CheckedIn, now);

        if (moved.IsFailure)
        {
            return moved;
        }

        CheckedInAt ??= now;

        return Result.Success();
    }

    /// <summary>
    /// The volunteer leaves.
    /// <para>
    /// Records the moment and does not move the state: the assignment is complete when the organizer
    /// confirms the hours, not when the volunteer's phone says they left.
    /// </para>
    /// </summary>
    public Result CheckOut(DateTimeOffset now)
    {
        if (Status != VolunteerAssignmentStatus.CheckedIn)
        {
            return Result.Failure(CommunityProgrammeErrors.AssignmentTransitionInvalid);
        }

        CheckedOutAt = now;
        Revision++;
        ModifiedAt = now;

        return Result.Success();
    }

    /// <summary>
    /// An organizer confirms what was actually done, which completes the assignment.
    /// <para>
    /// <b>Only from <see cref="VolunteerAssignmentStatus.CheckedIn"/>.</b> Confirming hours for
    /// somebody who never arrived would make the check-in optional, and a record of hours nobody
    /// witnessed is the thing this whole aggregate exists to avoid.
    /// </para>
    /// </summary>
    public Result ConfirmHours(decimal hours, UserId confirmedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(confirmedBy);

        if (Status != VolunteerAssignmentStatus.CheckedIn)
        {
            return Result.Failure(CommunityProgrammeErrors.HoursNotConfirmable);
        }

        if (hours <= 0 || hours > 24)
        {
            return Result.Failure(CommunityProgrammeErrors.Validation(
                "Confirmed hours must be between 0 and 24."));
        }

        ConfirmedHours = Math.Round(hours, 2, MidpointRounding.AwayFromZero);
        ConfirmedByUserId = confirmedBy;
        ConfirmedAt = now;
        CheckedOutAt ??= now;
        Status = VolunteerAssignmentStatus.Completed;
        Revision++;
        ModifiedAt = now;

        RaiseDomainEvent(new VolunteerHoursConfirmedDomainEvent
        {
            AssignmentId = Id,
            OpportunityId = OpportunityId,
            OrganizationId = OrganizationId,
            VolunteerUserId = VolunteerUserId,
            Hours = ConfirmedHours.Value,
            ConfirmedBy = confirmedBy,
            ConfirmedAt = now
        });

        return Result.Success();
    }

    private static bool CanTransition(
        VolunteerAssignmentStatus from, VolunteerAssignmentStatus to) => from switch
        {
            VolunteerAssignmentStatus.Assigned =>
                to is VolunteerAssignmentStatus.Confirmed
                    or VolunteerAssignmentStatus.CheckedIn
                    or VolunteerAssignmentStatus.NoShow
                    or VolunteerAssignmentStatus.Cancelled,

            VolunteerAssignmentStatus.Confirmed =>
                to is VolunteerAssignmentStatus.CheckedIn
                    or VolunteerAssignmentStatus.NoShow
                    or VolunteerAssignmentStatus.Cancelled,

            VolunteerAssignmentStatus.CheckedIn =>
                to is VolunteerAssignmentStatus.Completed
                    or VolunteerAssignmentStatus.Cancelled,

            _ => false
        };

    // ── Auditing ────────────────────────────────────────────────────────────────────────────

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
}

/// <summary>
/// An issued record of confirmed volunteering.
/// <para>
/// <b>Issued when hours are confirmed, and never before.</b> A certificate is a claim about the world
/// somebody has to stand behind; issuing one from a check-in would be issuing it from the volunteer's
/// own word. The row records the organization that confirmed, so a certificate can always be traced
/// back to whoever is answerable for it.
/// </para>
/// <para>
/// <b>No download URL yet, and that absence is honest.</b> Rendering a signed PDF is a document
/// service this platform does not have. The certificate is real, listed and verifiable; the file is
/// what a later phase adds, and the field stays null rather than pointing at something that 404s.
/// </para>
/// </summary>
public sealed class VolunteerCertificate
    : AggregateRoot<VolunteerCertificateId>, IAuditableEntity
{
    private VolunteerCertificate(
        VolunteerCertificateId id,
        UserId volunteerUserId,
        Guid organizationId,
        VolunteerOpportunityId opportunityId,
        VolunteerAssignmentId assignmentId,
        string title,
        decimal hours,
        DateTimeOffset issuedAt) : base(id)
    {
        VolunteerUserId = volunteerUserId;
        OrganizationId = organizationId;
        OpportunityId = opportunityId;
        AssignmentId = assignmentId;
        Title = title;
        Hours = hours;
        IssuedAt = issuedAt;
        CreatedAt = issuedAt;
    }

    private VolunteerCertificate() { }

    public UserId VolunteerUserId { get; private set; } = default!;

    public Guid OrganizationId { get; private set; }

    public VolunteerOpportunityId OpportunityId { get; private set; } = default!;

    /// <summary>The assignment it was issued for. Uniquely indexed: one certificate per assignment.</summary>
    public VolunteerAssignmentId AssignmentId { get; private set; } = default!;

    public string Title { get; private set; } = string.Empty;

    public decimal Hours { get; private set; }

    public DateTimeOffset IssuedAt { get; private set; }

    // ── Auditing ────────────────────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static VolunteerCertificate Issue(
        VolunteerCertificateId id,
        UserId volunteerUserId,
        Guid organizationId,
        VolunteerOpportunityId opportunityId,
        VolunteerAssignmentId assignmentId,
        string title,
        decimal hours,
        DateTimeOffset issuedAt) =>
        new(
            id,
            volunteerUserId,
            organizationId,
            opportunityId,
            assignmentId,
            MembershipProgramme.Clip(title, CommunityProgrammeLimits.MaxNameLength) ?? string.Empty,
            hours,
            issuedAt);

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
}
