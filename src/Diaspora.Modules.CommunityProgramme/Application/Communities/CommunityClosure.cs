using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Requests;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Modules.CommunityProgramme.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;
using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.Modules.CommunityProgramme.Application.Communities;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Closing a community, and handing one over — 11 Sep 2026.
//
// **The decision this implements**, from the product owner: an owner may ask for their community to
// be deleted; it takes thirty days, everything in the community carries on as normal in the
// meantime, they have to say why, and Diaspora reviews it. If Diaspora decides not to delete it, the
// owner is emailed the decision and the reasons, and asked whether they would rather step down and
// give the community to one of its moderators — who is then asked, in the app and by email, to
// accept or refuse. "Because a community might have grown so large that you cannot just delete them."
//
// **Four separate acts, and the separation is the design.**
//   - Asking is not deleting. A request performs nothing; the community is untouched while it runs.
//   - Deciding is not deleting either. An approved request still waits out the thirty days.
//   - The wait running out is what closes the community, and only for a request somebody approved —
//     a request nobody reviewed is never carried out. Deletion because nobody looked is not review.
//   - Handing the community over is its own act, available whether or not a deletion was ever asked
//     for. It is also the only way out of owning one, since an owner cannot leave.
//
// **What "closed" means here.** The community is archived: it leaves the directory, nobody joins,
// nobody posts, and what people wrote stays readable to them. Nothing is erased. A community is
// never hard-deleted on this platform — `CommunityStatus` says so — and this does not change that.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>An owner asking Diaspora to close their community.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Participate)]
public sealed record RequestCommunityDeletionCommand : ICommand<CommunityDeletionRequestResponse>
{
    public required string IdOrSlug { get; init; }

    public required RequestCommunityDeletionRequest Request { get; init; }
}

public sealed class RequestCommunityDeletionCommandHandler
    : ICommandHandler<RequestCommunityDeletionCommand, CommunityDeletionRequestResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityDeletionRequestRepository _requests;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;
    private readonly IOptions<CommunityProgrammeOptions> _options;
    private readonly IUnitOfWork _unitOfWork;

    public RequestCommunityDeletionCommandHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityDeletionRequestRepository requests,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles,
        IOptions<CommunityProgrammeOptions> options,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _memberships = memberships;
        _requests = requests;
        _access = access;
        _profiles = profiles;
        _options = options;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityDeletionRequestResponse>> Handle(
        RequestCommunityDeletionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityDeletionRequestResponse>(caller.Error);
        }

        var community = await _communities.FindByIdOrSlugAsync(command.IdOrSlug, cancellationToken);

        if (community is null)
        {
            return Result.Failure<CommunityDeletionRequestResponse>(CommunityErrors.NotFound);
        }

        var membership = await _memberships.FindAsync(community.Id, caller.Value, cancellationToken);

        // The community's own decision, so no staff bypass: a Diaspora administrator reviews these,
        // they do not raise them.
        if (!CommunityCapabilitySet.For(community, membership).CanRequestDeletion)
        {
            return Result.Failure<CommunityDeletionRequestResponse>(
                CommunityErrors.OnlyOwnerRequestsDeletion);
        }

        var open = await _requests.FindOpenAsync(community.Id, cancellationToken);

        if (open is not null)
        {
            return Result.Failure<CommunityDeletionRequestResponse>(
                CommunityErrors.DeletionAlreadyRequested);
        }

        var request = CommunityDeletionRequest.Open(
            CommunityDeletionRequestId.New(),
            community,
            caller.Value,
            command.Request.Reason,
            DateTimeOffset.UtcNow,
            _options.Value.CommunityDeletionWaitDays);

        if (request.IsFailure)
        {
            return Result.Failure<CommunityDeletionRequestResponse>(request.Error);
        }

        _requests.Add(request.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            await CommunityClosureMapper.ToResponseAsync(
                request.Value, community, _profiles, cancellationToken));
    }
}

/// <summary>The owner changing their mind before the community closes.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Participate)]
public sealed record WithdrawCommunityDeletionCommand : ICommand<CommunityDeletionRequestResponse>
{
    public required string IdOrSlug { get; init; }
}

