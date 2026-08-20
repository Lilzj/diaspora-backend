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
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Modules.CommunityProgramme.Domain.Volunteering;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.CommunityProgramme.Application.Volunteering;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// COMMUNITY-LIFE-GAP-010 — volunteer applications and assignments.
//
// Six routes. Two refusals the register names explicitly, and both are here:
//
//   "A full opportunity should waitlist rather than refuse — the kinder of the two correct answers."
//   The submission decides between Pending and Waitlisted from the counters, and Waitlisted is a
//   pending state so a place that frees up can promote somebody rather than starting again.
//
//   "Assigning an unapproved volunteer must be a 409." It is, and the check is on the application's
//   state rather than on anything the caller sends.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Offers to do a piece of volunteer work.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Participate)]
[Transactional(Reason =
    "Writes the application and its answers as one unit. An application that committed without its "
    + "answers would reach an organizer as a name and nothing to decide on.")]
public sealed record SubmitVolunteerApplicationCommand : ICommand<VolunteerApplicationResponse>
{
    public required Guid OpportunityId { get; init; }

    public required SubmitVolunteerApplicationRequest Request { get; init; }

    public string? IdempotencyKeyHeader { get; init; }
}

public sealed class SubmitVolunteerApplicationCommandValidator
    : AbstractValidator<SubmitVolunteerApplicationCommand>
{
    public SubmitVolunteerApplicationCommandValidator()
    {
        RuleFor(command => command.OpportunityId).NotEmpty();
        RuleFor(command => command.Request).NotNull();
    }
}

