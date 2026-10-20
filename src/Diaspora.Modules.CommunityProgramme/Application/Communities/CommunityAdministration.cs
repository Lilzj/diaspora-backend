using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Requests;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.Modules.CommunityProgramme.Application.Communities;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Community administration — founding, roles, and the two acts staff keep.
//
// **The decision this implements**, from the product owner on 11 Sep 2026: "the person that creates
// the community is the owner of the community and can also assign roles to members, and a community
// creator is automatically member of the administrator".
//
// **What already existed and was never reached.** `CommunityMembershipRole` had Moderator, Ambassador
// and Administrator. `CommunityMembership.Grant` and `RevokeRole` existed. `CommunityCapabilitySet`
// derived `CanManageRoles` from the Administrator role. `Community.Propose` existed. Nothing called
// any of them — there was no route that founded a community and no route that gave anybody a role,
// so every community on the platform had members and nobody who ran it. The directory seeder said
// so in as many words: "there is no route that creates one".
//
// **What each act answers to.**
//   - Founding — any verified member. The founder is Owner and Administrator from the first moment.
//     The community opens Pending, the domain's own rule, and joins nobody else until staff open it.
//   - Moderator and Ambassador — the Owner or any Administrator.
//   - Administrator — the Owner alone. An administrator who could make administrators could hand the
//     community to anybody, and one who could remove them could remove its founder.
//   - Owner — nobody, through this route. Founding makes one; staff appoint one to a community that
//     has none; handing a community on is a transfer, and a separate act this does not build.
//   - Opening a pending community, and appointing an owner — platform administrators, not moderators.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Found a community. The caller becomes its Owner and one of its Administrators.</summary>
[Authorize(Permission = CommunityProgrammePermissions.ProposeCommunity)]
public sealed record ProposeCommunityCommand : ICommand<CommunityMembershipResponse>
{
    public required ProposeCommunityRequest Request { get; init; }
}

public sealed class ProposeCommunityCommandHandler
    : ICommandHandler<ProposeCommunityCommand, CommunityMembershipResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IGuidGenerator _guids;
    private readonly IUnitOfWork _unitOfWork;

    public ProposeCommunityCommandHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        CommunityProgrammeAccessService access,
        IGuidGenerator guids,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _memberships = memberships;
        _access = access;
        _guids = guids;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityMembershipResponse>> Handle(
        ProposeCommunityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityMembershipResponse>(caller.Error);
        }

        var request = command.Request;

        if (!Enum.TryParse<CommunityType>(request.Type, ignoreCase: true, out var type)
            || !Enum.IsDefined(type))
        {
            return Result.Failure<CommunityMembershipResponse>(CommunityProgrammeErrors.Validation(
                "Choose what kind of community this is."));
        }

        var visibility = CommunityVisibility.Public;

        if (!string.IsNullOrWhiteSpace(request.Visibility)
            && (!Enum.TryParse(request.Visibility, ignoreCase: true, out visibility)
                || !Enum.IsDefined(visibility)))
        {
            return Result.Failure<CommunityMembershipResponse>(CommunityProgrammeErrors.Validation(
                "Choose who can find this community: Public, Unlisted, Private or InviteOnly."));
        }

        var slug = CommunitySlug.FromName(request.Name);

        if (slug.IsFailure)
        {
            return Result.Failure<CommunityMembershipResponse>(slug.Error);
        }

        // Checked before proposing rather than left to the unique index, so the member reads a
        // sentence about the name rather than a conflict about a constraint.
        if (await _communities.IsSlugTakenAsync(slug.Value, cancellationToken))
        {
            return Result.Failure<CommunityMembershipResponse>(CommunityErrors.SlugTaken);
        }

        var proposed = CommunityAggregate.Propose(
            CommunityId.From(_guids.NewSequentialGuid()),
            slug.Value,
            request.Name,
            type,
            visibility,
            request.OriginCountryCode,
            request.OriginRegionName);

        if (proposed.IsFailure)
        {
            return Result.Failure<CommunityMembershipResponse>(proposed.Error);
        }

        var community = proposed.Value;
        community.Describe(request.Tagline, request.Description);

        // Open joining, deliberately. A community that required approval would collect requests
        // that nobody can act on yet — approving a join is a route this increment does not build —
        // and a queue that never drains reads to the people in it as being ignored.
        community.SetJoinPolicy(requiresApproval: false);

        var founded = CommunityMembership.Found(
            CommunityMembershipId.New(), community, caller.Value, DateTimeOffset.UtcNow);

        if (founded.IsFailure)
        {
            return Result.Failure<CommunityMembershipResponse>(founded.Error);
        }

        // The founder is member one. Counted through the same path as everybody after them, so the
        // count and the milestones cannot disagree about where it started.
        community.RecordMemberJoined();

        _communities.Add(community);
        _memberships.Add(founded.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            CommunityMembershipMapper.ToResponse(community, founded.Value, _access.IsStaff));
    }
}

