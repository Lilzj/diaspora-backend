using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.Modules.CommunityProgramme.Application.Communities;

/// <summary>
/// FRONTEND-PHASE13-GAP-003 — the caller's whole set of community relationships.
/// <para>
/// <b>Served at <c>/communities/me/memberships</c> rather than the register's
/// <c>/communities/memberships/me</c>, because that path is taken.</b> It has answered B18's
/// <em>programme</em> memberships since the module shipped — an organization's paid membership
/// scheme, a different concept with the same word. The register marked this gap connected against
/// it, which was the mis-mark: the route is real and answers something else, and its own client
/// adapter records the symptom, that <c>primaryCommunityId</c> "is not carried at all".
/// </para>
/// <para>
/// <c>me</c> is a reserved handle, so <c>/communities/me/…</c> is a space no community can take.
/// </para>
/// </summary>
[Authorize(Permission = CommunityProgrammePermissions.Read)]
public sealed record GetMyCommunityMembershipsQuery : IQuery<CommunityMembershipSetResponse>;

public sealed class GetMyCommunityMembershipsQueryHandler
    : IQueryHandler<GetMyCommunityMembershipsQuery, CommunityMembershipSetResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly CommunityProgrammeAccessService _access;

    public GetMyCommunityMembershipsQueryHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        CommunityProgrammeAccessService access)
    {
        _communities = communities;
        _memberships = memberships;
        _access = access;
    }

    public async Task<Result<CommunityMembershipSetResponse>> Handle(
        GetMyCommunityMembershipsQuery query, CancellationToken cancellationToken)
    {
        var caller = _access.RequireCaller();

        return caller.IsFailure
            ? Result.Failure<CommunityMembershipSetResponse>(caller.Error)
            : Result.Success(await ReadAsync(caller.Value, _access, _communities, _memberships, cancellationToken));
    }

    /// <summary>
    /// Reads the set. Shared with the primary-community write, which answers with the whole set.
    /// <para>
    /// <b>Every row is returned, terminal ones included.</b> <c>Left</c>, <c>Declined</c> and
    /// <c>Revoked</c> are three different next steps on the client, and dropping them would offer
    /// Join to somebody the community has just removed.
    /// </para>
    /// </summary>
    internal static async Task<CommunityMembershipSetResponse> ReadAsync(
        UserId caller,
        CommunityProgrammeAccessService access,
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        CancellationToken cancellationToken)
    {
        var rows = await memberships.FindForMemberAsync(caller, null, cancellationToken);

        var items = new List<CommunityMembershipResponse>(rows.Count);
        CommunityAggregate? primary = null;

        foreach (var (communityId, membership) in rows)
        {
            var community = await communities.GetAsync(communityId, cancellationToken);

            // A membership whose community could not be read is dropped rather than half-rendered.
            // The alternative is a row on the client with an id and no name.
            if (community is null)
            {
                continue;
            }

            if (membership.IsPrimary)
            {
                primary = community;
            }

            items.Add(CommunityMembershipMapper.ToResponse(community, membership, access.IsStaff));
        }

        return new CommunityMembershipSetResponse
        {
            Memberships = items,

            // Stated rather than left to be scanned. The client reads this in preference to
            // scanning isPrimary, so a set that somehow carried two renders as one.
            PrimaryCommunityId = primary?.Id.Value,
            FetchedAt = DateTimeOffset.UtcNow
        };
    }
}

/// <summary>
/// FRONTEND-PHASE13-GAP-005 — setting and clearing the primary community.
/// <para>
/// <b>Served at <c>/communities/me/primary</c>, not the register's <c>/users/me/primary-community</c>.</b>
/// There is no <c>/api/v1/users</c> space on this platform and no module owns one; the register
/// sketched a route into a prefix that does not exist. Primary lives on the membership because the
/// rule that matters — a primary must be a community you belong to — is then structural rather than
/// a check somebody can forget: there is nowhere to record one without a membership to hang it on.
/// </para>
/// <para>
/// <b>It answers the whole set, which the register asks for and gives the reason: "so primacy cannot
/// disagree".</b> Setting a primary clears the previous one, and a response carrying only the new
/// row would leave a client holding a stale <c>isPrimary: true</c> on the old.
/// </para>
/// <para>
/// <b>Never back-filled from nationality.</b> §55 forbids inferring community membership from
/// identity data, and a migration that seeded this would assert a community for every existing
/// member that none of them chose. No migration here writes it.
/// </para>
/// </summary>
[Authorize(Permission = CommunityProgrammePermissions.Participate)]
public sealed record SetPrimaryCommunityCommand : ICommand<CommunityMembershipSetResponse>
{
    /// <summary>The community to lead with, or null to clear.</summary>
    public Guid? CommunityId { get; init; }
}

