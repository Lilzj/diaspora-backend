using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Results;

using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.Modules.CommunityProgramme.Application.Communities;

/// <summary>
/// FRONTEND-PHASE13-GAP-002 — one community, by id or by handle.
/// <para>
/// <b>Both, on one route, because a deep link carries the handle and a client that already has the
/// community carries the id.</b> Making them two routes would push the choice onto the client, which
/// would then have to guess what a pasted string is.
/// </para>
/// </summary>
public sealed record GetCommunityQuery : IQuery<CommunityResponse>
{
    public required string IdOrSlug { get; init; }
}

/// <summary>
/// <b>The documented 404-versus-403 rule, which the register asked for by name.</b>
/// <para>
/// <b>This route never answers 403.</b> Every reason it would refuse is also a reason not to confirm
/// the community exists, so a refusal is always <c>404 communities.community.not-found</c> — the
/// same answer a caller gets for a handle nobody has taken.
/// </para>
/// <para>
/// The register's requirement reads both ways at once: "a 403 must not be answered with a 404 that
/// leaks nothing, nor a 404 with a 403 that confirms a private community exists". A single rule
/// satisfies both — <b>a caller who already demonstrably knows the community exists is shown it</b>:
/// </para>
/// <list type="bullet">
/// <item>Public and Unlisted are readable by anyone, signed in or not. Unlisted is merely kept out
/// of the directory; the link is the access.</item>
/// <item>Private and InviteOnly are readable by anyone holding <em>any</em> membership row —
/// including somebody who left, was declined or was revoked. They know it exists, and their own row
/// tells them why they are out. Answering them 404 would be a lie they can detect.</item>
/// <item>To everybody else, Private and InviteOnly are absent. Not forbidden: absent.</item>
/// <item>A <c>Pending</c> community is absent to everyone but its creator and staff, whatever its
/// visibility. A name somebody has reserved but not opened is not yet a public fact.</item>
/// <item><c>Suspended</c> and <c>Archived</c> are readable, and carry <c>statusReason</c> verbatim.
/// That is the whole point of §75 keeping them distinct from deleted: a link sent last year resolves
/// to an explanation rather than a dead end.</item>
/// </list>
/// <para>
/// Where 403 <em>does</em> belong is the writes — joining a community that has revoked you. There
/// the caller already knows it exists, so the refusal tells them nothing new.
/// </para>
/// </summary>
public sealed class GetCommunityQueryHandler : IQueryHandler<GetCommunityQuery, CommunityResponse>
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;
    private readonly CommunityProgrammeAccessService _access;

    public GetCommunityQueryHandler(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships,
        CommunityProgrammeAccessService access)
    {
        _communities = communities;
        _memberships = memberships;
        _access = access;
    }

    public async Task<Result<CommunityResponse>> Handle(
        GetCommunityQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var community = await _communities.FindByIdOrSlugAsync(query.IdOrSlug, cancellationToken);

        if (community is null)
        {
            return Result.Failure<CommunityResponse>(CommunityErrors.NotFound);
        }

        var membership = _access.CallerId is { } viewer
            ? await _memberships.FindAsync(community.Id, viewer, cancellationToken)
            : null;

        if (!CanRead(community, membership))
        {
            // The same error, deliberately. See the type summary.
            return Result.Failure<CommunityResponse>(CommunityErrors.NotFound);
        }

        // A private community reports its size only to people who are in it. Withheld means the
        // field is absent, never zero — see CommunityResponse.MemberCount.
        var reportMemberCount =
            community.Visibility is CommunityVisibility.Public or CommunityVisibility.Unlisted
            || membership?.IsCurrentMember == true;

        return Result.Success(CommunityMapper.ToResponse(community, reportMemberCount));
    }

    private bool CanRead(CommunityAggregate community, CommunityMembership? membership)
    {
        // Staff read everything. Moderation cannot require belonging to what it moderates.
        if (_access.IsStaff)
        {
            return true;
        }

        // Not yet open. Its creator can see it; nobody else can know the name is taken.
        if (community.Status == CommunityStatus.Pending)
        {
            return _access.CallerId is { } caller
                && string.Equals(community.CreatedBy, caller.Value.ToString(), StringComparison.Ordinal);
        }

        return community.Visibility switch
        {
            CommunityVisibility.Public or CommunityVisibility.Unlisted => true,

            // Any row at all, in any state. Somebody who was removed knows the community exists.
            _ => membership is not null
        };
    }
}