/// <summary>Give a member a role in a community, or take one away.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Participate)]
public sealed record ChangeCommunityRoleCommand : ICommand<CommunityMemberResponse>
{
    public required string IdOrSlug { get; init; }

    public required Guid UserId { get; init; }

    public required ChangeCommunityRoleRequest Request { get; init; }
}

public sealed class ChangeCommunityRoleCommandHandler
    : ICommandHandler<ChangeCommunityRoleCommand, CommunityMemberResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeCommunityRoleCommandHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _memberships = memberships;
        _access = access;
        _profiles = profiles;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityMemberResponse>> Handle(
        ChangeCommunityRoleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityMemberResponse>(caller.Error);
        }

        var community = await _communities.FindByIdOrSlugAsync(command.IdOrSlug, cancellationToken);

        if (community is null)
        {
            return Result.Failure<CommunityMemberResponse>(CommunityErrors.NotFound);
        }

        var callerMembership = await _memberships.FindAsync(
            community.Id, caller.Value, cancellationToken);

        // The community's own decision, so no staff bypass. A platform moderator reviewing reports
        // in a community they do not belong to has no business deciding who runs it.
        var capabilities = CommunityCapabilitySet.For(community, callerMembership);

        if (!capabilities.CanManageRoles)
        {
            // Absent rather than forbidden where the caller cannot see the community at all, the
            // rule GetCommunity states: a refusal would confirm a private community exists.
            return Result.Failure<CommunityMemberResponse>(
                callerMembership is null && community.Visibility is not
                    (CommunityVisibility.Public or CommunityVisibility.Unlisted)
                    ? CommunityErrors.NotFound
                    : CommunityErrors.CannotManageRoles);
        }

        var role = ParseRole(command.Request.Role);

        if (role.IsFailure)
        {
            return Result.Failure<CommunityMemberResponse>(role.Error);
        }

        if (role.Value == CommunityMembershipRole.Administrator
            && !capabilities.CanAppointAdministrators)
        {
            return Result.Failure<CommunityMemberResponse>(
                CommunityErrors.OnlyOwnerAppointsAdministrators);
        }

        var target = await _memberships.FindAsync(
            community.Id, UserId.From(command.UserId), cancellationToken);

        if (target is null || !target.IsCurrentMember)
        {
            return Result.Failure<CommunityMemberResponse>(CommunityErrors.TargetNotAMember);
        }

        var changed = command.Request.Granted
            ? target.Grant(role.Value)
            : target.RevokeRole(role.Value);

        if (changed.IsFailure)
        {
            return Result.Failure<CommunityMemberResponse>(changed.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var names = await _profiles.ResolveAsync([target.MemberId], cancellationToken);

        return Result.Success(
            CommunityAdministrationMapper.ToMember(target, names, caller.Value));
    }

    /// <summary>
    /// The three roles this route hands out. Owner is refused by name so the member reads why.
    /// </summary>
    private static Result<CommunityMembershipRole> ParseRole(string? value)
    {
        if (!Enum.TryParse<CommunityMembershipRole>(value, ignoreCase: true, out var role)
            || !Enum.IsDefined(role))
        {
            return Result.Failure<CommunityMembershipRole>(CommunityErrors.UnknownRole);
        }

        return role switch
        {
            CommunityMembershipRole.Owner =>
                Result.Failure<CommunityMembershipRole>(CommunityErrors.OwnerRoleNotAssignable),

            // Implied by belonging and never stored. Granting it would do nothing and say it had.
            CommunityMembershipRole.Member =>
                Result.Failure<CommunityMembershipRole>(CommunityErrors.UnknownRole),

            _ => Result.Success(role)
        };
    }
}

/// <summary>The members of a community the caller may see.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record ListCommunityMembersQuery : IQuery<CommunityPageResponse<CommunityMemberResponse>>
{
    public required string IdOrSlug { get; init; }

    public string? Cursor { get; init; }

    public int Limit { get; init; } = 25;

    /// <summary>
    /// 21 Oct 2026: only members holding this role — Moderator, Ambassador, Administrator or Owner.
    /// Member, or none, is everybody.
    /// </summary>
    public string? Role { get; init; }
}

