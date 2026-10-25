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
// COMMUNITY-LIFE-GAP-009 — volunteer opportunities.
//
// The register's own framing is exact: a B15 contribution is a pledge against a *fund*, with a
// quantity and a unit, and "it cannot express four stewards needed, Saturday 09:00–13:00, must be
// over 18". Everything in this file is that sentence — a shift, a capacity, an eligibility rule and
// a deadline — and nothing in it duplicates the pledge, which stays B15's.
//
// canApply and cannotApplyReason are the server's decision on every read. They depend on whether the
// caller already applied, whether the deadline has passed, whether the opportunity is taking
// applications at all and whether the account is verified — four facts, none of which is on the
// device.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>The volunteer opportunity list. Anonymous, and drafts are absent from it.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record ListOpportunitiesQuery
    : IQuery<CommunityPageResponse<VolunteerOpportunityResponse>>
{
    public string? Query { get; init; }

    public string? Cause { get; init; }

    public Guid? OrganizationId { get; init; }

    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    public string? Skill { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public string? State { get; init; }

    public string? Cursor { get; init; }

    public int? Take { get; init; }
}

public sealed class ListOpportunitiesQueryHandler
    : IQueryHandler<ListOpportunitiesQuery, CommunityPageResponse<VolunteerOpportunityResponse>>
{
    private readonly IVolunteerOpportunityRepository _opportunities;
    private readonly IVolunteerApplicationRepository _applications;
    private readonly CommunityProgrammeAccessService _access;
    private readonly VolunteerEligibilityService _eligibility;

    public ListOpportunitiesQueryHandler(
        IVolunteerOpportunityRepository opportunities,
        IVolunteerApplicationRepository applications,
        CommunityProgrammeAccessService access,
        VolunteerEligibilityService eligibility)
    {
        _opportunities = opportunities;
        _applications = applications;
        _access = access;
        _eligibility = eligibility;
    }

    public async Task<Result<CommunityPageResponse<VolunteerOpportunityResponse>>> Handle(
        ListOpportunitiesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var managed = await _access.ManagedForAsync(
            OrganizationCapability.ManageVolunteers, cancellationToken);

        var page = await _opportunities.SearchAsync(
            new OpportunityQueryFilter
            {
                SearchTerm = query.Query,
                Cause = query.Cause,
                OrganizationId = query.OrganizationId,
                CountryCode = query.CountryCode,
                CityName = query.CityName,
                Skill = query.Skill,
                From = query.From,
                To = query.To,
                Statuses = ParseStatuses(query.State),
                ManagedOrganizationIds = managed,
                Cursor = query.Cursor,
                Limit = CommunityProgrammeLimits.PageSize(query.Take)
            },
            cancellationToken);

        if (page.Items.Count == 0)
        {
            return Result.Success(new CommunityPageResponse<VolunteerOpportunityResponse>
            {
                Items = [],
                HasMore = false
            });
        }

        var organizations = await _access.FindOrganizationsAsync(
            [.. page.Items.Select(opportunity => opportunity.OrganizationId)], cancellationToken);

        var caller = _access.CallerId;

        var mine = caller is null
            ? []
            : (await _applications.FindForVolunteerAsync(caller, 200, cancellationToken))
                .GroupBy(application => application.OpportunityId)
                .ToDictionary(group => group.Key, group => group.First());

        var rows = new List<VolunteerOpportunityResponse>();

        foreach (var opportunity in page.Items)
        {
            mine.TryGetValue(opportunity.Id, out var application);

            var reason = _eligibility.CannotApplyReason(opportunity, application, caller);

            rows.Add(CommunityProgrammeMapper.ToResponse(
                opportunity,
                organizations.GetValueOrDefault(opportunity.OrganizationId),
                application,
                canApply: reason is null,
                cannotApplyReason: reason,
                canManage: managed.Contains(opportunity.OrganizationId)));
        }

        return Result.Success(new CommunityPageResponse<VolunteerOpportunityResponse>
        {
            Items = rows,
            NextCursor = page.NextCursor,
            HasMore = page.HasMore
        });
    }

    private static IReadOnlyList<VolunteerOpportunityStatus>? ParseStatuses(string? state) =>
        string.IsNullOrWhiteSpace(state)
            ? null
            : Enum.TryParse<VolunteerOpportunityStatus>(state, ignoreCase: true, out var parsed)
                ? [parsed]
                : [];
}

/// <summary>One opportunity, with the caller's own standing and the server's decision.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record GetOpportunityQuery : IQuery<VolunteerOpportunityResponse>
{
    public required Guid OpportunityId { get; init; }
}

public sealed class GetOpportunityQueryHandler
    : IQueryHandler<GetOpportunityQuery, VolunteerOpportunityResponse>
{
    private readonly IVolunteerOpportunityRepository _opportunities;
    private readonly IVolunteerApplicationRepository _applications;
    private readonly CommunityProgrammeAccessService _access;
    private readonly VolunteerEligibilityService _eligibility;

    public GetOpportunityQueryHandler(
        IVolunteerOpportunityRepository opportunities,
        IVolunteerApplicationRepository applications,
        CommunityProgrammeAccessService access,
        VolunteerEligibilityService eligibility)
    {
        _opportunities = opportunities;
        _applications = applications;
        _access = access;
        _eligibility = eligibility;
    }

    public async Task<Result<VolunteerOpportunityResponse>> Handle(
        GetOpportunityQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var opportunity = await _opportunities.GetAsync(
            VolunteerOpportunityId.From(query.OpportunityId), cancellationToken);

        if (opportunity is null)
        {
            return Result.Failure<VolunteerOpportunityResponse>(
                CommunityProgrammeErrors.OpportunityNotFound);
        }

        var canManage = await _access.HoldsAsync(
            opportunity.OrganizationId, OrganizationCapability.ManageVolunteers, cancellationToken);

        // A draft answers not-found to everybody but the people who may manage it. The same answer as
        // an identifier that never existed, deliberately.
        if (!opportunity.IsDiscoverable && !canManage)
        {
            return Result.Failure<VolunteerOpportunityResponse>(
                CommunityProgrammeErrors.OpportunityNotFound);
        }

        var organization = await _access.FindOrganizationAsync(
            opportunity.OrganizationId, cancellationToken);

        var caller = _access.CallerId;

        var application = caller is null
            ? null
            : await _applications.FindForVolunteerAsync(opportunity.Id, caller, cancellationToken);

        var reason = _eligibility.CannotApplyReason(opportunity, application, caller);

        return Result.Success(CommunityProgrammeMapper.ToResponse(
            opportunity, organization, application, reason is null, reason, canManage));
    }
}

/// <summary>The shifts on an opportunity, for a screen that only draws the rota.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record ListShiftsQuery : IQuery<IReadOnlyList<VolunteerShiftResponse>>
{
    public required Guid OpportunityId { get; init; }
}

public sealed class ListShiftsQueryHandler
    : IQueryHandler<ListShiftsQuery, IReadOnlyList<VolunteerShiftResponse>>
{
    private readonly IVolunteerOpportunityRepository _opportunities;
    private readonly CommunityProgrammeAccessService _access;

    public ListShiftsQueryHandler(
        IVolunteerOpportunityRepository opportunities, CommunityProgrammeAccessService access)
    {
        _opportunities = opportunities;
        _access = access;
    }

    public async Task<Result<IReadOnlyList<VolunteerShiftResponse>>> Handle(
        ListShiftsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var opportunity = await _opportunities.GetAsync(
            VolunteerOpportunityId.From(query.OpportunityId), cancellationToken);

        if (opportunity is null)
        {
            return Result.Failure<IReadOnlyList<VolunteerShiftResponse>>(
                CommunityProgrammeErrors.OpportunityNotFound);
        }

        if (!opportunity.IsDiscoverable
            && !await _access.HoldsAsync(
                opportunity.OrganizationId,
                OrganizationCapability.ManageVolunteers,
                cancellationToken))
        {
            return Result.Failure<IReadOnlyList<VolunteerShiftResponse>>(
                CommunityProgrammeErrors.OpportunityNotFound);
        }

        return Result.Success<IReadOnlyList<VolunteerShiftResponse>>(
        [
            .. opportunity.Shifts.Select(shift =>
                CommunityProgrammeMapper.ToResponse(shift, opportunity.Id.Value))
        ]);
    }
}

/// <summary>Creates a volunteer opportunity.</summary>
[Authorize(Permission = CommunityProgrammePermissions.ManageVolunteers)]
[Transactional(Reason =
    "Writes the opportunity, its shifts and its required skills as one unit. An opportunity that "
    + "committed without its shifts would be a rota with no slots, and a volunteer applying to it "
    + "would be applying to nothing.")]
public sealed record CreateOpportunityCommand : ICommand<VolunteerOpportunityResponse>
{
    public required SaveVolunteerOpportunityRequest Request { get; init; }

    public string? IdempotencyKeyHeader { get; init; }
}

public sealed class CreateOpportunityCommandValidator : AbstractValidator<CreateOpportunityCommand>
{
    public CreateOpportunityCommandValidator()
    {
        RuleFor(command => command.Request).NotNull();
        RuleFor(command => command.Request.OrganizationId).NotEmpty();

        RuleFor(command => command.Request.Title)
            .NotEmpty()
            .MaximumLength(CommunityProgrammeLimits.MaxNameLength);

        RuleFor(command => command.Request.Description)
            .MaximumLength(CommunityProgrammeLimits.MaxDescriptionLength);

        RuleFor(command => command.Request.MinimumAge)
            .InclusiveBetween(1, 120)
            .When(command => command.Request.MinimumAge is not null);

        RuleFor(command => command.Request.Capacity)
            .GreaterThan(0)
            .When(command => command.Request.Capacity is not null);

        RuleForEach(command => command.Request.Shifts).ChildRules(shift =>
        {
            shift.RuleFor(value => value.Capacity).GreaterThan(0);

            shift.RuleFor(value => value.EndsAt)
                .GreaterThan(value => value.StartsAt)
                .WithMessage("A shift ends after it starts.");
        });
    }
}

public sealed class CreateOpportunityCommandHandler
    : ICommandHandler<CreateOpportunityCommand, VolunteerOpportunityResponse>
{
    private const string Operation = "volunteer-opportunity";

    private readonly IVolunteerOpportunityRepository _opportunities;
    private readonly CommunityProgrammeAccessService _access;
    private readonly VolunteerEligibilityService _eligibility;
    private readonly ProgrammeIdempotency _commit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateOpportunityCommandHandler(
        IVolunteerOpportunityRepository opportunities,
        CommunityProgrammeAccessService access,
        VolunteerEligibilityService eligibility,
        ProgrammeIdempotency commit,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock)
    {
        _opportunities = opportunities;
        _access = access;
        _eligibility = eligibility;
        _commit = commit;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<VolunteerOpportunityResponse>> Handle(
        CreateOpportunityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<VolunteerOpportunityResponse>(caller.Error);
        }

        var permitted = await _access.RequireCapabilityAsync(
            command.Request.OrganizationId,
            OrganizationCapability.ManageVolunteers,
            cancellationToken);

        if (permitted.IsFailure)
        {
            return Result.Failure<VolunteerOpportunityResponse>(permitted.Error);
        }

        var key = ProgrammeIdempotency.Normalise(
            Operation, caller.Value.Value, command.IdempotencyKeyHeader, command.Request.IdempotencyKey);

        if (key is not null)
        {
            var replayed = await _opportunities.FindByIdempotencyKeyAsync(
                caller.Value, key, cancellationToken);

            if (replayed is not null)
            {
                // The caller's own key, spent on another organization's opportunity.
                if (replayed.OrganizationId != command.Request.OrganizationId)
                {
                    return Result.Failure<VolunteerOpportunityResponse>(
                        CommunityProgrammeErrors.IdempotencyKeyReused);
                }

                return await RenderAsync(replayed, cancellationToken);
            }
        }

        var now = _clock.UtcNow;

        var drafted = VolunteerOpportunity.Draft(
            VolunteerOpportunityId.From(_guids.NewSequentialGuid()),
            command.Request.OrganizationId,
            caller.Value,
            command.Request.Title,
            command.Request.Description,
            command.Request.Cause,
            now,
            key);

        if (drafted.IsFailure)
        {
            return Result.Failure<VolunteerOpportunityResponse>(drafted.Error);
        }

        var opportunity = drafted.Value;

        opportunity.Describe(
            command.Request.Title,
            command.Request.Description,
            command.Request.Cause,
            command.Request.CountryCode,
            command.Request.CityName,
            command.Request.AreaName,
            command.Request.AddressLine,
            command.Request.StartsAt,
            command.Request.EndsAt,
            command.Request.ApplicationDeadline,
            command.Request.MinimumAge,
            command.Request.Capacity,
            now);

        var shifts = BuildShifts(command.Request);
        var replaced = opportunity.ReplaceShifts(shifts, now);

        if (replaced.IsFailure)
        {
            return Result.Failure<VolunteerOpportunityResponse>(replaced.Error);
        }

        opportunity.ReplaceSkills(command.Request.RequiredSkills ?? [], now);

        if (command.Request.Publish)
        {
            var published = opportunity.MoveTo(VolunteerOpportunityStatus.Published, now);

            if (published.IsFailure)
            {
                return Result.Failure<VolunteerOpportunityResponse>(published.Error);
            }
        }

        _opportunities.Add(opportunity);

        var committed = await ProgrammeIdempotency.GuardAsync(
            key,
            token => _unitOfWork.SaveChangesAsync(token),
            token => key is null
                ? Task.FromResult<VolunteerOpportunity?>(null)
                : _opportunities.FindByIdempotencyKeyAsync(caller.Value, key, token),
            cancellationToken,
            inTransaction: true);

        if (committed.IsFailure)
        {
            return Result.Failure<VolunteerOpportunityResponse>(committed.Error);
        }

        return await RenderAsync(committed.Value.Replayed ?? opportunity, cancellationToken);
    }

    private IReadOnlyList<VolunteerShift> BuildShifts(SaveVolunteerOpportunityRequest request) =>
    [
        .. (request.Shifts ?? [])
            .Take(CommunityProgrammeLimits.MaxShiftsPerOpportunity)
            .Select(shift => VolunteerShift.Create(
                shift.ShiftId is { } supplied
                    ? VolunteerShiftId.From(supplied)
                    : VolunteerShiftId.From(_guids.NewSequentialGuid()),
                shift.Label,
                shift.StartsAt,
                shift.EndsAt,
                shift.Capacity))
    ];

    private async Task<Result<VolunteerOpportunityResponse>> RenderAsync(
        VolunteerOpportunity opportunity, CancellationToken cancellationToken)
    {
        var organization = await _access.FindOrganizationAsync(
            opportunity.OrganizationId, cancellationToken);

        return Result.Success(CommunityProgrammeMapper.ToResponse(
            opportunity,
            organization,
            myApplication: null,

            // The organizer who just created it is not an applicant. Saying so plainly beats
            // computing an eligibility answer nobody asked for.
            canApply: false,
            cannotApplyReason: VolunteerEligibilityService.OrganizerCannotApply,
            canManage: true));
    }
}

/// <summary>Rewrites a volunteer opportunity.</summary>
[Authorize(Permission = CommunityProgrammePermissions.ManageVolunteers)]
[Transactional(Reason =
    "Rewrites the opportunity, its shifts and its skills as one unit, for the same reason creating "
    + "it is one unit.")]
public sealed record UpdateOpportunityCommand : ICommand<VolunteerOpportunityResponse>
{
    public required Guid OpportunityId { get; init; }

    public required SaveVolunteerOpportunityRequest Request { get; init; }
}

public sealed class UpdateOpportunityCommandHandler
    : ICommandHandler<UpdateOpportunityCommand, VolunteerOpportunityResponse>
{
    private readonly IVolunteerOpportunityRepository _opportunities;
    private readonly CommunityProgrammeAccessService _access;
    private readonly VolunteerEligibilityService _eligibility;
    private readonly ProgrammeIdempotency _commit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public UpdateOpportunityCommandHandler(
        IVolunteerOpportunityRepository opportunities,
        CommunityProgrammeAccessService access,
        VolunteerEligibilityService eligibility,
        ProgrammeIdempotency commit,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock)
    {
        _opportunities = opportunities;
        _access = access;
        _eligibility = eligibility;
        _commit = commit;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<VolunteerOpportunityResponse>> Handle(
        UpdateOpportunityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var opportunity = await _opportunities.GetAsync(
            VolunteerOpportunityId.From(command.OpportunityId), cancellationToken);

        if (opportunity is null)
        {
            return Result.Failure<VolunteerOpportunityResponse>(
                CommunityProgrammeErrors.OpportunityNotFound);
        }

        var permitted = await _access.RequireCapabilityAsync(
            opportunity.OrganizationId, OrganizationCapability.ManageVolunteers, cancellationToken);

        if (permitted.IsFailure)
        {
            return Result.Failure<VolunteerOpportunityResponse>(permitted.Error);
        }

        var now = _clock.UtcNow;

        opportunity.Describe(
            command.Request.Title,
            command.Request.Description,
            command.Request.Cause,
            command.Request.CountryCode,
            command.Request.CityName,
            command.Request.AreaName,
            command.Request.AddressLine,
            command.Request.StartsAt,
            command.Request.EndsAt,
            command.Request.ApplicationDeadline,
            command.Request.MinimumAge,
            command.Request.Capacity,
            now);

        if (command.Request.Shifts is not null)
        {
            var shifts = command.Request.Shifts
                .Take(CommunityProgrammeLimits.MaxShiftsPerOpportunity)
                .Select(shift => VolunteerShift.Create(
                    shift.ShiftId is { } supplied
                        ? VolunteerShiftId.From(supplied)
                        : VolunteerShiftId.From(_guids.NewSequentialGuid()),
                    shift.Label,
                    shift.StartsAt,
                    shift.EndsAt,
                    shift.Capacity))
                .ToList();

            var replaced = opportunity.ReplaceShifts(shifts, now);

            if (replaced.IsFailure)
            {
                return Result.Failure<VolunteerOpportunityResponse>(replaced.Error);
            }
        }

        if (command.Request.RequiredSkills is not null)
        {
            opportunity.ReplaceSkills(command.Request.RequiredSkills, now);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var organization = await _access.FindOrganizationAsync(
            opportunity.OrganizationId, cancellationToken);

        return Result.Success(CommunityProgrammeMapper.ToResponse(
            opportunity,
            organization,
            myApplication: null,
            canApply: false,
            cannotApplyReason: VolunteerEligibilityService.OrganizerCannotApply,
            canManage: true));
    }
}

/// <summary>Publishes, closes or cancels an opportunity.</summary>
[Authorize(Permission = CommunityProgrammePermissions.ManageVolunteers)]
public sealed record ActOnOpportunityCommand : ICommand<VolunteerOpportunityResponse>
{
    public required Guid OpportunityId { get; init; }

    /// <summary><c>publish</c>, <c>close</c>, <c>cancel</c> or <c>complete</c>.</summary>
    public required string Action { get; init; }
}

public sealed class ActOnOpportunityCommandHandler
    : ICommandHandler<ActOnOpportunityCommand, VolunteerOpportunityResponse>
{
    private readonly IVolunteerOpportunityRepository _opportunities;
    private readonly CommunityProgrammeAccessService _access;
    private readonly VolunteerEligibilityService _eligibility;
    private readonly ProgrammeIdempotency _commit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ActOnOpportunityCommandHandler(
        IVolunteerOpportunityRepository opportunities,
        CommunityProgrammeAccessService access,
        VolunteerEligibilityService eligibility,
        ProgrammeIdempotency commit,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _opportunities = opportunities;
        _access = access;
        _eligibility = eligibility;
        _commit = commit;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<VolunteerOpportunityResponse>> Handle(
        ActOnOpportunityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var opportunity = await _opportunities.GetAsync(
            VolunteerOpportunityId.From(command.OpportunityId), cancellationToken);

        if (opportunity is null)
        {
            return Result.Failure<VolunteerOpportunityResponse>(
                CommunityProgrammeErrors.OpportunityNotFound);
        }

        var permitted = await _access.RequireCapabilityAsync(
            opportunity.OrganizationId, OrganizationCapability.ManageVolunteers, cancellationToken);

        if (permitted.IsFailure)
        {
            return Result.Failure<VolunteerOpportunityResponse>(permitted.Error);
        }

        var target = command.Action.ToLowerInvariant() switch
        {
            "publish" => VolunteerOpportunityStatus.Published,
            "close" => VolunteerOpportunityStatus.Closed,
            "complete" => VolunteerOpportunityStatus.Completed,
            "cancel" => VolunteerOpportunityStatus.Cancelled,
            _ => (VolunteerOpportunityStatus?)null
        };

        if (target is not { } status)
        {
            return Result.Failure<VolunteerOpportunityResponse>(
                CommunityProgrammeErrors.Validation(
                    $"'{command.Action}' is not something you can do to an opportunity."));
        }

        var moved = opportunity.MoveTo(status, _clock.UtcNow);

        if (moved.IsFailure)
        {
            return Result.Failure<VolunteerOpportunityResponse>(moved.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var organization = await _access.FindOrganizationAsync(
            opportunity.OrganizationId, cancellationToken);

        return Result.Success(CommunityProgrammeMapper.ToResponse(
            opportunity,
            organization,
            myApplication: null,
            canApply: false,
            cannotApplyReason: VolunteerEligibilityService.OrganizerCannotApply,
            canManage: true));
    }
}