public sealed class SubmitVolunteerApplicationCommandHandler
    : ICommandHandler<SubmitVolunteerApplicationCommand, VolunteerApplicationResponse>
{
    private const string Operation = "volunteer-application";

    private readonly IVolunteerOpportunityRepository _opportunities;
    private readonly IVolunteerApplicationRepository _applications;
    private readonly CommunityProgrammeAccessService _access;
    private readonly VolunteerEligibilityService _eligibility;
    private readonly ProgrammeIdempotency _commit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public SubmitVolunteerApplicationCommandHandler(
        IVolunteerOpportunityRepository opportunities,
        IVolunteerApplicationRepository applications,
        CommunityProgrammeAccessService access,
        VolunteerEligibilityService eligibility,
        ProgrammeIdempotency commit,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock)
    {
        _opportunities = opportunities;
        _applications = applications;
        _access = access;
        _eligibility = eligibility;
        _commit = commit;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<VolunteerApplicationResponse>> Handle(
        SubmitVolunteerApplicationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<VolunteerApplicationResponse>(caller.Error);
        }

        var key = ProgrammeIdempotency.Normalise(
            Operation, command.IdempotencyKeyHeader, command.Request.IdempotencyKey);

        if (key is not null)
        {
            var replayed = await _applications.FindByIdempotencyKeyAsync(key, cancellationToken);

            if (replayed is not null)
            {
                return await RenderAsync(replayed, cancellationToken);
            }
        }

        var opportunity = await _opportunities.GetAsync(
            VolunteerOpportunityId.From(command.OpportunityId), cancellationToken);

        if (opportunity is null || !opportunity.IsDiscoverable)
        {
            return Result.Failure<VolunteerApplicationResponse>(
                CommunityProgrammeErrors.OpportunityNotFound);
        }

        var existing = await _applications.FindForVolunteerAsync(
            opportunity.Id, caller.Value, cancellationToken);

        if (existing is not null && !existing.IsTerminal)
        {
            return Result.Failure<VolunteerApplicationResponse>(
                CommunityProgrammeErrors.AlreadyApplied);
        }

        var reason = _eligibility.CannotApplyReason(opportunity, existing, caller.Value);

        if (reason is not null)
        {
            return Result.Failure<VolunteerApplicationResponse>(
                opportunity.ApplicationDeadline is { } deadline && deadline <= _clock.UtcNow
                    ? CommunityProgrammeErrors.ApplicationDeadlinePassed
                    : CommunityProgrammeErrors.OpportunityNotOpen);
        }

        VolunteerShiftId? shiftId = null;

        if (command.Request.ShiftId is { } requested)
        {
            var shift = opportunity.FindShift(VolunteerShiftId.From(requested));

            if (shift is null)
            {
                return Result.Failure<VolunteerApplicationResponse>(
                    CommunityProgrammeErrors.ShiftNotFound);
            }

            shiftId = shift.Id;
        }

        // Full waitlists rather than refuses. The counters are the opportunity's own, reconciled from
        // a recount, and the decision is made here rather than in the aggregate because it is a fact
        // about the opportunity rather than about this row.
        var opening = opportunity.IsFull
            ? VolunteerApplicationStatus.Waitlisted
            : VolunteerApplicationStatus.Pending;

        var now = _clock.UtcNow;

        var submitted = VolunteerApplication.Submit(
            VolunteerApplicationId.From(_guids.NewSequentialGuid()),
            opportunity.Id,
            opportunity.OrganizationId,
            caller.Value,
            shiftId,
            opening,
            now,
            (command.Request.Answers ?? new Dictionary<string, string>())
                .Take(CommunityProgrammeLimits.MaxAnswers)
                .Select(answer => ApplicationAnswer.Create(answer.Key, answer.Value)),
            key);

        if (submitted.IsFailure)
        {
            return Result.Failure<VolunteerApplicationResponse>(submitted.Error);
        }

        // A terminal application from a previous attempt still holds the unique (opportunity,
        // volunteer) row, so re-applying moves that row rather than adding one — the same rule the
        // membership rejoin follows, and for the same index.
        if (existing is not null)
        {
            var reopened = existing.MoveTo(opening, now);

            if (reopened.IsFailure)
            {
                return Result.Failure<VolunteerApplicationResponse>(
                    CommunityProgrammeErrors.AlreadyApplied);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return await RenderAsync(existing, cancellationToken);
        }

        _applications.Add(submitted.Value);

        var committed = await ProgrammeIdempotency.GuardAsync(
            key,
            token => _unitOfWork.SaveChangesAsync(token),
            token => key is null
                ? Task.FromResult<VolunteerApplication?>(null)
                : _applications.FindByIdempotencyKeyAsync(key, token),
            cancellationToken);

        if (committed.IsFailure)
        {
            return Result.Failure<VolunteerApplicationResponse>(committed.Error);
        }

        return await RenderAsync(committed.Value.Replayed ?? submitted.Value, cancellationToken);
    }

    private async Task<Result<VolunteerApplicationResponse>> RenderAsync(
        VolunteerApplication application, CancellationToken cancellationToken) =>
        Result.Success(await VolunteerRenderer.RenderAsync(
            application, _opportunities, _applications, _access, cancellationToken));
}

/// <summary>Every application the caller has made.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record ListMyVolunteerApplicationsQuery
    : IQuery<IReadOnlyList<VolunteerApplicationResponse>>;

public sealed class ListMyVolunteerApplicationsQueryHandler
    : IQueryHandler<ListMyVolunteerApplicationsQuery, IReadOnlyList<VolunteerApplicationResponse>>
{
    private readonly IVolunteerApplicationRepository _applications;
    private readonly IVolunteerOpportunityRepository _opportunities;
    private readonly IVolunteerAssignmentRepository _assignments;
    private readonly CommunityProgrammeAccessService _access;

    public ListMyVolunteerApplicationsQueryHandler(
        IVolunteerApplicationRepository applications,
        IVolunteerOpportunityRepository opportunities,
        IVolunteerAssignmentRepository assignments,
        CommunityProgrammeAccessService access)
    {
        _applications = applications;
        _opportunities = opportunities;
        _assignments = assignments;
        _access = access;
    }

    public async Task<Result<IReadOnlyList<VolunteerApplicationResponse>>> Handle(
        ListMyVolunteerApplicationsQuery query, CancellationToken cancellationToken)
    {
        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<VolunteerApplicationResponse>>(caller.Error);
        }

        var applications = await _applications.FindForVolunteerAsync(
            caller.Value, 100, cancellationToken);

        if (applications.Count == 0)
        {
            return Result.Success<IReadOnlyList<VolunteerApplicationResponse>>([]);
        }

        var opportunities = await _opportunities.FindManyAsync(
            [.. applications.Select(application => application.OpportunityId)], cancellationToken);

        var organizations = await _access.FindOrganizationsAsync(
            [.. applications.Select(application => application.OrganizationId)], cancellationToken);

        var assignments = (await _assignments.FindForVolunteerAsync(
                caller.Value, 100, cancellationToken))
            .GroupBy(assignment => assignment.ApplicationId)
            .ToDictionary(group => group.Key, group => group.First());

        var rows = new List<VolunteerApplicationResponse>();

        foreach (var application in applications)
        {
            opportunities.TryGetValue(application.OpportunityId, out var opportunity);
            assignments.TryGetValue(application.Id, out var assignment);

            rows.Add(CommunityProgrammeMapper.ToResponse(
                application,
                opportunity,
                organizations.GetValueOrDefault(application.OrganizationId),

                // The queue position is only meaningful while waiting, and computing it for every
                // row of a member's own history would be a query per terminal application.
                waitlistPosition: null,
                assignment is null
                    ? null
                    : CommunityProgrammeMapper.ToResponse(assignment, opportunity)));
        }

        return Result.Success<IReadOnlyList<VolunteerApplicationResponse>>(rows);
    }
}

