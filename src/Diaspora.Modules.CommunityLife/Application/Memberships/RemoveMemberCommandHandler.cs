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
/// Removes somebody from an organization.
/// <para>
/// <b>Nobody may remove somebody who outranks them</b>, for the same reason nobody may demote them:
/// otherwise a moderator takes the body by removing its owner. The aggregate refuses to remove an
/// owner at all.
/// </para>
/// <para>
/// <b>A removal is not a departure and the two are kept apart.</b> Only the organization can undo a
/// removal — the membership evaluator refuses a re-application outright — whereas somebody who left
/// can come back on their own. Collapsing them would either trap people who resigned or let removed
/// people walk back in.
/// </para>
/// </summary>
public sealed class RemoveMemberCommandHandler
    : ICommandHandler<RemoveMemberCommand, MembershipResponse>
{
    private readonly IOrganizationMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public RemoveMemberCommandHandler(
        IOrganizationMembershipRepository memberships,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _memberships = memberships;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<MembershipResponse>> Handle(
        RemoveMemberCommand command, CancellationToken cancellationToken)
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
            membership.OrganizationId, MembershipRole.Moderator, cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<MembershipResponse>(caller.Error);
        }

        // Staff are held to this too: a member of staff who belongs to the body is a member with a
        // role there, and one who does not was refused above.
        var callerMembership = await _authorization.FindMembershipAsync(
            membership.OrganizationId, cancellationToken);

        if (callerMembership is null || membership.Role > callerMembership.Role)
        {
            return Result.Failure<MembershipResponse>(CommunityLifeErrors.RoleNotAssignable);
        }

        var removed = membership.Remove(
            _guids.NewSequentialGuid(),
            _clock.UtcNow,
            caller.Value,
            command.Request.Reason);

        if (removed.IsFailure)
        {
            return Result.Failure<MembershipResponse>(removed.Error);
        }

        _memberships.Update(membership);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(OrganizationMapper.ToResponse(membership));
    }
}
