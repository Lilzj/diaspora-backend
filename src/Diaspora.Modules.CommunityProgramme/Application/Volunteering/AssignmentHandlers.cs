using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Application.Mapping;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Requests;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Capabilities;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Modules.CommunityProgramme.Domain.Volunteering;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.CommunityProgramme.Application.Volunteering;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// COMMUNITY-LIFE-GAP-011 — volunteer shifts, attendance, hours and certificates.
//
// The register's rule for this gap is one sentence and everything here follows from it: "A
// self-reported hour and a confirmed hour are different facts, and only the confirmed one may appear
// on a profile."
//
// So a check-in and a check-out are timestamps, and nothing counts them. ConfirmedHours is null until
// somebody who holds canManageVolunteers writes it, and the certificate is issued from that write and
// from no other.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Places an approved volunteer on a shift.</summary>
[Authorize(Permission = CommunityProgrammePermissions.ManageVolunteers)]
[Transactional(Reason =
    "Creates the assignment and reconciles the opportunity's and the shift's counters as one unit. "
    + "An assignment that committed without the recount would leave a place counted as free that "
    + "somebody now holds, and the next applicant would be told there was room.")]
public sealed record AssignVolunteerCommand : ICommand<VolunteerAssignmentResponse>
{
    public required AssignVolunteerRequest Request { get; init; }
}

public sealed class AssignVolunteerCommandValidator : AbstractValidator<AssignVolunteerCommand>
{
    public AssignVolunteerCommandValidator()
    {
        RuleFor(command => command.Request).NotNull();
        RuleFor(command => command.Request.ApplicationId).NotEmpty();
    }
}

