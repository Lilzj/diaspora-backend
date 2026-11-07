using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Memberships;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityLife.Application.Services;

/// <summary>
/// Who answers for an event or an organisation when it is reported or moderated (7 Nov 2026).
/// <para>
/// <b>An event's organiser; an organisation's Owner member, not whoever created it.</b> Ownership of
/// an organisation can be handed on, and the Community content source's <c>CreatedByUserId</c> then
/// names somebody who no longer runs it: they would be told about a hide they cannot act on, the
/// real owner would not, and the real owner could report their own page while its creator could
/// not. The creator is the fallback only for a body with no active Owner, which every organisation
/// is created with and should never lack. <c>OrganizationReadContext</c> made the same choice for
/// who a message to an organisation reaches.
/// </para>
/// <para>
/// One place, so the report filing, the case Trust opens and the decision's notice all name the same
/// person.
/// </para>
/// </summary>
public sealed class CommunityLifeContentOwners
{
    private readonly IOrganizationMembershipRepository _memberships;

    public CommunityLifeContentOwners(IOrganizationMembershipRepository memberships) =>
        _memberships = memberships;

    public static UserId OfEvent(CommunityEvent communityEvent)
    {
        ArgumentNullException.ThrowIfNull(communityEvent);

        return communityEvent.OrganizerUserId;
    }

    public async Task<UserId> OfOrganizationAsync(
        Organization organization, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organization);

        var owner = await _memberships.FindOwnerAsync(organization.Id, cancellationToken);

        return owner?.MemberUserId ?? organization.CreatedByUserId;
    }
}
