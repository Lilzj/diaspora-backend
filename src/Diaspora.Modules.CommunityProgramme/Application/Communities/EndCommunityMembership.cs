using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Application.Communities;

/// <summary>
/// FRONTEND-PHASE13-GAP-004, the other half — the member stepping back.
/// <para>
/// <b>Leaving is the member's own decision and the row says so.</b> <c>Left</c> carries no reason
/// and no decider, which is what keeps it distinguishable from <c>Revoked</c> on the screen that
/// renders both. Collapsing them would tell somebody who quietly left that they were removed.
/// </para>
/// <para>
/// <b>The row is not deleted.</b> Leaving and coming back keeps one row, so a community can tell a
/// returning member from a new one — and so a revocation cannot be escaped by leaving first.
/// </para>
/// </summary>
[Authorize(Permission = CommunityProgrammePermissions.Participate)]
public sealed record EndCommunityMembershipCommand : ICommand<CommunityMembershipResponse>
{
    public required string IdOrSlug { get; init; }

    /// <summary><c>leave</c>, <c>unfollow</c> or <c>remove-interest</c>.</summary>
    public string? Action { get; init; }
}

public sealed class EndCommunityMembershipCommandHandler
    : ICommandHandler<EndCommunityMembershipCommand, CommunityMembershipResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IUnitOfWork _unitOfWork;

    public EndCommunityMembershipCommandHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        CommunityProgrammeAccessService access,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _memberships = memberships;
        _access = access;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityMembershipResponse>> Handle(
        EndCommunityMembershipCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityMembershipResponse>(caller.Error);
        }

        var community = await _communities.FindByIdOrSlugAsync(command.IdOrSlug, cancellationToken);

        if (community is null)
        {
            return Result.Failure<CommunityMembershipResponse>(CommunityErrors.NotFound);
        }

        var membership = await _memberships.FindAsync(
            community.Id, caller.Value, cancellationToken);

        // Nothing to end. A 404 rather than a silent success, and that is the platform's existing
        // convention: unfollowing something you do not follow is a 404 on `/community/follows` too,
        // and the client is already shaped around it.
        if (membership is null)
        {
            return Result.Failure<CommunityMembershipResponse>(CommunityErrors.NotAMember);
        }

        var wasMember = membership.IsCurrentMember;

        var applied = command.Action?.Trim().ToLowerInvariant() switch
        {
            "leave" or null or "" => membership.Leave(DateTimeOffset.UtcNow),
            "unfollow" or "remove-interest" => membership.Unfollow(),
            _ => Result.Failure(CommunityErrors.UnknownMembershipAction)
        };

        if (applied.IsFailure)
        {
            return Result.Failure<CommunityMembershipResponse>(applied.Error);
        }

        // Only a real departure moves the count. Unfollowing was never membership.
        if (wasMember && !membership.IsCurrentMember)
        {
            community.RecordMemberLeft();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            CommunityMembershipMapper.ToResponse(community, membership, _access.IsStaff));
    }
}