public sealed class AssignVolunteerCommandHandler
    : ICommandHandler<AssignVolunteerCommand, VolunteerAssignmentResponse>
{
    private readonly IVolunteerApplicationRepository _applications;
    private readonly IVolunteerOpportunityRepository _opportunities;
    private readonly IVolunteerAssignmentRepository _assignments;
    private readonly CommunityProgrammeAccessService _access;
    private readonly ProgrammeIdempotency _commit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public AssignVolunteerCommandHandler(
        IVolunteerApplicationRepository applications,
        IVolunteerOpportunityRepository opportunities,
        IVolunteerAssignmentRepository assignments,
        CommunityProgrammeAccessService access,
        ProgrammeIdempotency commit,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock)
    {
        _applications = applications;
        _opportunities = opportunities;
        _assignments = assignments;
        _access = access;
        _commit = commit;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<VolunteerAssignmentResponse>> Handle(
        AssignVolunteerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var application = await _applications.GetAsync(
            VolunteerApplicationId.From(command.Request.ApplicationId), cancellationToken);

        if (application is null)
        {
            return Result.Failure<VolunteerAssignmentResponse>(
                CommunityProgrammeErrors.ApplicationNotFound);
        }

        var permitted = await _access.RequireCapabilityAsync(
            application.OrganizationId, OrganizationCapability.ManageVolunteers, cancellationToken);

        if (permitted.IsFailure)
        {
            return Result.Failure<VolunteerAssignmentResponse>(permitted.Error);
        }

        // "Assigning an unapproved volunteer must be a 409" — the register, verbatim. Checked on the
        // application's own state rather than on anything the caller sent.
        if (application.Status != VolunteerApplicationStatus.Approved)
        {
            return Result.Failure<VolunteerAssignmentResponse>(
                CommunityProgrammeErrors.ApplicationNotApproved);
        }

        var existing = await _assignments.FindForApplicationAsync(
            application.Id, cancellationToken);

        if (existing is not null)
        {
            // Assigning twice is an organizer pressing the button twice. The unique index would
            // refuse the second row; answering with the first is kinder and is the same outcome.
            var opportunityForExisting = await _opportunities.GetAsync(
                application.OpportunityId, cancellationToken);

            return Result.Success(
                CommunityProgrammeMapper.ToResponse(existing, opportunityForExisting));
        }

        var opportunity = await _opportunities.GetAsync(
            application.OpportunityId, cancellationToken);

        if (opportunity is null)
        {
            return Result.Failure<VolunteerAssignmentResponse>(
                CommunityProgrammeErrors.OpportunityNotFound);
        }

        var shiftId = command.Request.ShiftId is { } requested
            ? VolunteerShiftId.From(requested)
            : application.ShiftId;

        VolunteerShift? shift = null;

        if (opportunity.Shifts.Count > 0)
        {
            if (shiftId is null)
            {
                return Result.Failure<VolunteerAssignmentResponse>(
                    CommunityProgrammeErrors.Validation(
                        "This opportunity runs in shifts. Name the one to place them on."));
            }

            shift = opportunity.FindShift(shiftId);

            if (shift is null)
            {
                return Result.Failure<VolunteerAssignmentResponse>(
                    CommunityProgrammeErrors.ShiftNotFound);
            }

            if (shift.IsFull)
            {
                return Result.Failure<VolunteerAssignmentResponse>(
                    CommunityProgrammeErrors.ShiftFull);
            }
        }

        var now = _clock.UtcNow;

        var created = VolunteerAssignment.Create(
            VolunteerAssignmentId.From(_guids.NewSequentialGuid()),
            application.Id,
            opportunity.Id,
            opportunity.OrganizationId,
            application.VolunteerUserId,
            shift?.Id,
            shift?.StartsAt ?? opportunity.StartsAt ?? now,
            shift?.EndsAt ?? opportunity.EndsAt ?? opportunity.StartsAt ?? now,
            now);

        if (created.IsFailure)
        {
            return Result.Failure<VolunteerAssignmentResponse>(created.Error);
        }

        _assignments.Add(created.Value);

        await VolunteerReconciliation.ReconcileAsync(
            opportunity.Id, _opportunities, _applications, _assignments, _clock, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(CommunityProgrammeMapper.ToResponse(created.Value, opportunity));
    }
}

/// <summary>Every assignment the caller holds.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record ListMyAssignmentsQuery : IQuery<IReadOnlyList<VolunteerAssignmentResponse>>;

public sealed class ListMyAssignmentsQueryHandler
    : IQueryHandler<ListMyAssignmentsQuery, IReadOnlyList<VolunteerAssignmentResponse>>
{
    private readonly IVolunteerAssignmentRepository _assignments;
    private readonly IVolunteerOpportunityRepository _opportunities;
    private readonly CommunityProgrammeAccessService _access;

    public ListMyAssignmentsQueryHandler(
        IVolunteerAssignmentRepository assignments,
        IVolunteerOpportunityRepository opportunities,
        CommunityProgrammeAccessService access)
    {
        _assignments = assignments;
        _opportunities = opportunities;
        _access = access;
    }

    public async Task<Result<IReadOnlyList<VolunteerAssignmentResponse>>> Handle(
        ListMyAssignmentsQuery query, CancellationToken cancellationToken)
    {
        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<VolunteerAssignmentResponse>>(caller.Error);
        }

        var assignments = await _assignments.FindForVolunteerAsync(
            caller.Value, 100, cancellationToken);

        if (assignments.Count == 0)
        {
            return Result.Success<IReadOnlyList<VolunteerAssignmentResponse>>([]);
        }

        var opportunities = await _opportunities.FindManyAsync(
            [.. assignments.Select(assignment => assignment.OpportunityId)], cancellationToken);

        return Result.Success<IReadOnlyList<VolunteerAssignmentResponse>>(
        [
            .. assignments.Select(assignment => CommunityProgrammeMapper.ToResponse(
                assignment, opportunities.GetValueOrDefault(assignment.OpportunityId)))
        ]);
    }
}

/// <summary>
/// The volunteer arrives, or leaves.
/// <para>
/// <b>The volunteer's own act, and it proves nothing on its own.</b> A check-in is what the volunteer
/// says happened; the hours that reach their record are what an organizer stood behind. Both
/// timestamps are recorded and neither is counted.
/// </para>
/// </summary>
[Authorize(Permission = CommunityProgrammePermissions.Participate)]
public sealed record CheckVolunteerCommand : ICommand<VolunteerAssignmentResponse>
{
    public required Guid AssignmentId { get; init; }

    /// <summary><c>check-in</c> or <c>check-out</c>.</summary>
    public required string Direction { get; init; }
}

public sealed class CheckVolunteerCommandHandler
    : ICommandHandler<CheckVolunteerCommand, VolunteerAssignmentResponse>
{
    private readonly IVolunteerAssignmentRepository _assignments;
    private readonly IVolunteerOpportunityRepository _opportunities;
    private readonly CommunityProgrammeAccessService _access;
    private readonly ProgrammeIdempotency _commit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CheckVolunteerCommandHandler(
        IVolunteerAssignmentRepository assignments,
        IVolunteerOpportunityRepository opportunities,
        CommunityProgrammeAccessService access,
        ProgrammeIdempotency commit,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _assignments = assignments;
        _opportunities = opportunities;
        _access = access;
        _commit = commit;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<VolunteerAssignmentResponse>> Handle(
        CheckVolunteerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<VolunteerAssignmentResponse>(caller.Error);
        }

        var assignment = await _assignments.GetAsync(
            VolunteerAssignmentId.From(command.AssignmentId), cancellationToken);

        if (assignment is null)
        {
            return Result.Failure<VolunteerAssignmentResponse>(
                CommunityProgrammeErrors.AssignmentNotFound);
        }

        // The volunteer themselves, or an organizer working the door on their behalf. Both are real
        // ways a check-in happens, and refusing the second would make a paper rota necessary.
        var isVolunteer = assignment.VolunteerUserId == caller.Value;

        var isOrganizer = await _access.HoldsAsync(
            assignment.OrganizationId, OrganizationCapability.ManageVolunteers, cancellationToken);

        if (!isVolunteer && !isOrganizer)
        {
            return Result.Failure<VolunteerAssignmentResponse>(
                CommunityProgrammeErrors.AssignmentNotFound);
        }

        var now = _clock.UtcNow;

        var moved = command.Direction.ToLowerInvariant() switch
        {
            "check-in" => assignment.CheckIn(now),
            "check-out" => assignment.CheckOut(now),
            _ => Result.Failure(CommunityProgrammeErrors.Validation(
                "A check is either check-in or check-out."))
        };

        if (moved.IsFailure)
        {
            return Result.Failure<VolunteerAssignmentResponse>(moved.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var opportunity = await _opportunities.GetAsync(
            assignment.OpportunityId, cancellationToken);

        return Result.Success(CommunityProgrammeMapper.ToResponse(assignment, opportunity));
    }
}

/// <summary>
/// An organizer confirms the hours actually worked, which completes the assignment and issues the
/// certificate.
/// </summary>
[Authorize(Permission = CommunityProgrammePermissions.ManageVolunteers)]
[Transactional(Reason =
    "Confirms the hours and issues the certificate as one unit. A certificate that committed without "
    + "the confirmed hours behind it would be a credential with nothing to stand on, and confirmed "
    + "hours with no certificate would leave the volunteer unable to show them.")]
public sealed record ConfirmVolunteerHoursCommand : ICommand<VolunteerAssignmentResponse>
{
    public required Guid AssignmentId { get; init; }

    public required ConfirmVolunteerHoursRequest Request { get; init; }
}

public sealed class ConfirmVolunteerHoursCommandValidator
    : AbstractValidator<ConfirmVolunteerHoursCommand>
{
    public ConfirmVolunteerHoursCommandValidator()
    {
        RuleFor(command => command.AssignmentId).NotEmpty();
        RuleFor(command => command.Request).NotNull();
        RuleFor(command => command.Request.Hours).InclusiveBetween(0.25m, 24m);
    }
}

public sealed class ConfirmVolunteerHoursCommandHandler
    : ICommandHandler<ConfirmVolunteerHoursCommand, VolunteerAssignmentResponse>
{
    private readonly IVolunteerAssignmentRepository _assignments;
    private readonly IVolunteerOpportunityRepository _opportunities;
    private readonly IVolunteerApplicationRepository _applications;
    private readonly CommunityProgrammeAccessService _access;
    private readonly ProgrammeIdempotency _commit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public ConfirmVolunteerHoursCommandHandler(
        IVolunteerAssignmentRepository assignments,
        IVolunteerOpportunityRepository opportunities,
        IVolunteerApplicationRepository applications,
        CommunityProgrammeAccessService access,
        ProgrammeIdempotency commit,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock)
    {
        _assignments = assignments;
        _opportunities = opportunities;
        _applications = applications;
        _access = access;
        _commit = commit;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<VolunteerAssignmentResponse>> Handle(
        ConfirmVolunteerHoursCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<VolunteerAssignmentResponse>(caller.Error);
        }

        var assignment = await _assignments.GetAsync(
            VolunteerAssignmentId.From(command.AssignmentId), cancellationToken);

        if (assignment is null)
        {
            return Result.Failure<VolunteerAssignmentResponse>(
                CommunityProgrammeErrors.AssignmentNotFound);
        }

        var permitted = await _access.RequireCapabilityAsync(
            assignment.OrganizationId, OrganizationCapability.ManageVolunteers, cancellationToken);

        if (permitted.IsFailure)
        {
            return Result.Failure<VolunteerAssignmentResponse>(permitted.Error);
        }

        var opportunity = await _opportunities.GetAsync(
            assignment.OpportunityId, cancellationToken);

        var confirmed = assignment.ConfirmHours(
            command.Request.Hours, caller.Value, _clock.UtcNow);

        if (confirmed.IsFailure)
        {
            return Result.Failure<VolunteerAssignmentResponse>(confirmed.Error);
        }

        // The application follows the assignment to Completed, so a volunteer's own history reads
        // consistently from either side.
        var application = await _applications.GetAsync(assignment.ApplicationId, cancellationToken);

        application?.MoveTo(VolunteerApplicationStatus.Completed, _clock.UtcNow);

        // One certificate per assignment, enforced by a unique index and checked here so a second
        // confirmation answers rather than throwing.
        var existing = await _assignments.FindCertificateForAssignmentAsync(
            assignment.Id, cancellationToken);

        if (existing is null)
        {
            _assignments.AddCertificate(VolunteerCertificate.Issue(
                VolunteerCertificateId.From(_guids.NewSequentialGuid()),
                assignment.VolunteerUserId,
                assignment.OrganizationId,
                assignment.OpportunityId,
                assignment.Id,
                opportunity?.Title ?? "Volunteering",
                assignment.ConfirmedHours ?? command.Request.Hours,
                _clock.UtcNow));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(CommunityProgrammeMapper.ToResponse(assignment, opportunity));
    }
}

/// <summary>The caller's volunteering record. Confirmed hours only.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record GetMyVolunteerHoursQuery : IQuery<VolunteerHoursResponse>;

public sealed class GetMyVolunteerHoursQueryHandler
    : IQueryHandler<GetMyVolunteerHoursQuery, VolunteerHoursResponse>
{
    private readonly IVolunteerAssignmentRepository _assignments;
    private readonly CommunityProgrammeAccessService _access;

    public GetMyVolunteerHoursQueryHandler(
        IVolunteerAssignmentRepository assignments, CommunityProgrammeAccessService access)
    {
        _assignments = assignments;
        _access = access;
    }

    public async Task<Result<VolunteerHoursResponse>> Handle(
        GetMyVolunteerHoursQuery query, CancellationToken cancellationToken)
    {
        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<VolunteerHoursResponse>(caller.Error);
        }

        var confirmed = await _assignments.FindConfirmedForVolunteerAsync(
            caller.Value, cancellationToken: cancellationToken);

        var certificates = await _assignments.FindCertificatesForVolunteerAsync(
            caller.Value, 100, cancellationToken);

        var organizations = await _access.FindOrganizationsAsync(
            [.. certificates.Select(certificate => certificate.OrganizationId)], cancellationToken);

        return Result.Success(new VolunteerHoursResponse
        {
            ConfirmedHours = confirmed.Sum(assignment => assignment.ConfirmedHours ?? 0m),
            CompletedAssignments = confirmed.Count,
            OrganizationsSupported = confirmed
                .Select(assignment => assignment.OrganizationId)
                .Distinct()
                .Count(),
            Certificates =
            [
                .. certificates.Select(certificate => CommunityProgrammeMapper.ToResponse(
                    certificate, organizations.GetValueOrDefault(certificate.OrganizationId)))
            ]
        });
    }
}

/// <summary>The caller's issued certificates.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record ListMyCertificatesQuery : IQuery<IReadOnlyList<VolunteerCertificateResponse>>;

public sealed class ListMyCertificatesQueryHandler
    : IQueryHandler<ListMyCertificatesQuery, IReadOnlyList<VolunteerCertificateResponse>>
{
    private readonly IVolunteerAssignmentRepository _assignments;
    private readonly CommunityProgrammeAccessService _access;

    public ListMyCertificatesQueryHandler(
        IVolunteerAssignmentRepository assignments, CommunityProgrammeAccessService access)
    {
        _assignments = assignments;
        _access = access;
    }

    public async Task<Result<IReadOnlyList<VolunteerCertificateResponse>>> Handle(
        ListMyCertificatesQuery query, CancellationToken cancellationToken)
    {
        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<VolunteerCertificateResponse>>(caller.Error);
        }

        var certificates = await _assignments.FindCertificatesForVolunteerAsync(
            caller.Value, 100, cancellationToken);

        var organizations = await _access.FindOrganizationsAsync(
            [.. certificates.Select(certificate => certificate.OrganizationId)], cancellationToken);

        return Result.Success<IReadOnlyList<VolunteerCertificateResponse>>(
        [
            .. certificates.Select(certificate => CommunityProgrammeMapper.ToResponse(
                certificate, organizations.GetValueOrDefault(certificate.OrganizationId)))
        ]);
    }
}

/// <summary>
/// Recounts an opportunity's places from the rows that hold them.
/// <para>
/// <b>A recount rather than a delta, everywhere.</b> A handler that incremented a counter would
/// compound whatever drift it inherited; a recount is idempotent and repairs whatever went wrong
/// earlier — which is why the sweep and the write path can both call it and neither has to know
/// whether the other ran.
/// </para>
/// </summary>
internal static class VolunteerReconciliation
{
    public static async Task ReconcileAsync(
        VolunteerOpportunityId opportunityId,
        IVolunteerOpportunityRepository opportunities,
        IVolunteerApplicationRepository applications,
        IVolunteerAssignmentRepository assignments,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var opportunity = await opportunities.GetAsync(opportunityId, cancellationToken);

        if (opportunity is null)
        {
            return;
        }

        var counts = await applications.CountForOpportunityAsync(opportunityId, cancellationToken);
        var byShift = await assignments.CountByShiftAsync(opportunityId, cancellationToken);

        opportunity.Reconcile(counts.Filled, counts.Waiting, byShift, clock.UtcNow);
    }
}
