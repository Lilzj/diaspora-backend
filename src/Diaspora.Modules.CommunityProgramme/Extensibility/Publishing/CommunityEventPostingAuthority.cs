using Diaspora.Contracts.CommunityProgramme.V1.Directory;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityProgramme.Extensibility.Publishing;

/// <summary>
/// Who may post an event into a community (21 Oct 2026): <see cref="CommunityCapabilitySet.CanPostToCommunity"/>.
/// <para>
/// A current member of an Active community — the bar for posting into a community, not the higher
/// one for speaking as it (<see cref="CommunityPublisherAuthority"/>). An event posted into a
/// community is the member's event, hosted by them and shown in the community's list; it does not
/// speak in the community's name. Read from the same set the capabilities route gives the app, so
/// the "Post in community" picker and the server's answer cannot disagree.
/// </para>
/// <para><b>No staff bypass</b>, for the reason the publisher authority gives.</para>
/// </summary>
public sealed class CommunityEventPostingAuthority : ICommunityEventPostingAuthority
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;

    public CommunityEventPostingAuthority(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships)
    {
        _communities = communities;
        _memberships = memberships;
    }

    public async Task<CommunityEventPostingGrant?> AuthorizeAsync(
        Guid memberId, Guid communityId, CancellationToken cancellationToken = default)
    {
        if (memberId == Guid.Empty || communityId == Guid.Empty)
        {
            return null;
        }

        var community = await _communities.GetAsync(CommunityId.From(communityId), cancellationToken);

        if (community is null)
        {
            return null;
        }

        var membership = await _memberships.FindAsync(
            community.Id, UserId.From(memberId), cancellationToken);

        return CommunityCapabilitySet.For(community, membership).CanPostToCommunity
            ? new CommunityEventPostingGrant(community.Id.Value, community.Name, community.Slug.Value)
            : null;
    }
}