public sealed class ListCommunityMembersQueryHandler
    : IQueryHandler<ListCommunityMembersQuery, CommunityPageResponse<CommunityMemberResponse>>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;

    public ListCommunityMembersQueryHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles)
    {
        _communities = communities;
        _memberships = memberships;
        _access = access;
        _profiles = profiles;
    }

    public async Task<Result<CommunityPageResponse<CommunityMemberResponse>>> Handle(
        ListCommunityMembersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityPageResponse<CommunityMemberResponse>>(caller.Error);
        }

        var role = CommunityRoleFilter.Parse(query.Role);

        if (role.IsFailure)
        {
            return Result.Failure<CommunityPageResponse<CommunityMemberResponse>>(role.Error);
        }

        var community = await _communities.FindByIdOrSlugAsync(query.IdOrSlug, cancellationToken);

        if (community is null)
        {
            return Result.Failure<CommunityPageResponse<CommunityMemberResponse>>(
                CommunityErrors.NotFound);
        }

        var callerMembership = await _memberships.FindAsync(
            community.Id, caller.Value, cancellationToken);

        // Members only, and no staff bypass — `CommunityCapabilitySet` draws that line for the
        // member roll specifically, and this is the member roll.
        if (!CommunityCapabilitySet.For(community, callerMembership).CanViewMembers)
        {
            return Result.Failure<CommunityPageResponse<CommunityMemberResponse>>(
                CommunityErrors.NotAMember);
        }

        var page = await _memberships.ListVisibleMembersAsync(
            community.Id, caller.Value, query.Cursor, query.Limit, role.Value, cancellationToken);

        // One call for the whole page, never one per row.
        var names = await _profiles.ResolveAsync(
            [.. page.Items.Select(membership => membership.MemberId)], cancellationToken);

        return Result.Success(new CommunityPageResponse<CommunityMemberResponse>
        {
            Items = [.. page.Items.Select(membership =>
                CommunityAdministrationMapper.ToMember(membership, names, caller.Value))],
            NextCursor = page.NextCursor,
            HasMore = page.HasMore
        });
    }
}

/// <summary>Open a pending community into the directory. Platform administrators.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record OpenCommunityCommand : ICommand<CommunityResponse>
{
    public required string IdOrSlug { get; init; }
}

public sealed class OpenCommunityCommandHandler : ICommandHandler<OpenCommunityCommand, CommunityResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IUnitOfWork _unitOfWork;

    public OpenCommunityCommandHandler(
        ICommunityRepository communities,
        CommunityProgrammeAccessService access,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _access = access;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityResponse>> Handle(
        OpenCommunityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_access.IsPlatformAdministrator)
        {
            return Result.Failure<CommunityResponse>(CommunityErrors.PlatformAdministratorsOnly);
        }

        var community = await _communities.FindByIdOrSlugAsync(command.IdOrSlug, cancellationToken);

        if (community is null)
        {
            return Result.Failure<CommunityResponse>(CommunityErrors.NotFound);
        }

        var opened = community.Open();

        if (opened.IsFailure)
        {
            return Result.Failure<CommunityResponse>(opened.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(CommunityMapper.ToResponse(community));
    }
}

/// <summary>Make a member the owner of a community that has none. Platform administrators.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record AppointCommunityOwnerCommand : ICommand<CommunityMemberResponse>
{
    public required string IdOrSlug { get; init; }

    public required AppointCommunityOwnerRequest Request { get; init; }
}

public sealed class AppointCommunityOwnerCommandHandler
    : ICommandHandler<AppointCommunityOwnerCommand, CommunityMemberResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;
    private readonly IUnitOfWork _unitOfWork;

    public AppointCommunityOwnerCommandHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _memberships = memberships;
        _access = access;
        _profiles = profiles;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityMemberResponse>> Handle(
        AppointCommunityOwnerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_access.IsPlatformAdministrator || _access.CallerId is not { } caller)
        {
            return Result.Failure<CommunityMemberResponse>(CommunityErrors.PlatformAdministratorsOnly);
        }

        var community = await _communities.FindByIdOrSlugAsync(command.IdOrSlug, cancellationToken);

        if (community is null)
        {
            return Result.Failure<CommunityMemberResponse>(CommunityErrors.NotFound);
        }

        // One owner. Replacing one is a transfer, and a transfer is not what this route is for —
        // it exists so the curated communities, which nobody founded, can have anybody at all.
        if (await _memberships.FindOwnerAsync(community.Id, cancellationToken) is not null)
        {
            return Result.Failure<CommunityMemberResponse>(CommunityErrors.AlreadyHasOwner);
        }

        var target = await _memberships.FindAsync(
            community.Id, UserId.From(command.Request.UserId), cancellationToken);

        if (target is null)
        {
            return Result.Failure<CommunityMemberResponse>(CommunityErrors.TargetNotAMember);
        }

        var appointed = target.AppointOwner();

        if (appointed.IsFailure)
        {
            return Result.Failure<CommunityMemberResponse>(appointed.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var names = await _profiles.ResolveAsync([target.MemberId], cancellationToken);

        return Result.Success(CommunityAdministrationMapper.ToMember(target, names, caller));
    }
}

/// <summary>The one projection from a membership to a member row.</summary>
internal static class CommunityAdministrationMapper
{
    public static CommunityMemberResponse ToMember(
        CommunityMembership membership,
        IReadOnlyDictionary<Guid, MemberProfile> names,
        UserId caller) => new()
    {
        UserId = membership.MemberId.Value,
        DisplayName = names.TryGetValue(membership.MemberId.Value, out var profile)
            ? profile.DisplayName
            : null,
        Roles = [.. membership.Roles],
        Visibility = membership.Visibility.ToString(),
        JoinedAt = membership.JoinedAt,
        IsYou = membership.MemberId == caller
    };
}