public sealed class WithdrawCommunityDeletionCommandHandler
    : ICommandHandler<WithdrawCommunityDeletionCommand, CommunityDeletionRequestResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityDeletionRequestRepository _requests;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;
    private readonly IUnitOfWork _unitOfWork;

    public WithdrawCommunityDeletionCommandHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityDeletionRequestRepository requests,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _memberships = memberships;
        _requests = requests;
        _access = access;
        _profiles = profiles;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityDeletionRequestResponse>> Handle(
        WithdrawCommunityDeletionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityDeletionRequestResponse>(caller.Error);
        }

        var community = await _communities.FindByIdOrSlugAsync(command.IdOrSlug, cancellationToken);

        if (community is null)
        {
            return Result.Failure<CommunityDeletionRequestResponse>(CommunityErrors.NotFound);
        }

        var membership = await _memberships.FindAsync(community.Id, caller.Value, cancellationToken);

        if (!CommunityCapabilitySet.For(community, membership).CanRequestDeletion)
        {
            return Result.Failure<CommunityDeletionRequestResponse>(
                CommunityErrors.OnlyOwnerRequestsDeletion);
        }

        var request = await _requests.FindOpenAsync(community.Id, cancellationToken);

        if (request is null)
        {
            return Result.Failure<CommunityDeletionRequestResponse>(
                CommunityErrors.DeletionNotRequested);
        }

        var withdrawn = request.Withdraw(DateTimeOffset.UtcNow);

        if (withdrawn.IsFailure)
        {
            return Result.Failure<CommunityDeletionRequestResponse>(withdrawn.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            await CommunityClosureMapper.ToResponseAsync(
                request, community, _profiles, cancellationToken));
    }
}

/// <summary>
/// What is happening to this community, if anything. The owner's own banner, and what an
/// administrator reads before deciding.
/// </summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record GetCommunityDeletionRequestQuery : IQuery<CommunityDeletionRequestResponse?>
{
    public required string IdOrSlug { get; init; }
}

public sealed class GetCommunityDeletionRequestQueryHandler
    : IQueryHandler<GetCommunityDeletionRequestQuery, CommunityDeletionRequestResponse?>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityDeletionRequestRepository _requests;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;

    public GetCommunityDeletionRequestQueryHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityDeletionRequestRepository requests,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles)
    {
        _communities = communities;
        _memberships = memberships;
        _requests = requests;
        _access = access;
        _profiles = profiles;
    }

    public async Task<Result<CommunityDeletionRequestResponse?>> Handle(
        GetCommunityDeletionRequestQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityDeletionRequestResponse?>(caller.Error);
        }

        var community = await _communities.FindByIdOrSlugAsync(query.IdOrSlug, cancellationToken);

        if (community is null)
        {
            return Result.Failure<CommunityDeletionRequestResponse?>(CommunityErrors.NotFound);
        }

        var membership = await _memberships.FindAsync(community.Id, caller.Value, cancellationToken);
        var capabilities = CommunityCapabilitySet.For(community, membership);

        // Whoever runs the community, and Diaspora. **Not the members**: while the wait runs the
        // community carries on as normal, and telling everybody it might close would end it early by
        // itself — people leave a place they have been told is closing.
        if (!capabilities.CanManageRoles && !_access.IsPlatformAdministrator)
        {
            return Result.Failure<CommunityDeletionRequestResponse?>(CommunityErrors.NotAMember);
        }

        var request = await _requests.FindOpenAsync(community.Id, cancellationToken)
            ?? await _requests.FindLatestAsync(community.Id, cancellationToken);

        return Result.Success(request is null
            ? null
            : await CommunityClosureMapper.ToResponseAsync(
                request, community, _profiles, cancellationToken));
    }
}

/// <summary>The queue a Diaspora administrator works through.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record ListCommunityDeletionRequestsQuery
    : IQuery<CommunityPageResponse<CommunityDeletionRequestResponse>>
{
    /// <summary>A <see cref="CommunityDeletionStatus"/> name. Absent means everything.</summary>
    public string? Status { get; init; }

    public string? Cursor { get; init; }

    public int Limit { get; init; } = 20;
}

