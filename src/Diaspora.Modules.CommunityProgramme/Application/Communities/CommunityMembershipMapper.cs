using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;

using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.Modules.CommunityProgramme.Application.Communities;

/// <summary>
/// The one projection from a membership to the wire.
/// <para>
/// <b>The capability set is derived here rather than by the caller, so it cannot be omitted.</b>
/// Every screen that renders a membership also needs to know what the member may do there, and a
/// mapper that made capabilities optional would eventually produce a membership with none — which
/// the client renders as "you may do nothing", indistinguishable from a real refusal.
/// </para>
/// </summary>
internal static class CommunityMembershipMapper
{
    public static CommunityMembershipResponse ToResponse(
        CommunityAggregate community, CommunityMembership? membership, bool isStaff = false)
    {
        ArgumentNullException.ThrowIfNull(community);

        var capabilities = CommunityCapabilitySet.For(community, membership, isStaff);

        return new CommunityMembershipResponse
        {
            CommunityId = community.Id.Value,
            State = (membership?.State ?? CommunityMembershipState.None).ToString(),
            IsPrimary = membership?.IsPrimary ?? false,
            Roles = [.. capabilities.Roles],
            Capabilities = ToResponse(community.Id.Value, capabilities),

            // Private when there is no row at all. A membership that does not exist is not public.
            Visibility = (membership?.Visibility ?? CommunityMembershipVisibility.Private).ToString(),

            JoinedAt = membership?.JoinedAt,
            VerifiedAt = membership?.VerifiedAt,

            // The community's decision and its reason, on the two states that carry one. Null on
            // Left, because nobody decided that but the member.
            StateReason = membership?.State is CommunityMembershipState.Declined
                or CommunityMembershipState.Revoked
                ? membership.DecisionReason
                : null,

            RequiresApproval = community.RequiresApproval
        };
    }

    public static CommunityMemberCapabilitiesResponse ToResponse(
        Guid communityId, CommunityCapabilitySet capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        return new CommunityMemberCapabilitiesResponse
        {
            CommunityId = communityId,
            Roles = [.. capabilities.Roles],
            CanViewMembers = capabilities.CanViewMembers,
            CanViewPrivateStatistics = capabilities.CanViewPrivateStatistics,
            CanPostToCommunity = capabilities.CanPostToCommunity,
            CanModerateContent = capabilities.CanModerateContent,
            CanReviewReports = capabilities.CanReviewReports,
            CanPublishNotice = capabilities.CanPublishNotice,
            CanManageRules = capabilities.CanManageRules,
            CanManageRoles = capabilities.CanManageRoles,
            CanAppointAdministrators = capabilities.CanAppointAdministrators,
            CanViewModerationQueue = capabilities.CanViewModerationQueue,
            CanViewAmbassadorDirectory = capabilities.CanViewAmbassadorDirectory,
            CanManageInitiatives = capabilities.CanManageInitiatives
        };
    }
}
