using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.UnitTests.CommunityProgramme;

/// <summary>
/// What the server says a caller may do in a community — FRONTEND-PHASE13-GAP-006.
/// <para>
/// <b>This is the type that exists so the client has no code path that derives a capability from a
/// role.</b> §62 forbids trusting frontend community state for roles, moderation or content access,
/// and the register's own reason is that a client-side derivation "would show a moderation queue to
/// somebody the server will refuse — which is worse than showing nothing". Every branch of that
/// derivation is here.
/// </para>
/// </summary>
public class CommunityCapabilitySetTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    // ── Nobody ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_stranger_holds_nothing()
    {
        var set = CommunityCapabilitySet.For(Opened(), membership: null);

        set.Roles.Should().BeEmpty();
        set.CanViewMembers.Should().BeFalse();
        set.CanPostToCommunity.Should().BeFalse();
        set.CanModerateContent.Should().BeFalse();
        set.CanManageRoles.Should().BeFalse();
    }

    [Fact]
    public void A_community_that_could_not_be_read_grants_nothing()
    {
        // Not an exception and not a partial set. A caller who cannot be told about the community
        // is told they can do nothing in it, which is true.
        CommunityCapabilitySet.For(community: null, membership: null)
            .Should().Be(CommunityCapabilitySet.None);
    }

    [Fact]
    public void Following_a_community_is_not_belonging_to_it()
    {
        var (community, membership) = Following();

        var set = CommunityCapabilitySet.For(community, membership);

        set.CanViewMembers.Should().BeFalse("a follower has not been let in");
        set.CanPostToCommunity.Should().BeFalse();
    }

    // ── Members ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_member_may_read_the_roll_and_post()
    {
        var (community, membership) = Member();

        var set = CommunityCapabilitySet.For(community, membership);

        set.CanViewMembers.Should().BeTrue();
        set.CanPostToCommunity.Should().BeTrue();
        set.CanViewAmbassadorDirectory.Should().BeTrue();
    }

    [Fact]
    public void A_member_holds_no_moderation_capability_by_being_a_member()
    {
        // The whole point of the split: belonging is not authority.
        var (community, membership) = Member();

        var set = CommunityCapabilitySet.For(community, membership);

        set.CanModerateContent.Should().BeFalse();
        set.CanReviewReports.Should().BeFalse();
        set.CanViewModerationQueue.Should().BeFalse();
        set.CanManageRules.Should().BeFalse();
        set.CanManageRoles.Should().BeFalse();
    }

    [Fact]
    public void A_moderator_moderates_and_does_not_administer()
    {
        var (community, membership) = Member();
        membership.Grant(CommunityMembershipRole.Moderator);

        var set = CommunityCapabilitySet.For(community, membership);

        set.CanModerateContent.Should().BeTrue();
        set.CanReviewReports.Should().BeTrue();
        set.CanViewModerationQueue.Should().BeTrue();
        set.CanViewPrivateStatistics.Should().BeTrue();

        set.CanManageRules.Should().BeFalse("running the community is a different role");
        set.CanManageRoles.Should().BeFalse();
        set.CanPublishNotice.Should().BeFalse();
    }

    [Fact]
    public void An_administrator_holds_the_moderation_set_too()
    {
        // Otherwise the person who runs the community would have to grant themselves a second role
        // to act on a report, which nobody would remember to do.
        var (community, membership) = Member();
        membership.Grant(CommunityMembershipRole.Administrator);

        var set = CommunityCapabilitySet.For(community, membership);

        set.CanManageRules.Should().BeTrue();
        set.CanManageRoles.Should().BeTrue();
        set.CanPublishNotice.Should().BeTrue();
        set.CanModerateContent.Should().BeTrue();
        set.CanManageInitiatives.Should().BeTrue();
    }

    [Fact]
    public void An_ambassador_represents_and_does_not_moderate()
    {
        var (community, membership) = Member();
        membership.Grant(CommunityMembershipRole.Ambassador);

        var set = CommunityCapabilitySet.For(community, membership);

        set.CanManageInitiatives.Should().BeTrue();
        set.CanModerateContent.Should().BeFalse("representing is not policing");
        set.CanReviewReports.Should().BeFalse();
    }

    [Fact]
    public void Roles_are_echoed_for_display()
    {
        var (community, membership) = Member();
        membership.Grant(CommunityMembershipRole.Moderator);

        CommunityCapabilitySet.For(community, membership)
            .Roles.Should().ContainSingle().Which.Should().Be("Moderator");
    }

    // ── Staff ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Staff_moderate_a_community_they_do_not_belong_to()
    {
        // Moderation cannot require belonging to everything it touches.
        var set = CommunityCapabilitySet.For(Opened(), membership: null, isStaff: true);

        set.CanModerateContent.Should().BeTrue();
        set.CanReviewReports.Should().BeTrue();
        set.CanViewModerationQueue.Should().BeTrue();
    }

    [Fact]
    public void Staff_do_not_read_the_member_roll()
    {
        // The line B9, B10, B11 and B12 all draw. Reading who belongs to a named faith community,
        // support group or political association should take a deliberate support tool, not fall
        // out of a role check in a general-purpose helper.
        var set = CommunityCapabilitySet.For(Opened(), membership: null, isStaff: true);

        set.CanViewMembers.Should().BeFalse();
        set.CanPostToCommunity.Should().BeFalse("staff are not members");
    }

    // ── Lifecycle ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_suspended_community_cannot_be_posted_to_and_can_still_be_moderated()
    {
        // Suspension usually exists precisely so somebody can review what happened. Taking the
        // queue away from the people reviewing it would be the wrong way round.
        var (community, membership) = Member();
        membership.Grant(CommunityMembershipRole.Moderator);
        community.Suspend("Under review.");

        var set = CommunityCapabilitySet.For(community, membership);

        set.CanPostToCommunity.Should().BeFalse();
        set.CanViewMembers.Should().BeTrue("they still belong");
        set.CanModerateContent.Should().BeTrue();
    }

    [Fact]
    public void An_archived_community_grants_nothing_but_reading()
    {
        var (community, membership) = Member();
        membership.Grant(CommunityMembershipRole.Administrator);
        community.Archive("Closed by its organizers.");

        var set = CommunityCapabilitySet.For(community, membership);

        set.CanPostToCommunity.Should().BeFalse();
        set.CanModerateContent.Should().BeFalse();
        set.CanManageRules.Should().BeFalse();
        set.CanManageRoles.Should().BeFalse();
        set.CanManageInitiatives.Should().BeFalse();

        set.CanViewMembers.Should().BeTrue("a member can still see who they were there with");
    }

    // ── Roles follow standing ───────────────────────────────────────────────────────────────

    [Fact]
    public void A_role_cannot_be_granted_to_somebody_who_does_not_belong()
    {
        var (_, membership) = Following();

        membership.Grant(CommunityMembershipRole.Moderator).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Leaving_takes_the_roles_with_it()
    {
        // A revoked moderator who kept the badge would keep it on every screen that reads roles.
        var (community, membership) = Member();
        membership.Grant(CommunityMembershipRole.Moderator);

        membership.Revoke(UserId.New(), "Removed.", Now);

        membership.Roles.Should().BeEmpty();
        CommunityCapabilitySet.For(community, membership).CanModerateContent.Should().BeFalse();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static CommunityAggregate Opened()
    {
        var community = CommunityAggregate.Propose(
            CommunityId.New(),
            CommunitySlug.Create("nigerians-in-london").Value,
            "Nigerians in London",
            CommunityType.NationalDiaspora,
            CommunityVisibility.Public,
            "NG").Value;

        community.Open();
        return community;
    }

    private static (CommunityAggregate Community, CommunityMembership Membership) Following()
    {
        var community = Opened();

        var membership = CommunityMembership.Start(
            CommunityMembershipId.New(), community.Id, UserId.New(),
            CommunityMembershipState.Following).Value;

        return (community, membership);
    }

    private static (CommunityAggregate Community, CommunityMembership Membership) Member()
    {
        var (community, membership) = Following();
        membership.Join(community, requiresApproval: false, Now);
        return (community, membership);
    }
}