public sealed class ListCommunityDeletionRequestsQueryHandler
    : IQueryHandler<ListCommunityDeletionRequestsQuery,
        CommunityPageResponse<CommunityDeletionRequestResponse>>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityDeletionRequestRepository _requests;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;

    public ListCommunityDeletionRequestsQueryHandler(
        ICommunityRepository communities,
        ICommunityDeletionRequestRepository requests,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles)
    {
        _communities = communities;
        _requests = requests;
        _access = access;
        _profiles = profiles;
    }

    public async Task<Result<CommunityPageResponse<CommunityDeletionRequestResponse>>> Handle(
        ListCommunityDeletionRequestsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_access.IsPlatformAdministrator)
        {
            return Result.Failure<CommunityPageResponse<CommunityDeletionRequestResponse>>(
                CommunityErrors.PlatformAdministratorsOnly);
        }

        CommunityDeletionStatus? status = null;

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<CommunityDeletionStatus>(query.Status, ignoreCase: true, out var parsed)
                || !Enum.IsDefined(parsed))
            {
                return Result.Failure<CommunityPageResponse<CommunityDeletionRequestResponse>>(
                    CommunityErrors.DeletionNotRequested);
            }

            status = parsed;
        }

        var page = await _requests.ListAsync(status, query.Cursor, query.Limit, cancellationToken);

        var items = new List<CommunityDeletionRequestResponse>(page.Items.Count);

        foreach (var request in page.Items)
        {
            // One community read per row. The queue is small by design — a platform with hundreds of
            // communities closing at once has a problem this page is not the answer to.
            var community = await _communities.GetAsync(request.CommunityId, cancellationToken);

            if (community is null)
            {
                continue;
            }

            items.Add(await CommunityClosureMapper.ToResponseAsync(
                request, community, _profiles, cancellationToken));
        }

        return Result.Success(new CommunityPageResponse<CommunityDeletionRequestResponse>
        {
            Items = items,
            NextCursor = page.NextCursor,
            HasMore = page.HasMore
        });
    }
}

/// <summary>A Diaspora administrator deciding. Platform administrators, not moderators.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record DecideCommunityDeletionCommand : ICommand<CommunityDeletionRequestResponse>
{
    public required Guid RequestId { get; init; }

    public required DecideCommunityDeletionRequest Request { get; init; }
}

