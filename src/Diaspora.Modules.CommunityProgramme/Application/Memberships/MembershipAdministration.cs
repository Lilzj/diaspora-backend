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
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.CommunityProgramme.Application.Memberships;

/// <summary>
/// The organization's member roll.
/// <para>
/// <b>Role-gated with a 403 rather than filtered.</b> The register is explicit, and this is the route
/// it names: "refuses the member list for an organization the caller does not administer". A caller
/// without <c>canManageMembers</c> gets a refusal, not an empty page — because an empty page and a
/// forbidden page are different facts and a client that could not tell them apart would render "this
/// organization has no members".
/// </para>
/// <para>
/// <b>Identifiers and standing, and no contact of any kind.</b> §45 names member information among
/// the things not to leak; the roll carries what an administrator needs to act — who, which tier,
/// what state, since when — and a client that wants display names asks Identity for them under its
/// own authorization.
/// </para>
/// </summary>
[Authorize(Permission = CommunityProgrammePermissions.ManageProgramme)]
public sealed record ListOrganizationMembersQuery
    : IQuery<CommunityPageResponse<OrganizationMemberResponse>>
{
    public required Guid OrganizationId { get; init; }

    /// <summary>One of the fourteen states, or absent for every state.</summary>
    public string? State { get; init; }

    public string? Cursor { get; init; }

    public int? Take { get; init; }
}

public sealed class ListOrganizationMembersQueryHandler
    : IQueryHandler<ListOrganizationMembersQuery, CommunityPageResponse<OrganizationMemberResponse>>
{
    private readonly IProgrammeMembershipRepository _memberships;
    private readonly IMembershipProgrammeRepository _programmes;
    private readonly CommunityProgrammeAccessService _access;

    public ListOrganizationMembersQueryHandler(
        IProgrammeMembershipRepository memberships,
        IMembershipProgrammeRepository programmes,
        CommunityProgrammeAccessService access)
    {
        _memberships = memberships;
        _programmes = programmes;
        _access = access;
    }

    public async Task<Result<CommunityPageResponse<OrganizationMemberResponse>>> Handle(
        ListOrganizationMembersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var permitted = await _access.RequireCapabilityAsync(
            query.OrganizationId, OrganizationCapability.ManageMembers, cancellationToken);

        if (permitted.IsFailure)
        {
            return Result.Failure<CommunityPageResponse<OrganizationMemberResponse>>(
                permitted.Error);
        }

        var statuses = ParseStatuses(query.State);

        var page = await _memberships.SearchAsync(
            new MembershipRollFilter
            {
                OrganizationId = query.OrganizationId,
                Statuses = statuses,
                Cursor = query.Cursor,
                Limit = CommunityProgrammeLimits.PageSize(query.Take)
            },
            cancellationToken);

        var programme = await _programmes.FindByOrganizationAsync(
            query.OrganizationId, cancellationToken);

        var rows = new List<OrganizationMemberResponse>();

        foreach (var membership in page.Items)
        {
            // The B12 role, when the member holds one. Asked per row because B12's directory has no
            // batch read for it and a roll page is bounded at fifty — a cost worth paying for the
            // one field an administrator uses to decide who to promote.
            var organizationMembership = await _access.FindOrganizationMembershipAsync(
                query.OrganizationId, membership.MemberUserId.Value, cancellationToken);

            rows.Add(CommunityProgrammeMapper.ToRollResponse(
                membership,
                programme?.FindTier(membership.TierId),
                organizationMembership));
        }

        return Result.Success(new CommunityPageResponse<OrganizationMemberResponse>
        {
            Items = rows,
            NextCursor = page.NextCursor,
            HasMore = page.HasMore
        });
    }

    private static IReadOnlyList<ProgrammeMembershipStatus>? ParseStatuses(string? state) =>
        string.IsNullOrWhiteSpace(state)
            ? null
            : Enum.TryParse<ProgrammeMembershipStatus>(state, ignoreCase: true, out var parsed)
                ? [parsed]

                // An unrecognised filter answers nothing rather than everything. Silently ignoring it
                // would hand an administrator the whole roll when they asked for pending applications.
                : [];
}

/// <summary>
/// An organization's decision about a member.
/// <para>
/// <b>Five decisions, and <c>waitlist</c> is deliberately not among them.</b> The register lists it,
/// but a membership has no waiting list: a full tier refuses the application at submission rather
/// than accepting it into a queue, because a place in a paid tier that frees up is not automatically
/// the next applicant's — an organization chooses. A volunteer opportunity <em>does</em> waitlist,
/// and that is where the state lives.
/// </para>
/// <para>
/// <b>The reason is surfaced to the member verbatim.</b> An organization that refuses somebody owes
/// them the sentence it wrote, not a code.
/// </para>
/// </summary>
[Authorize(Permission = CommunityProgrammePermissions.ManageProgramme)]
[Transactional(Reason =
    "Moves the membership and appends its history entry as one unit. A decision recorded without "
    + "its history entry would leave the member with a state nobody can explain.")]
