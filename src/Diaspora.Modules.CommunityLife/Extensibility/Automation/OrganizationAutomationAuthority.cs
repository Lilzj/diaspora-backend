using System.Globalization;
using Diaspora.Contracts.Automation.V1.Extensibility;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Memberships;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityLife.Extensibility.Automation;

/// <summary>
/// B12's answer to what an organization's people may automate — B23.x Wave 12.
///
/// <para><b>Wave 11 shipped <c>notify.organization</c> working and granted to nobody.</b>
/// <c>SelfOnlyScopeAuthority</c> withholds <c>automations.organization</c> with a stated reason: "this
/// authority cannot tell a moderator from a member — that is B6, B12 and B18's knowledge". This is
/// B12 answering, for the scope it owns.</para>
///
/// <para><b>Two roles, two different permissions, and the split is the point.</b> An organizer may
/// write and run automations for the body — reminders, registration alerts — because organizing
/// events is what the role is for. Only an administrator may write one that <em>notifies the
/// members</em>, because a message to everybody who joined is the strongest thing an organization can
/// do to its own people and the platform already reserves it for administrators everywhere else.</para>
///
/// <para><b>The role is re-read in the moment before every dispatch.</b> The automation engine calls
/// <c>EvaluateAsync</c> per run rather than trusting what was true when the rule was written, which is
/// the only place an organizer removed since then can be stopped. An automation whose author lost the
/// role is flagged and disabled rather than silently skipped, so somebody is told.</para>
/// </summary>
public sealed class OrganizationAutomationAuthority : IAutomationScopeAuthority
{
    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationMembershipRepository _memberships;

    public OrganizationAutomationAuthority(
        IOrganizationRepository organizations,
        IOrganizationMembershipRepository memberships)
    {
        _organizations = organizations;
        _memberships = memberships;
    }

    /// <summary>
    /// Organization scope, and nothing else.
    /// <para>
    /// Naming one kind is what makes the automation platform's resolver prefer this over the shipped
    /// floor, which handles every kind. Preferring the specific one means registration order — which is
    /// assembly scan order — cannot decide a permission.
    /// </para>
    /// <para>
    /// <b><c>Community</c> is deliberately not handled.</b> This module owns organizations; a
    /// community in the frontend's Phase 13 sense is a different concept with no backend yet, and an
    /// authority that answered for it would be this module claiming a scope it does not own.
    /// </para>
    /// </summary>
    public IReadOnlyList<AutomationOwnerKind> Handles { get; } = [AutomationOwnerKind.Organization];

    public async Task<AutomationScopeGrant> EvaluateAsync(
        UserId member,
        AutomationOwnerKind ownerKind,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(member);

        if (ownerKind != AutomationOwnerKind.Organization ||
            member.Value == Guid.Empty ||
            ownerId == Guid.Empty)
        {
            return AutomationScopeGrant.None();
        }

        var organizationId = OrganizationId.From(ownerId);
        var organization = await _organizations.GetAsync(organizationId, cancellationToken);

        if (organization is null)
        {
            // Deleted, or never existed. Deny without saying which — the automation platform answers
            // 404-not-403 on a foreign identifier, and a grant that distinguished them here would put
            // the distinction back.
            return AutomationScopeGrant.None();
        }

        var membership = await _memberships.FindCurrentAsync(
            organizationId, member, cancellationToken);

        if (membership is null || !membership.CanOrganize)
        {
            return AutomationScopeGrant.None(organization.Name.DisplayName);
        }

        var isAdministrator = membership.CanAdminister;

        return new AutomationScopeGrant(
            CanOwn: true,

            // An administrator may take over an organizer's automation. An organization's rules
            // outlive whoever wrote them, and a body whose only organizer left would otherwise hold
            // an automation nobody could turn off.
            CanManage: isAdministrator || membership.CanOrganize,
            CanReachMembers: isAdministrator,
            Label: organization.Name.DisplayName,
            Capabilities: isAdministrator ? ["automations.organization"] : []);
    }

    public async Task<AutomationAudienceEstimate> EstimateAudienceAsync(
        AutomationOwnerKind ownerKind,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        if (ownerKind != AutomationOwnerKind.Organization || ownerId == Guid.Empty)
        {
            return AutomationAudienceEstimate.Unavailable("This is not an organization scope.");
        }

        var counts = await _memberships.CountByStatusAsync(
            OrganizationId.From(ownerId), cancellationToken);

        // Current members only. An invited or requested member has not joined, and counting them
        // would tell an administrator a broadcast reaches people it must not.
        var active = counts.TryGetValue(MembershipStatus.Active, out var count) ? count : 0;

        // Small memberships are banded rather than reported exactly, the same suppression rule B19
        // applies to a small cohort: "3 members" in a small body identifies them.
        return active < 10
            ? new AutomationAudienceEstimate(
                active, "Fewer than ten current members.", IsExact: false)
            : new AutomationAudienceEstimate(
                active,
                $"About {active.ToString("N0", CultureInfo.InvariantCulture)} current members.",
                IsExact: true);
    }

    public async Task<IReadOnlyList<UserId>> ListRecipientsAsync(
        AutomationOwnerKind ownerKind,
        Guid ownerId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (ownerKind != AutomationOwnerKind.Organization || ownerId == Guid.Empty)
        {
            return [];
        }

        var page = await _memberships.ListCurrentMemberPageAsync(
            OrganizationId.From(ownerId), skip, take, cancellationToken);

        return [.. page.Select(UserId.From)];
    }
}