public sealed class DecideCommunityDeletionCommandHandler
    : ICommandHandler<DecideCommunityDeletionCommand, CommunityDeletionRequestResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityDeletionRequestRepository _requests;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;
    private readonly IUnitOfWork _unitOfWork;

    public DecideCommunityDeletionCommandHandler(
        ICommunityRepository communities,
        ICommunityDeletionRequestRepository requests,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _requests = requests;
        _access = access;
        _profiles = profiles;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityDeletionRequestResponse>> Handle(
        DecideCommunityDeletionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Closing somebody's community is an administrative act, and a moderator reviewing reports
        // should not be able to do it as a side effect — the line `IsPlatformAdministrator` draws.
        if (!_access.IsPlatformAdministrator || _access.CallerId is not { } reviewer)
        {
            return Result.Failure<CommunityDeletionRequestResponse>(
                CommunityErrors.PlatformAdministratorsOnly);
        }

        var request = await _requests.GetAsync(
            CommunityDeletionRequestId.From(command.RequestId), cancellationToken);

        if (request is null)
        {
            return Result.Failure<CommunityDeletionRequestResponse>(
                CommunityErrors.DeletionNotRequested);
        }

        var community = await _communities.GetAsync(request.CommunityId, cancellationToken);

        if (community is null)
        {
            return Result.Failure<CommunityDeletionRequestResponse>(CommunityErrors.NotFound);
        }

        var decided = command.Request.Approve
            ? request.Approve(reviewer, command.Request.Reason, DateTimeOffset.UtcNow, community.Name)
            : request.Reject(reviewer, command.Request.Reason, DateTimeOffset.UtcNow, community.Name);

        if (decided.IsFailure)
        {
            return Result.Failure<CommunityDeletionRequestResponse>(decided.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            await CommunityClosureMapper.ToResponseAsync(
                request, community, _profiles, cancellationToken));
    }
}

/// <summary>An owner offering the community to one of its moderators or administrators.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Participate)]
public sealed record OfferCommunityOwnershipCommand : ICommand<CommunityOwnershipOfferResponse>
{
    public required string IdOrSlug { get; init; }

    public required OfferCommunityOwnershipRequest Request { get; init; }
}

public sealed class OfferCommunityOwnershipCommandHandler
    : ICommandHandler<OfferCommunityOwnershipCommand, CommunityOwnershipOfferResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityOwnershipTransferRepository _transfers;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;
    private readonly IOptions<CommunityProgrammeOptions> _options;
    private readonly IUnitOfWork _unitOfWork;

    public OfferCommunityOwnershipCommandHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityOwnershipTransferRepository transfers,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles,
        IOptions<CommunityProgrammeOptions> options,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _memberships = memberships;
        _transfers = transfers;
        _access = access;
        _profiles = profiles;
        _options = options;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityOwnershipOfferResponse>> Handle(
        OfferCommunityOwnershipCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityOwnershipOfferResponse>(caller.Error);
        }

        var community = await _communities.FindByIdOrSlugAsync(command.IdOrSlug, cancellationToken);

        if (community is null)
        {
            return Result.Failure<CommunityOwnershipOfferResponse>(CommunityErrors.NotFound);
        }

        var callerMembership = await _memberships.FindAsync(
            community.Id, caller.Value, cancellationToken);

        if (!CommunityCapabilitySet.For(community, callerMembership).CanTransferOwnership)
        {
            return Result.Failure<CommunityOwnershipOfferResponse>(
                CommunityErrors.OnlyOwnerOffersOwnership);
        }

        var open = await _transfers.FindOpenAsync(community.Id, cancellationToken);

        if (open is not null)
        {
            return Result.Failure<CommunityOwnershipOfferResponse>(
                CommunityErrors.OwnershipOfferAlreadyOpen);
        }

        var target = await _memberships.FindAsync(
            community.Id, UserId.From(command.Request.UserId), cancellationToken);

        if (target is null || !target.IsCurrentMember)
        {
            return Result.Failure<CommunityOwnershipOfferResponse>(CommunityErrors.TargetNotAMember);
        }

        // Somebody who already helps run the place: a moderator or an administrator. An owner is not
        // free to move a community to a stranger, and §41 means a member who kept their membership
        // private has not agreed to be named as anybody's successor.
        var helps = target.Holds(CommunityMembershipRole.Moderator)
            || target.Holds(CommunityMembershipRole.Administrator);

        if (!helps || target.Visibility == CommunityMembershipVisibility.Private)
        {
            return Result.Failure<CommunityOwnershipOfferResponse>(
                CommunityErrors.OwnershipNeedsHelper);
        }

        var transfer = CommunityOwnershipTransfer.Offer(
            CommunityOwnershipTransferId.New(),
            community,
            caller.Value,
            target.MemberId,
            command.Request.Message,
            DateTimeOffset.UtcNow,
            _options.Value.CommunityOwnershipOfferDays);

        if (transfer.IsFailure)
        {
            return Result.Failure<CommunityOwnershipOfferResponse>(transfer.Error);
        }

        _transfers.Add(transfer.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            await CommunityClosureMapper.ToResponseAsync(
                transfer.Value, community, _profiles, cancellationToken));
    }
}

/// <summary>The owner taking the offer back.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Participate)]
public sealed record WithdrawCommunityOwnershipOfferCommand
    : ICommand<CommunityOwnershipOfferResponse>
{
    public required string IdOrSlug { get; init; }
}

public sealed class WithdrawCommunityOwnershipOfferCommandHandler
    : ICommandHandler<WithdrawCommunityOwnershipOfferCommand, CommunityOwnershipOfferResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityOwnershipTransferRepository _transfers;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;
    private readonly IUnitOfWork _unitOfWork;

    public WithdrawCommunityOwnershipOfferCommandHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityOwnershipTransferRepository transfers,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _memberships = memberships;
        _transfers = transfers;
        _access = access;
        _profiles = profiles;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityOwnershipOfferResponse>> Handle(
        WithdrawCommunityOwnershipOfferCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityOwnershipOfferResponse>(caller.Error);
        }

        var community = await _communities.FindByIdOrSlugAsync(command.IdOrSlug, cancellationToken);

        if (community is null)
        {
            return Result.Failure<CommunityOwnershipOfferResponse>(CommunityErrors.NotFound);
        }

        var callerMembership = await _memberships.FindAsync(
            community.Id, caller.Value, cancellationToken);

        if (!CommunityCapabilitySet.For(community, callerMembership).CanTransferOwnership)
        {
            return Result.Failure<CommunityOwnershipOfferResponse>(
                CommunityErrors.OnlyOwnerOffersOwnership);
        }

        var transfer = await _transfers.FindOpenAsync(community.Id, cancellationToken);

        if (transfer is null)
        {
            return Result.Failure<CommunityOwnershipOfferResponse>(
                CommunityErrors.OwnershipOfferAlreadyAnswered);
        }

        var withdrawn = transfer.Withdraw(DateTimeOffset.UtcNow);

        if (withdrawn.IsFailure)
        {
            return Result.Failure<CommunityOwnershipOfferResponse>(withdrawn.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            await CommunityClosureMapper.ToResponseAsync(
                transfer, community, _profiles, cancellationToken));
    }
}