public sealed class SetPrimaryCommunityCommandHandler
    : ICommandHandler<SetPrimaryCommunityCommand, CommunityMembershipSetResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly CommunityProgrammeAccessService _access;
    private readonly IUnitOfWork _unitOfWork;

    public SetPrimaryCommunityCommandHandler(
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

    public async Task<Result<CommunityMembershipSetResponse>> Handle(
        SetPrimaryCommunityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CommunityMembershipSetResponse>(caller.Error);
        }

        // The one already set, cleared first and unconditionally. A filtered unique index enforces
        // one primary per member, so setting a second without clearing the first is not a wrong
        // answer — it is a database error on an ordinary tap.
        var current = await _memberships.FindPrimaryAsync(caller.Value, cancellationToken);

        if (current is not null)
        {
            current.ClearPrimary();

            // Saved on its own, and this is the whole reason the method has two saves.
            //
            // Clearing and setting in one unit of work leaves the ordering of the two UPDATEs to
            // EF, and it does not order them: the set lands first, both rows carry IsPrimary for an
            // instant, and the filtered unique index rejects the batch. The result is a 500 on
            // somebody changing their primary community, which is exactly the ordinary tap this
            // index is meant to survive rather than punish.
            //
            // What a crash between the two saves costs is that the member ends with no primary
            // rather than the wrong one. That is a state the product already has — most members
            // have never set one — and it self-corrects on the next tap. The alternative failure,
            // two primaries, is the one nothing above this layer could resolve.
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (command.CommunityId is { } wanted)
        {
            var membership = await _memberships.FindAsync(
                CommunityId.From(wanted), caller.Value, cancellationToken);

            if (membership is null)
            {
                return Result.Failure<CommunityMembershipSetResponse>(
                    CommunityErrors.PrimaryRequiresMembership);
            }

            var made = membership.MakePrimary();

            if (made.IsFailure)
            {
                return Result.Failure<CommunityMembershipSetResponse>(made.Error);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            await GetMyCommunityMembershipsQueryHandler.ReadAsync(
                caller.Value, _access, _communities, _memberships, cancellationToken));
    }
}

/// <summary>
/// FRONTEND-PHASE13-GAP-006 — what this caller may do in this community.
/// <para>
/// <b>An all-false set rather than a 403 for a stranger, which the register asks for by name.</b> A
/// refusal would tell the caller a moderation queue exists; eleven falses tell them what they can
/// do, which is the question they asked.
/// </para>
/// <para>
/// Anonymous, and answers all-false. Requiring a token would make the client special-case the
/// signed-out path, and the answer is the same either way.
/// </para>
/// </summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record GetCommunityCapabilitiesQuery : IQuery<CommunityMemberCapabilitiesResponse>
{
    public required string IdOrSlug { get; init; }
}

public sealed class GetCommunityCapabilitiesQueryHandler
    : IQueryHandler<GetCommunityCapabilitiesQuery, CommunityMemberCapabilitiesResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly CommunityProgrammeAccessService _access;

    public GetCommunityCapabilitiesQueryHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        CommunityProgrammeAccessService access)
    {
        _communities = communities;
        _memberships = memberships;
        _access = access;
    }

    public async Task<Result<CommunityMemberCapabilitiesResponse>> Handle(
        GetCommunityCapabilitiesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var community = await _communities.FindByIdOrSlugAsync(query.IdOrSlug, cancellationToken);

        if (community is null)
        {
            return Result.Failure<CommunityMemberCapabilitiesResponse>(CommunityErrors.NotFound);
        }

        var membership = _access.CallerId is { } caller
            ? await _memberships.FindAsync(community.Id, caller, cancellationToken)
            : null;

        // A community the caller may not read reports nothing about itself, including what they
        // cannot do in it. Same rule, same answer — see GetCommunityQueryHandler.
        if (community.Visibility is CommunityVisibility.Private or CommunityVisibility.InviteOnly
            && membership is null
            && !_access.IsStaff)
        {
            return Result.Failure<CommunityMemberCapabilitiesResponse>(CommunityErrors.NotFound);
        }

        return Result.Success(
            CommunityMembershipMapper.ToResponse(
                community.Id.Value,
                CommunityCapabilitySet.For(community, membership, _access.IsStaff)));
    }
}