/// <summary>The applicant withdraws.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Participate)]
public sealed record WithdrawVolunteerApplicationCommand : ICommand<VolunteerApplicationResponse>
{
    public required Guid ApplicationId { get; init; }
}

public sealed class WithdrawVolunteerApplicationCommandHandler
    : ICommandHandler<WithdrawVolunteerApplicationCommand, VolunteerApplicationResponse>
{
    private readonly IVolunteerApplicationRepository _applications;
    private readonly IVolunteerOpportunityRepository _opportunities;
    private readonly CommunityProgrammeAccessService _access;
    private readonly ProgrammeIdempotency _commit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public WithdrawVolunteerApplicationCommandHandler(
        IVolunteerApplicationRepository applications,
        IVolunteerOpportunityRepository opportunities,
        CommunityProgrammeAccessService access,
        ProgrammeIdempotency commit,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _applications = applications;
        _opportunities = opportunities;
        _access = access;
        _commit = commit;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<VolunteerApplicationResponse>> Handle(
        WithdrawVolunteerApplicationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<VolunteerApplicationResponse>(caller.Error);
        }

        var application = await _applications.GetAsync(
            VolunteerApplicationId.From(command.ApplicationId), cancellationToken);

        if (application is null || application.VolunteerUserId != caller.Value)
        {
            return Result.Failure<VolunteerApplicationResponse>(
                CommunityProgrammeErrors.ApplicationNotFound);
        }

        var moved = application.MoveTo(VolunteerApplicationStatus.Withdrawn, _clock.UtcNow);

        if (moved.IsFailure)
        {
            return Result.Failure<VolunteerApplicationResponse>(moved.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await VolunteerRenderer.RenderAsync(
            application, _opportunities, _applications, _access, cancellationToken));
    }
}

/// <summary>
/// The organizer's applicant queue.
/// <para>
/// Role-gated with a 403 — the register names this route: "refuses the applicant queue for an
/// opportunity the caller does not manage". Oldest first, because a queue is a queue.
/// </para>
/// </summary>
[Authorize(Permission = CommunityProgrammePermissions.ManageVolunteers)]
public sealed record ListApplicantsQuery : IQuery<CommunityPageResponse<VolunteerApplicantResponse>>
{
    public required Guid OpportunityId { get; init; }

    public string? State { get; init; }

    public string? Cursor { get; init; }

    public int? Take { get; init; }
}

public sealed class ListApplicantsQueryHandler
    : IQueryHandler<ListApplicantsQuery, CommunityPageResponse<VolunteerApplicantResponse>>
{
    private readonly IVolunteerApplicationRepository _applications;
    private readonly IVolunteerOpportunityRepository _opportunities;
    private readonly CommunityProgrammeAccessService _access;

    public ListApplicantsQueryHandler(
        IVolunteerApplicationRepository applications,
        IVolunteerOpportunityRepository opportunities,
        CommunityProgrammeAccessService access)
    {
        _applications = applications;
        _opportunities = opportunities;
        _access = access;
    }

    public async Task<Result<CommunityPageResponse<VolunteerApplicantResponse>>> Handle(
        ListApplicantsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var opportunityId = VolunteerOpportunityId.From(query.OpportunityId);
        var opportunity = await _opportunities.GetAsync(opportunityId, cancellationToken);

        if (opportunity is null)
        {
            return Result.Failure<CommunityPageResponse<VolunteerApplicantResponse>>(
                CommunityProgrammeErrors.OpportunityNotFound);
        }

        var permitted = await _access.RequireCapabilityAsync(
            opportunity.OrganizationId, OrganizationCapability.ManageVolunteers, cancellationToken);

        if (permitted.IsFailure)
        {
            return Result.Failure<CommunityPageResponse<VolunteerApplicantResponse>>(
                permitted.Error);
        }

        var page = await _applications.SearchAsync(
            new ApplicationQueryFilter
            {
                OpportunityId = opportunityId,
                Statuses = ParseStatuses(query.State),
                Cursor = query.Cursor,
                Limit = CommunityProgrammeLimits.PageSize(query.Take)
            },
            cancellationToken);

        var waitlist = await _applications.FindWaitlistOrderAsync(opportunityId, cancellationToken);

        return Result.Success(new CommunityPageResponse<VolunteerApplicantResponse>
        {
            Items =
            [
                .. page.Items.Select(application =>
                    CommunityProgrammeMapper.ToApplicantResponse(
                        application, PositionOf(waitlist, application)))
            ],
            NextCursor = page.NextCursor,
            HasMore = page.HasMore
        });
    }

    internal static int? PositionOf(
        IReadOnlyList<VolunteerApplicationId> waitlist, VolunteerApplication application)
    {
        if (application.Status != VolunteerApplicationStatus.Waitlisted)
        {
            return null;
        }

        var index = waitlist.ToList().IndexOf(application.Id);

        return index < 0 ? null : index + 1;
    }

    private static IReadOnlyList<VolunteerApplicationStatus>? ParseStatuses(string? state) =>
        string.IsNullOrWhiteSpace(state)
            ? null
            : Enum.TryParse<VolunteerApplicationStatus>(state, ignoreCase: true, out var parsed)
                ? [parsed]
                : [];
}

/// <summary>An organizer's decision about an applicant.</summary>
[Authorize(Permission = CommunityProgrammePermissions.ManageVolunteers)]
[Transactional(Reason =
    "Moves the application and reconciles the opportunity's counters as one unit. An approval that "
    + "committed without the recount would leave a place counted as free that somebody now holds.")]
public sealed record DecideVolunteerApplicationCommand : ICommand<VolunteerApplicantResponse>
{
    public required Guid ApplicationId { get; init; }

    public required DecideVolunteerApplicationRequest Request { get; init; }
}

public sealed class DecideVolunteerApplicationCommandValidator
    : AbstractValidator<DecideVolunteerApplicationCommand>
{
    private static readonly string[] Decisions = ["approve", "reject", "waitlist"];

    public DecideVolunteerApplicationCommandValidator()
    {
        RuleFor(command => command.ApplicationId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Decision)
            .NotEmpty()
            .Must(decision => Decisions.Contains(decision, StringComparer.OrdinalIgnoreCase))
            .WithMessage("A decision is one of approve, reject or waitlist.");

        RuleFor(command => command.Request.Reason)
            .MaximumLength(CommunityProgrammeLimits.MaxReasonLength);
    }
}

public sealed class DecideVolunteerApplicationCommandHandler
    : ICommandHandler<DecideVolunteerApplicationCommand, VolunteerApplicantResponse>
{
    private readonly IVolunteerApplicationRepository _applications;
    private readonly IVolunteerOpportunityRepository _opportunities;
    private readonly IVolunteerAssignmentRepository _assignments;
    private readonly CommunityProgrammeAccessService _access;
    private readonly ProgrammeIdempotency _commit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public DecideVolunteerApplicationCommandHandler(
        IVolunteerApplicationRepository applications,
        IVolunteerOpportunityRepository opportunities,
        IVolunteerAssignmentRepository assignments,
        CommunityProgrammeAccessService access,
        ProgrammeIdempotency commit,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _applications = applications;
        _opportunities = opportunities;
        _assignments = assignments;
        _access = access;
        _commit = commit;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<VolunteerApplicantResponse>> Handle(
        DecideVolunteerApplicationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<VolunteerApplicantResponse>(caller.Error);
        }

        var application = await _applications.GetAsync(
            VolunteerApplicationId.From(command.ApplicationId), cancellationToken);

        if (application is null)
        {
            return Result.Failure<VolunteerApplicantResponse>(
                CommunityProgrammeErrors.ApplicationNotFound);
        }

        var permitted = await _access.RequireCapabilityAsync(
            application.OrganizationId, OrganizationCapability.ManageVolunteers, cancellationToken);

        if (permitted.IsFailure)
        {
            return Result.Failure<VolunteerApplicantResponse>(permitted.Error);
        }

        var target = command.Request.Decision.ToLowerInvariant() switch
        {
            "approve" => VolunteerApplicationStatus.Approved,
            "reject" => VolunteerApplicationStatus.Rejected,
            _ => VolunteerApplicationStatus.Waitlisted
        };

        var moved = application.MoveTo(
            target, _clock.UtcNow, caller.Value, command.Request.Reason);

        if (moved.IsFailure)
        {
            return Result.Failure<VolunteerApplicantResponse>(moved.Error);
        }

        await VolunteerReconciliation.ReconcileAsync(
            application.OpportunityId, _opportunities, _applications, _assignments, _clock,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var waitlist = await _applications.FindWaitlistOrderAsync(
            application.OpportunityId, cancellationToken);

        return Result.Success(CommunityProgrammeMapper.ToApplicantResponse(
            application, ListApplicantsQueryHandler.PositionOf(waitlist, application)));
    }
}

/// <summary>
/// Renders one application with everything a screen needs around it.
/// <para>
/// Shared by four handlers, so an application rendered after a submission and one rendered after a
/// withdrawal carry the same fields — which is what stops a client branching on which route it
/// called.
/// </para>
/// </summary>
internal static class VolunteerRenderer
{
    public static async Task<VolunteerApplicationResponse> RenderAsync(
        VolunteerApplication application,
        IVolunteerOpportunityRepository opportunities,
        IVolunteerApplicationRepository applications,
        CommunityProgrammeAccessService access,
        CancellationToken cancellationToken)
    {
        var opportunity = await opportunities.GetAsync(
            application.OpportunityId, cancellationToken);

        var organization = await access.FindOrganizationAsync(
            application.OrganizationId, cancellationToken);

        int? position = null;

        if (application.IsWaiting)
        {
            var waitlist = await applications.FindWaitlistOrderAsync(
                application.OpportunityId, cancellationToken);

            position = ListApplicantsQueryHandler.PositionOf(waitlist, application);
        }

        return CommunityProgrammeMapper.ToResponse(
            application, opportunity, organization, position, assignment: null);
    }
}