/// <summary>Every community somebody has been asked to take over.</summary>
[Authorize(Permission = CommunityProgrammePermissions.Participate)]
public sealed record ListMyCommunityOwnershipOffersQuery
    : IQuery<IReadOnlyList<CommunityOwnershipOfferResponse>>;

public sealed class ListMyCommunityOwnershipOffersQueryHandler
    : IQueryHandler<ListMyCommunityOwnershipOffersQuery,
        IReadOnlyList<CommunityOwnershipOfferResponse>>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityOwnershipTransferRepository _transfers;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;

    public ListMyCommunityOwnershipOffersQueryHandler(
        ICommunityRepository communities,
        ICommunityOwnershipTransferRepository transfers,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles)
    {
        _communities = communities;
        _transfers = transfers;
        _access = access;
        _profiles = profiles;
    }

    public async Task<Result<IReadOnlyList<CommunityOwnershipOfferResponse>>> Handle(
        ListMyCommunityOwnershipOffersQuery query, CancellationToken cancellationToken)
    {
        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CommunityOwnershipOfferResponse>>(caller.Error);
        }

        var offers = await _transfers.ListOpenForMemberAsync(caller.Value, cancellationToken);
        var items = new List<CommunityOwnershipOfferResponse>(offers.Count);

        foreach (var offer in offers)
        {
            var community = await _communities.GetAsync(offer.CommunityId, cancellationToken);

            if (community is null)
            {
                continue;
            }

            items.Add(await CommunityClosureMapper.ToResponseAsync(
                offer, community, _profiles, cancellationToken));
        }

        return Result.Success<IReadOnlyList<CommunityOwnershipOfferResponse>>(items);
    }
}

/// <summary>
/// The answer from the person who was offered a community.
/// <para>
/// <b>Accepting is where the roles move</b>, because this is the one place both memberships are in
/// hand: the new owner gains Owner and Administrator, and the old owner keeps Administrator and
/// loses Owner. They keep administering deliberately — the person who founded a community is usually
/// the one who knows how it is run — and, no longer being the owner, they may now leave if they want
/// to, which as owner they could not.
/// </para>
/// </summary>
[Authorize(Permission = CommunityProgrammePermissions.Participate)]
public sealed record DecideCommunityOwnershipCommand : ICommand<CommunityOwnershipOfferResponse>
{
    public required Guid TransferId { get; init; }

    public required DecideCommunityOwnershipRequest Request { get; init; }
}