public sealed record DecideMembershipCommand : ICommand<OrganizationMemberResponse>
{
    public required Guid MembershipId { get; init; }

    public required DecideMembershipRequest Request { get; init; }
}

public sealed class DecideMembershipCommandValidator : AbstractValidator<DecideMembershipCommand>
{
    private static readonly string[] Decisions =
        ["approve", "reject", "suspend", "reinstate", "remove"];

    public DecideMembershipCommandValidator()
    {
        RuleFor(command => command.MembershipId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Decision)
            .NotEmpty()
            .Must(decision => Decisions.Contains(decision, StringComparer.OrdinalIgnoreCase))
            .WithMessage(
                "A decision is one of approve, reject, suspend, reinstate or remove.");

        RuleFor(command => command.Request.Reason)
            .MaximumLength(CommunityProgrammeLimits.MaxReasonLength);
    }
}

public sealed class DecideMembershipCommandHandler
    : ICommandHandler<DecideMembershipCommand, OrganizationMemberResponse>
{
    private readonly IProgrammeMembershipRepository _memberships;
    private readonly IMembershipProgrammeRepository _programmes;
    private readonly CommunityProgrammeAccessService _access;
    private readonly ProgrammeIdempotency _commit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public DecideMembershipCommandHandler(
        IProgrammeMembershipRepository memberships,
        IMembershipProgrammeRepository programmes,
        CommunityProgrammeAccessService access,
        ProgrammeIdempotency commit,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock)
    {
        _memberships = memberships;
        _programmes = programmes;
        _access = access;
        _commit = commit;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<OrganizationMemberResponse>> Handle(
        DecideMembershipCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<OrganizationMemberResponse>(caller.Error);
        }

        var membership = await _memberships.GetAsync(
            ProgrammeMembershipId.From(command.MembershipId), cancellationToken);

        if (membership is null)
        {
            return Result.Failure<OrganizationMemberResponse>(
                CommunityProgrammeErrors.MembershipNotFound);
        }

        var permitted = await _access.RequireCapabilityAsync(
            membership.OrganizationId, OrganizationCapability.ApproveMembers, cancellationToken);

        if (permitted.IsFailure)
        {
            return Result.Failure<OrganizationMemberResponse>(permitted.Error);
        }

        var programme = await _programmes.FindByOrganizationAsync(
            membership.OrganizationId, cancellationToken);

        var tier = programme?.FindTier(membership.TierId);
        var now = _clock.UtcNow;

        var target = command.Request.Decision.ToLowerInvariant() switch
        {
            "approve" => ProgrammeMembershipStatus.Approved,
            "reject" => ProgrammeMembershipStatus.Rejected,
            "suspend" => ProgrammeMembershipStatus.Suspended,
            "reinstate" => ProgrammeMembershipStatus.Active,

            // Removal ends the membership. It is recorded as cancelled by the organization rather
            // than by the member, which the history entry's actor distinguishes.
            _ => ProgrammeMembershipStatus.Cancelled
        };

        var moved = membership.MoveTo(
            target,
            _guids.NewSequentialGuid(),
            now,
            MembershipActor.Organization,
            caller.Value,
            command.Request.Reason,
            target == ProgrammeMembershipStatus.Active ? tier?.RenewalMonths : null);

        if (moved.IsFailure)
        {
            return Result.Failure<OrganizationMemberResponse>(moved.Error);
        }

        // An approval on a free tier activates immediately; a priced one waits for B15. The same
        // rule the application handler applies, stated once in each place it is decided rather than
        // shared through a helper that would hide which of the two made the call.
        if (target == ProgrammeMembershipStatus.Approved && tier?.Price is null)
        {
            var activated = membership.MoveTo(
                ProgrammeMembershipStatus.Active,
                _guids.NewSequentialGuid(),
                now,
                MembershipActor.Organization,
                caller.Value,
                renewalMonths: tier?.RenewalMonths);

            if (activated.IsFailure)
            {
                return Result.Failure<OrganizationMemberResponse>(activated.Error);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var organizationMembership = await _access.FindOrganizationMembershipAsync(
            membership.OrganizationId, membership.MemberUserId.Value, cancellationToken);

        return Result.Success(CommunityProgrammeMapper.ToRollResponse(
            membership, tier, organizationMembership));
    }
}
