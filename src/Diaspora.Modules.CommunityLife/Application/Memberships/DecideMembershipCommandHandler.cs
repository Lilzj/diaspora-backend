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
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Memberships;

/// <summary>
/// Approves or refuses a pending membership.
/// <para>
/// <b>One endpoint serves both directions, and who may call it depends on which way the membership
/// is pointing.</b> A <em>request</em> is the organization's to decide, so an administrator answers
/// it. An <em>invitation</em> is the member's, so only they may accept or decline it. Splitting
/// these into four endpoints would make a client track which one applies; conflating the permissions
/// would let an administrator accept an invitation on somebody's behalf, which is precisely the
/// affiliation nobody should be able to publish for another person.
/// </para>
/// </summary>
public sealed class DecideMembershipCommandHandler
    : ICommandHandler<DecideMembershipCommand, MembershipResponse>
{
    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationMembershipRepository _memberships;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public DecideMembershipCommandHandler(
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
        DecideMembershipCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<MembershipResponse>(caller.Error);
        }

        var membership = await _memberships.GetAsync(
            OrganizationMembershipId.From(command.MembershipId), cancellationToken);

        if (membership is null
            || membership.OrganizationId.Value != command.OrganizationId)
        {
            return Result.Failure<MembershipResponse>(CommunityLifeErrors.MembershipNotFound);
        }

        if (!membership.IsPending)
        {
            return Result.Failure<MembershipResponse>(CommunityLifeErrors.MembershipNotPending);
        }

        var permitted = await MayDecideAsync(membership, cancellationToken);

        if (permitted.IsFailure)
        {
            return Result.Failure<MembershipResponse>(permitted.Error);
        }

        var now = _clock.UtcNow;

        var decided = command.Request.Approve
            ? membership.Approve(
                _guids.NewSequentialGuid(),
                now,
                caller.Value,
                await RenewalMonthsAsync(membership, cancellationToken))
            : membership.Reject(
                _guids.NewSequentialGuid(), now, caller.Value, command.Request.Reason);

        if (decided.IsFailure)
        {
            return Result.Failure<MembershipResponse>(decided.Error);
        }

        _memberships.Update(membership);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(OrganizationMapper.ToResponse(membership));
    }

    /// <summary>
    /// Whether the caller is the right person to answer this.
    /// <para>
    /// An invitation belongs to the person invited. A request belongs to the organization. See the
    /// class summary for why these are not interchangeable.
    /// </para>
    /// </summary>
    private async Task<Result> MayDecideAsync(
        OrganizationMembership membership, CancellationToken cancellationToken)
    {
        if (membership.Status == MembershipStatus.Invited)
        {
            return membership.MemberUserId == _authorization.CallerId
                ? Result.Success()
                : Result.Failure(CommunityLifeErrors.NotOrganizationMember);
        }

        var role = await _authorization.RequireRoleAsync(
            membership.OrganizationId, MembershipRole.Moderator, cancellationToken);

        return role.IsSuccess ? Result.Success() : Result.Failure(role.Error);
    }

    /// <summary>
    /// The renewal window the body sets for its members, applied at the moment of admission.
    /// <para>
    /// Read from the organization rather than carried on the membership, because a body that
    /// changes its renewal period means it for everybody admitted from then on.
    /// </para>
    /// </summary>
    private async Task<int?> RenewalMonthsAsync(
        OrganizationMembership membership, CancellationToken cancellationToken)
    {
        var organization = await _organizations.GetAsync(
            membership.OrganizationId, cancellationToken);

        return organization?.MembershipPolicy.RenewalMonths;
    }
}
