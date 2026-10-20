using Diaspora.Contracts.Community.V1.Publishing;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityProgramme.Extensibility.Publishing;

/// <summary>
/// Who may publish a post as a community (20 Oct 2026).
/// <para>
/// <b><see cref="CommunityCapabilitySet.CanPublishNotice"/>, and the community must be live.</b>
/// A post published as a community speaks in its name and lands in its moderation queue; the
/// capability set's own word for speaking for a community is publishing a notice — the Owner or an
/// Administrator, never a plain member or a moderator. <see cref="CommunityCapabilitySet.CanPostToCommunity"/>
/// adds that the caller is a current member of an Active community: a pending, suspended or archived
/// community has nobody speaking for it. Read from the same set the capabilities route gives the
/// app, so the composer's choice and the server's answer cannot disagree.
/// </para>
/// <para>
/// <b>No staff bypass.</b> Staff moderate any community; they do not speak for one.
/// </para>
/// </summary>
public sealed class CommunityPublisherAuthority : IPublisherAuthority
{
    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;

    public CommunityPublisherAuthority(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships)
    {
        _communities = communities;
        _memberships = memberships;
    }

    public async Task<PublisherGrant?> AuthorizeAsync(
        Guid memberId, Guid publisherId, CancellationToken cancellationToken = default)
    {
        if (memberId == Guid.Empty || publisherId == Guid.Empty)
        {
            return null;
        }

        var community = await _communities.GetAsync(CommunityId.From(publisherId), cancellationToken);

        if (community is null)
        {
            return null;
        }

        var membership = await _memberships.FindAsync(
            community.Id, UserId.From(memberId), cancellationToken);

        var capabilities = CommunityCapabilitySet.For(community, membership);

        return capabilities.CanPublishNotice && capabilities.CanPostToCommunity
            ? new PublisherGrant(PublisherKind.Community, community.Name)
            : null;
    }
}
