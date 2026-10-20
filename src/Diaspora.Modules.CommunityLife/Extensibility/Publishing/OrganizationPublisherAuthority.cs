using Diaspora.Contracts.Community.V1.Publishing;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Memberships;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityLife.Extensibility.Publishing;

/// <summary>
/// Who may publish a post as an organisation (20 Oct 2026).
/// <para>
/// <b>An Organizer or above, in a current membership</b> (<see cref="OrganizationMembership.CanOrganize"/>):
/// the role this module defines as acting "on the organization's behalf", and the one that already
/// decides who may announce an event under its name. A plain member, a volunteer or a moderator may
/// not. The organisation must also be one that may carry things under its name at all — the same
/// <see cref="Organization.AcceptsEvents"/> gate an event gets: published or active, not deleted,
/// hidden or suspended.
/// </para>
/// <para>
/// <b>No staff bypass.</b> Unlike this module's other role checks: staff may manage an
/// organisation's page, but a post in its name is the organisation speaking.
/// </para>
/// </summary>
public sealed class OrganizationPublisherAuthority : IPublisherAuthority
{
    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationMembershipRepository _memberships;

    public OrganizationPublisherAuthority(
        IOrganizationRepository organizations,
        IOrganizationMembershipRepository memberships)
    {
        _organizations = organizations;
        _memberships = memberships;
    }

    public async Task<PublisherGrant?> AuthorizeAsync(
        Guid memberId, Guid publisherId, CancellationToken cancellationToken = default)
    {
        if (memberId == Guid.Empty || publisherId == Guid.Empty)
        {
            return null;
        }

        var organizationId = OrganizationId.From(publisherId);
        var organization = await _organizations.GetAsync(organizationId, cancellationToken);

        if (organization is null || !organization.AcceptsEvents)
        {
            return null;
        }

        var membership = await _memberships.FindCurrentAsync(
            organizationId, UserId.From(memberId), cancellationToken);

        return membership?.CanOrganize == true
            ? new PublisherGrant(PublisherKind.Organization, organization.Name.DisplayName)
            : null;
    }
}