public sealed class DecideCommunityOwnershipCommandHandler
    : ICommandHandler<DecideCommunityOwnershipCommand, CommunityOwnershipOfferResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly ICommunityOwnershipTransferRepository _transfers;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IMemberProfileService _profiles;
    private readonly IUnitOfWork _unitOfWork;

    public DecideCommunityOwnershipCommandHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        ICommunityOwnershipTransferRepository transfers,
        CommunityProgrammeAccessService access,
        IMemberProfileService profiles,
        IUnitOfWork unitOfWork)
    {
        _communities = communities;
        _memberships = memberships;
        _transfers = transfers;
        _access = access;
        _profiles = profiles;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CommunityOwnershipOfferResponse>> Handle(
        DecideCommunityOwnershipCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityOwnershipOfferResponse>(caller.Error);
        }

        var transfer = await _transfers.GetAsync(
            CommunityOwnershipTransferId.From(command.TransferId), cancellationToken);

        if (transfer is null)
        {
            return Result.Failure<CommunityOwnershipOfferResponse>(
                CommunityErrors.OwnershipOfferAlreadyAnswered);
        }

        // An offer belongs to the person it was made to. Nobody else answers it, staff included.
        if (transfer.OfferedTo != caller.Value)
        {
            return Result.Failure<CommunityOwnershipOfferResponse>(
                CommunityErrors.OwnershipOfferNotYours);
        }

        var community = await _communities.GetAsync(transfer.CommunityId, cancellationToken);

        if (community is null)
        {
            return Result.Failure<CommunityOwnershipOfferResponse>(CommunityErrors.NotFound);
        }

        var now = DateTimeOffset.UtcNow;

        if (!command.Request.Accept)
        {
            var declined = transfer.Decline(now, command.Request.Reason, community.Name);

            if (declined.IsFailure)
            {
                return Result.Failure<CommunityOwnershipOfferResponse>(declined.Error);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(
                await CommunityClosureMapper.ToResponseAsync(
                    transfer, community, _profiles, cancellationToken));
        }

        var successor = await _memberships.FindAsync(
            community.Id, transfer.OfferedTo, cancellationToken);

        if (successor is null || !successor.IsCurrentMember)
        {
            return Result.Failure<CommunityOwnershipOfferResponse>(CommunityErrors.TargetNotAMember);
        }

        // Who owns it *now*, not who owned it when the offer was written. A community that has
        // already changed hands must not change again on the strength of an old message.
        var previous = await _memberships.FindOwnerAsync(community.Id, cancellationToken);

        if (previous is null || previous.MemberId != transfer.OfferedBy)
        {
            return Result.Failure<CommunityOwnershipOfferResponse>(
                CommunityErrors.OwnershipOfferStale);
        }

        var accepted = transfer.Accept(now, community.Name);

        if (accepted.IsFailure)
        {
            return Result.Failure<CommunityOwnershipOfferResponse>(accepted.Error);
        }

        // The old owner first: a moment with two owners is a moment when either could remove the
        // other.
        var stepped = previous.StepDownAsOwner();

        if (stepped.IsFailure)
        {
            return Result.Failure<CommunityOwnershipOfferResponse>(stepped.Error);
        }

        var appointed = successor.AppointOwner();

        if (appointed.IsFailure)
        {
            return Result.Failure<CommunityOwnershipOfferResponse>(appointed.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            await CommunityClosureMapper.ToResponseAsync(
                transfer, community, _profiles, cancellationToken));
    }
}

/// <summary>The one projection from these rows to what a screen renders.</summary>
internal static class CommunityClosureMapper
{
    public static async Task<CommunityDeletionRequestResponse> ToResponseAsync(
        CommunityDeletionRequest request,
        CommunityAggregate community,
        IMemberProfileService profiles,
        CancellationToken cancellationToken)
    {
        var names = await profiles.ResolveAsync([request.RequestedBy], cancellationToken);

        return new CommunityDeletionRequestResponse
        {
            Id = request.Id.Value,
            CommunityId = community.Id.Value,
            CommunityName = community.Name,
            CommunitySlug = community.Slug.Value,
            Status = request.Status.ToString(),
            Reason = request.Reason,
            RequestedBy = request.RequestedBy.Value,
            RequestedByName = Name(names, request.RequestedBy),
            RequestedAt = request.RequestedAt,
            EffectiveAt = request.EffectiveAt,
            ReviewedAt = request.ReviewedAt,
            DecisionReason = request.DecisionReason
        };
    }

    public static async Task<CommunityOwnershipOfferResponse> ToResponseAsync(
        CommunityOwnershipTransfer transfer,
        CommunityAggregate community,
        IMemberProfileService profiles,
        CancellationToken cancellationToken)
    {
        var names = await profiles.ResolveAsync(
            [transfer.OfferedBy, transfer.OfferedTo], cancellationToken);

        return new CommunityOwnershipOfferResponse
        {
            Id = transfer.Id.Value,
            CommunityId = community.Id.Value,
            CommunityName = community.Name,
            CommunitySlug = community.Slug.Value,
            Status = transfer.Status.ToString(),
            OfferedBy = transfer.OfferedBy.Value,
            OfferedByName = Name(names, transfer.OfferedBy),
            OfferedTo = transfer.OfferedTo.Value,
            OfferedToName = Name(names, transfer.OfferedTo),
            Message = transfer.Message,
            OfferedAt = transfer.OfferedAt,
            ExpiresAt = transfer.ExpiresAt,
            DeclineReason = transfer.DeclineReason
        };
    }

    /// <summary>
    /// Null rather than the identifier when the kernel would not say. A screen that printed a Guid
    /// where a name belongs is the defect this avoids; a screen with no name says "a member".
    /// </summary>
    private static string? Name(IReadOnlyDictionary<Guid, MemberProfile> names, UserId userId) =>
        names.TryGetValue(userId.Value, out var profile) ? profile.DisplayName : null;
}
