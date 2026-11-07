using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Memberships;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Memberships;

/// <summary>
/// Promotes or stands down a member.
/// <para>
/// <b>Nobody may grant a role above their own, and nobody may move somebody who outranks them.</b>
/// The first stops a moderator manufacturing an owner; the second stops them demoting the owner and
/// taking the body. Both are checked here rather than in the aggregate, which does not know who is
/// asking.
/// </para>
/// <para>
/// <b>The last owner cannot be stood down.</b> An organization with no owner is one nobody can
/// publish, edit or hand on — the transfer has to name the new owner first, which is why this
/// refuses rather than silently promoting somebody.
/// </para>
/// </summary>
public sealed class ChangeMemberRoleCommandHandler
    : ICommandHandler<ChangeMemberRoleCommand, MembershipResponse>
{
    private readonly IOrganizationMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly IClock _clock;

    public ChangeMemberRoleCommandHandler(
        IOrganizationMembershipRepository memberships,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        IClock clock)
    {
        _memberships = memberships;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<MembershipResponse>> Handle(
        ChangeMemberRoleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var membership = await _memberships.GetAsync(
            OrganizationMembershipId.From(command.MembershipId), cancellationToken);

        if (membership is null
            || membership.OrganizationId.Value != command.OrganizationId)
        {
            return Result.Failure<MembershipResponse>(CommunityLifeErrors.MembershipNotFound);
        }

        var caller = await _authorization.RequireRoleAsync(
            membership.OrganizationId, MembershipRole.Administrator, cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<MembershipResponse>(caller.Error);
        }

        if (CommunityLifeSharedMapper.ParseEnum<MembershipRole>(command.Request.Role)
            is not { } role)
        {
            return Result.Failure<MembershipResponse>(CommunityLifeErrors.RoleNotAssignable);
        }

        var permitted = await MayGrantAsync(membership, role, cancellationToken);

        if (permitted.IsFailure)
        {
            return Result.Failure<MembershipResponse>(permitted.Error);
        }

        var lastOwner = await WouldLeaveNoOwnerAsync(membership, role, cancellationToken);

        if (lastOwner)
        {
            return Result.Failure<MembershipResponse>(CommunityLifeErrors.LastOwner);
        }

        var changed = membership.ChangeRole(role, _clock.UtcNow, caller.Value);

        if (changed.IsFailure)
        {
            return Result.Failure<MembershipResponse>(changed.Error);
        }

        _memberships.Update(membership);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(OrganizationMapper.ToResponse(membership));
    }

    /// <summary>
    /// The two comparisons against the caller's own standing. See the class summary. Staff are
    /// held to them like anybody else: being staff grants no role in somebody's organization.
    /// </summary>
    private async Task<Result> MayGrantAsync(
        OrganizationMembership membership,
        MembershipRole role,
        CancellationToken cancellationToken)
    {
        var callerMembership = await _authorization.FindMembershipAsync(
            membership.OrganizationId, cancellationToken);

        if (callerMembership is null)
        {
            return Result.Failure(CommunityLifeErrors.NotOrganizationMember);
        }

        return role <= callerMembership.Role && membership.Role <= callerMembership.Role
            ? Result.Success()
            : Result.Failure(CommunityLifeErrors.RoleNotAssignable);
    }

    /// <summary>
    /// Whether standing this person down would leave the body without an owner.
    /// <para>
    /// Only asked when an owner is being moved, so the extra count costs nothing on the ordinary
    /// promotion.
    /// </para>
    /// </summary>
    private async Task<bool> WouldLeaveNoOwnerAsync(
        OrganizationMembership membership,
        MembershipRole role,
        CancellationToken cancellationToken)
    {
        if (membership.Role != MembershipRole.Owner || role == MembershipRole.Owner)
        {
            return false;
        }

        var owners = await _memberships.CountByRoleAsync(
            membership.OrganizationId, MembershipRole.Owner, cancellationToken);

        return owners <= 1;
    }
}
