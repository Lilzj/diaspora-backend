using System.Globalization;
using Diaspora.Contracts.Automation.V1.Extensibility;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityProgramme.Extensibility.Automation;

/// <summary>
/// Who may own a community's automations, and who its notices reach — 11 Sep 2026.
/// <para>
/// <b>The seam that was answered by nobody.</b> Every other scope a member can act in had an
/// authority: <c>SelfOnlyScopeAuthority</c> for a member's own affairs, CommunityLife's for an
/// organisation, Creator's for a creator record. The Community owner kind fell through to the floor,
/// which answers <c>None()</c> for it — so <c>community.milestoneReached</c>, <c>notify.community</c>
/// and every other community-owned part was reachable by nobody, and the reason was not that this
/// seam was hard to write. It was that a community had nobody who ran it.
/// </para>
/// <para>
/// <b>It has now.</b> Founding a community makes the founder its Owner and an Administrator, and the
/// Owner appoints further administrators. This authority reads exactly that — the same
/// <see cref="CommunityCapabilitySet"/> every community screen reads, so an automation's idea of who
/// runs a community cannot disagree with the community's own.
/// </para>
/// <para>
/// <b>Administrators own and broadcast; nobody else does.</b> A moderator reviews reports, and an
/// ambassador represents the community in public; neither was chosen to address every member of it,
/// and a broadcast is a message to all of them.
/// </para>
/// </summary>
public sealed class CommunityAutomationAuthority : IAutomationScopeAuthority
{
    /// <summary>The capability the community-owned triggers and <c>notify.community</c> ask for.</summary>
    public const string CommunityCapability = "automations.community";

    private readonly ICommunityRepository _communities;
    private readonly ICommunityMembershipRepository _memberships;

    public CommunityAutomationAuthority(
        ICommunityRepository communities,
        ICommunityMembershipRepository memberships)
    {
        _communities = communities;
        _memberships = memberships;
    }

    public IReadOnlyList<AutomationOwnerKind> Handles { get; } = [AutomationOwnerKind.Community];

    public async Task<AutomationScopeGrant> EvaluateAsync(
        UserId member,
        AutomationOwnerKind ownerKind,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(member);

        if (ownerKind != AutomationOwnerKind.Community
            || member.Value == Guid.Empty
            || ownerId == Guid.Empty)
        {
            return AutomationScopeGrant.None();
        }

        var community = await _communities.GetAsync(CommunityId.From(ownerId), cancellationToken);

        if (community is null)
        {
            // Deny without saying why, as the organisation authority does — a grant that told
            // "does not exist" from "not yours" apart would put that distinction back.
            return AutomationScopeGrant.None();
        }

        var membership = await _memberships.FindAsync(community.Id, member, cancellationToken);
        var capabilities = CommunityCapabilitySet.For(community, membership);

        // `CanManageRoles` is the community's own word for "administers here": the Owner or an
        // Administrator, in a community that is not archived. Reusing it rather than re-deriving it
        // is what keeps this answer and the community's screens from drifting apart.
        if (!capabilities.CanManageRoles)
        {
            return AutomationScopeGrant.None(community.Name);
        }

        // Reaching members needs a community people can be in. A pending one has its founder and
        // nobody else; a suspended one has been stopped.
        var canReach = community.Status == CommunityStatus.Active;

        return new AutomationScopeGrant(
            CanOwn: true,

            // Any administrator may take over another's automation. A community's rules outlive
            // whoever wrote them, the reason the organisation authority gives for the same choice.
            CanManage: true,
            CanReachMembers: canReach,
            Label: community.Name,
            Capabilities: [CommunityCapability],
            ReachRefusal: community.Status switch
            {
                CommunityStatus.Active => null,
                CommunityStatus.Pending =>
                    "This community has not been opened yet, so there is nobody to notify. It will "
                    + "reach members once Diaspora opens it.",
                CommunityStatus.Suspended =>
                    "This community is suspended, so its members cannot be notified until it is "
                    + "reopened.",
                _ => "This community is closed, so its members can no longer be notified."
            });
    }

    public async Task<AutomationAudienceEstimate> EstimateAudienceAsync(
        AutomationOwnerKind ownerKind,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        if (ownerKind != AutomationOwnerKind.Community || ownerId == Guid.Empty)
        {
            return AutomationAudienceEstimate.Unavailable("This is not a community scope.");
        }

        var community = await _communities.GetAsync(CommunityId.From(ownerId), cancellationToken);

        if (community is null)
        {
            return AutomationAudienceEstimate.Unavailable("This community could not be found.");
        }

        var members = community.MemberCount;

        // Banded below ten, the same suppression rule the organisation authority applies and B19
        // applies to a small cohort: "3 members" in a small community identifies them.
        return members < 10
            ? new AutomationAudienceEstimate(members, "Fewer than ten members.", IsExact: false)
            : new AutomationAudienceEstimate(
                members,
                $"About {members.ToString("N0", CultureInfo.InvariantCulture)} members.",
                IsExact: true);
    }

    public async Task<IReadOnlyList<UserId>> ListRecipientsAsync(
        AutomationOwnerKind ownerKind,
        Guid ownerId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (ownerKind != AutomationOwnerKind.Community || ownerId == Guid.Empty)
        {
            return [];
        }

        // Every current member, private ones included. Receiving a notice discloses nothing to
        // anybody, and a member who kept their belonging private still belongs.
        var page = await _memberships.ListCurrentMemberIdsAsync(
            CommunityId.From(ownerId), skip, take, cancellationToken);

        return [.. page.Select(UserId.From)];
    }
}
