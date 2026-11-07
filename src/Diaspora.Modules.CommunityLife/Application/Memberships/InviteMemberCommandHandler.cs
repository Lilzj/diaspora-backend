using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Memberships;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Memberships;

/// <summary>
/// Invites somebody into an organization.
/// <para>
/// <b>Nobody can invite somebody to a role above their own.</b> A moderator who could invite an
/// owner would be able to hand the body to anybody, including themselves through a second account —
/// which is the failure mode of every role system that checks only "can this person invite".
/// </para>
/// <para>
/// <b>An invitation lands pending, never active.</b> Membership of a church, a political association
/// or a support group is something a person accepts; a body that could enrol somebody without asking
/// would be publishing an affiliation they never chose.
/// </para>
/// </summary>
public sealed class InviteMemberCommandHandler
    : ICommandHandler<InviteMemberCommand, MembershipResponse>
{
    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public InviteMemberCommandHandler(
        IOrganizationRepository organizations,
        IOrganizationMembershipRepository memberships,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _organizations = organizations;
        _memberships = memberships;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<MembershipResponse>> Handle(
        InviteMemberCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var organizationId = OrganizationId.From(command.OrganizationId);

        var organization = await _organizations.GetAsync(organizationId, cancellationToken);

        if (organization is null)
        {
            return Result.Failure<MembershipResponse>(CommunityLifeErrors.OrganizationNotFound);
        }

        var caller = await _authorization.RequireRoleAsync(
            organizationId, MembershipRole.Moderator, cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<MembershipResponse>(caller.Error);
        }

        var role = CommunityLifeSharedMapper.ParseEnum<MembershipRole>(command.Request.Role)
            ?? MembershipRole.Member;

        var grantable = await CanGrantAsync(
            organizationId, caller.Value, role, cancellationToken);

        if (grantable.IsFailure)
        {
            return Result.Failure<MembershipResponse>(grantable.Error);
        }

        var invitee = UserId.From(command.Request.MemberUserId);

        var existing = await _memberships.FindAsync(
            organizationId, invitee, cancellationToken);

        if (existing?.IsCurrent == true)
        {
            return Result.Failure<MembershipResponse>(CommunityLifeErrors.AlreadyMember);
        }

        var now = _clock.UtcNow;

        var membership = existing is null
            ? OrganizationMembership.Open(
                OrganizationMembershipId.From(_guids.NewSequentialGuid()),
                organizationId,
                invitee,
                MembershipStatus.Invited,
                role,
                _guids.NewSequentialGuid(),
                now,
                command.Request.Message,
                caller.Value,
                organization.MembershipPolicy.RenewalMonths)
            : Reinvite(existing, role, caller.Value, now);

        if (membership.IsFailure)
        {
            return Result.Failure<MembershipResponse>(membership.Error);
        }

        if (existing is null)
        {
            await _memberships.AddAsync(membership.Value, cancellationToken);
        }
        else
        {
            _memberships.Update(membership.Value);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(OrganizationMapper.ToResponse(membership.Value));
    }

    /// <summary>
    /// Whether the caller may grant this role.
    /// <para>
    /// Everybody is capped at their own role, which is what stops a moderator manufacturing an
    /// owner — staff included: being staff grants no role in somebody's organization. See the class
    /// summary.
    /// </para>
    /// </summary>
    private async Task<Result> CanGrantAsync(
        OrganizationId organizationId,
        UserId caller,
        MembershipRole role,
        CancellationToken cancellationToken)
    {
        var membership = await _memberships.FindCurrentAsync(
            organizationId, caller, cancellationToken);

        return membership is not null && role <= membership.Role
            ? Result.Success()
            : Result.Failure(CommunityLifeErrors.RoleNotAssignable);
    }

    /// <summary>
    /// Re-invites somebody whose membership has ended.
    /// <para>
    /// Through the ordinary transition so the history records both the departure and the return.
    /// A removal cannot be re-invited around: the aggregate's machine refuses it.
    /// </para>
    /// </summary>
    private Result<OrganizationMembership> Reinvite(
        OrganizationMembership existing,
        MembershipRole role,
        UserId caller,
        DateTimeOffset now)
    {
        var moved = existing.MoveTo(
            MembershipStatus.Invited, _guids.NewSequentialGuid(), now, caller);

        if (moved.IsFailure)
        {
            return Result.Failure<OrganizationMembership>(moved.Error);
        }

        // The role travels with the invitation, so somebody invited back as a volunteer does not
        // silently return with the role they had when they left.
        var changed = existing.ChangeRole(role, now, caller);

        return changed.IsFailure
            ? Result.Failure<OrganizationMembership>(changed.Error)
            : Result.Success(existing);
    }
}
