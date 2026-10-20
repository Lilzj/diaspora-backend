using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.Modules.CommunityProgramme.Application.Communities;

/// <summary>
/// FRONTEND-PHASE13-GAP-004 — the member's own acts on their own standing.
/// <para>
/// <b>One route with the act as data, rather than six routes.</b> The client's own migration note
/// asked for it: "the mutation hooks already take the action as data rather than as four separate
/// functions, so a backend that exposes one route or six needs no screen change". Six routes would
/// also mean six places to forget the community's state, the invitation rule and the revocation.
/// </para>
/// <para>
/// <b>Nothing here is an administrative act.</b> Approving, declining and revoking are the
/// community's decisions about somebody else and are not reachable from this command at all — the
/// subject is always the bearer token, never a path parameter.
/// </para>
/// </summary>
[Authorize(Permission = CommunityProgrammePermissions.Participate)]
public sealed record ChangeCommunityMembershipCommand : ICommand<CommunityMembershipResponse>
{
    public required string IdOrSlug { get; init; }

    /// <summary><c>join</c>, <c>follow</c> or <c>express-interest</c>.</summary>
    public string? Action { get; init; }
}

public sealed class ChangeCommunityMembershipCommandHandler
    : ICommandHandler<ChangeCommunityMembershipCommand, CommunityMembershipResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityMemberPreferencesRepository _preferences;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeCommunityMembershipCommandHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityMemberPreferencesRepository preferences,
        CommunityProgrammeAccessService access,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _memberships = memberships;
        _preferences = preferences;
        _access = access;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityMembershipResponse>> Handle(
        ChangeCommunityMembershipCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityMembershipResponse>(caller.Error);
        }

        var community = await _communities.FindByIdOrSlugAsync(command.IdOrSlug, cancellationToken);

        // A community the caller may not read is one they may not join, and it answers the same
        // way — see GetCommunityQueryHandler for the rule and why it is one rule.
        if (community is null || !await CanSeeAsync(community, caller.Value, cancellationToken))
        {
            return Result.Failure<CommunityMembershipResponse>(CommunityErrors.NotFound);
        }

        var membership = await _memberships.FindAsync(
            community.Id, caller.Value, cancellationToken);

        // 21 Oct 2026. Where a brand-new relationship starts: the member's own default, or Private
        // when they never chose one. Read only when there is no row yet, because an existing row
        // already carries the member's choice for this community and must not be overwritten.
        var startingVisibility = membership is null
            ? (await _preferences.FindAsync(caller.Value, cancellationToken))?.DefaultMembershipVisibility
                ?? CommunityMembershipVisibility.Private
            : CommunityMembershipVisibility.Private;

        var applied = command.Action?.Trim().ToLowerInvariant() switch
        {
            "join" or null or "" => await JoinAsync(
                community, membership, caller.Value, startingVisibility),
            "follow" => Relate(
                community, membership, caller.Value, CommunityMembershipState.Following,
                startingVisibility),
            "express-interest" => Relate(
                community, membership, caller.Value, CommunityMembershipState.Interested,
                startingVisibility),
            _ => Result.Failure<CommunityMembership>(CommunityErrors.UnknownMembershipAction)
        };

        if (applied.IsFailure)
        {
            return Result.Failure<CommunityMembershipResponse>(applied.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            CommunityMembershipMapper.ToResponse(community, applied.Value, _access.IsStaff));
    }

    /// <summary>
    /// Joining, which is the only act that reads the community's own rules.
    /// <para>
    /// Whether it is open, whether an invitation is needed and whether approval is are all decided
    /// by the aggregate rather than here, so the same three questions cannot be asked differently by
    /// a second caller.
    /// </para>
    /// </summary>
    private async Task<Result<CommunityMembership>> JoinAsync(
        CommunityAggregate community,
        CommunityMembership? existing,
        UserId caller,
        CommunityMembershipVisibility startingVisibility)
    {
        var membership = existing
            ?? Start(community, caller, CommunityMembershipState.Discovering, startingVisibility);

        var wasMember = membership.IsCurrentMember;

        var joined = membership.Join(
            community, community.RequiresApproval, DateTimeOffset.UtcNow);

        if (joined.IsFailure)
        {
            return Result.Failure<CommunityMembership>(joined.Error);
        }

        // The count moves only on a real transition into membership. An approval-gated join leaves
        // the caller waiting and the community the size it was.
        if (!wasMember && membership.IsCurrentMember)
        {
            community.RecordMemberJoined();
        }

        if (existing is null)
        {
            _memberships.Add(membership);
        }

        return Result.Success(membership);
    }

    /// <summary>
    /// Following, and saying you are interested. Neither is membership and neither touches the count.
    /// <para>
    /// <b>A current member who follows is left alone rather than demoted.</b> The client sends the
    /// act, not the target state, and a member tapping Follow means "keep me posted" — turning their
    /// membership into a follow would silently remove them from their own community.
    /// </para>
    /// <para>
    /// A revoked member may not follow their way back in either: the row records what happened, and
    /// stepping from Revoked to Following would launder it.
    /// </para>
    /// </summary>
    private Result<CommunityMembership> Relate(
        CommunityAggregate community,
        CommunityMembership? existing,
        UserId caller,
        CommunityMembershipState state,
        CommunityMembershipVisibility startingVisibility)
    {
        if (existing is null)
        {
            var started = Start(community, caller, state, startingVisibility);
            _memberships.Add(started);
            return Result.Success(started);
        }

        if (existing.State == CommunityMembershipState.Revoked)
        {
            return Result.Failure<CommunityMembership>(CommunityErrors.MembershipRevoked);
        }

        // Already in, at a standing that outranks this. Nothing to do, and saying so as a success
        // keeps the client's optimistic path from rolling back a state that is correct.
        if (existing.IsCurrentMember || existing.State == CommunityMembershipState.PendingApproval)
        {
            return Result.Success(existing);
        }

        var restarted = existing.Restart(state);

        return restarted.IsFailure
            ? Result.Failure<CommunityMembership>(restarted.Error)
            : Result.Success(existing);
    }

    private static CommunityMembership Start(
        CommunityAggregate community,
        UserId caller,
        CommunityMembershipState state,
        CommunityMembershipVisibility visibility)
    {
        var membership = CommunityMembership.Start(
            CommunityMembershipId.New(), community.Id, caller, state).Value;

        membership.SetVisibility(visibility);
        return membership;
    }

    /// <summary>The read rule, applied to a write. See <see cref="GetCommunityQueryHandler"/>.</summary>
    private async Task<bool> CanSeeAsync(
        CommunityAggregate community, UserId caller, CancellationToken cancellationToken)
    {
        if (_access.IsStaff
            || community.Visibility is CommunityVisibility.Public or CommunityVisibility.Unlisted)
        {
            return community.Status != CommunityStatus.Pending || _access.IsStaff;
        }

        return await _memberships.FindAsync(community.Id, caller, cancellationToken) is not null;
    }
}
