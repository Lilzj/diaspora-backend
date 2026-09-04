using Diaspora.Modules.CommunityProgramme;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

// `Diaspora.UnitTests.Community` is a sibling namespace, so the bare name resolves to it.
using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.UnitTests.CommunityProgramme;

/// <summary>
/// One person's standing in one community.
/// <para>
/// The register marked joining a community as served, against
/// <c>PUT /communities/{organizationId}/membership</c> — which opens an organization's membership
/// <em>programme</em> and is role-gated. That mark was hiding a real gap. These pin the state
/// machine that fills it, and in particular the three refusal states the client renders differently.
/// </para>
/// </summary>
public class CommunityMembershipTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    // ── Starting ────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(CommunityMembershipState.Discovering)]
    [InlineData(CommunityMembershipState.Interested)]
    [InlineData(CommunityMembershipState.Following)]
    public void A_relationship_can_start_at_any_state_that_carries_no_standing(
        CommunityMembershipState state)
    {
        var result = CommunityMembership.Start(
            CommunityMembershipId.New(), CommunityId.New(), UserId.New(), state);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCurrentMember.Should().BeFalse();
    }

    [Theory]
    [InlineData(CommunityMembershipState.Member)]
    [InlineData(CommunityMembershipState.VerifiedMember)]
    [InlineData(CommunityMembershipState.PendingApproval)]
    public void Nothing_but_joining_may_mint_a_standing(CommunityMembershipState state)
    {
        // Join is where the community's own state and approval rule are read. A second door into
        // Member would let a caller skip both.
        CommunityMembership.Start(
            CommunityMembershipId.New(), CommunityId.New(), UserId.New(), state)
            .IsFailure.Should().BeTrue();
    }

    // ── Joining ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Joining_an_open_community_without_approval_is_immediate()
    {
        var (community, membership) = Following();

        membership.Join(community, requiresApproval: false, Now).IsSuccess.Should().BeTrue();

        membership.State.Should().Be(CommunityMembershipState.Member);
        membership.IsCurrentMember.Should().BeTrue();
        membership.JoinedAt.Should().Be(Now);
    }

    [Fact]
    public void Joining_a_community_that_asks_for_approval_waits()
    {
        var (community, membership) = Following();

        membership.Join(community, requiresApproval: true, Now).IsSuccess.Should().BeTrue();

        membership.State.Should().Be(CommunityMembershipState.PendingApproval);
        membership.IsCurrentMember.Should().BeFalse();
        membership.JoinedAt.Should().BeNull("waiting is not belonging");
    }

    [Fact]
    public void A_community_that_is_not_open_cannot_be_joined()
    {
        var (community, membership) = Following();
        community.Suspend("Under review.");

        membership.Join(community, requiresApproval: false, Now)
            .Error.Code.Should().Be(CommunityErrorCodes.NotJoinable);
    }

    [Fact]
    public void An_invite_only_community_refuses_a_join_without_one()
    {
        var (community, membership) = Following();
        community.SetVisibility(CommunityVisibility.InviteOnly);

        membership.Join(community, requiresApproval: false, Now)
            .Error.Code.Should().Be(CommunityErrorCodes.InviteRequired);

        membership.Join(community, requiresApproval: false, Now, hasInvitation: true)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Joining_twice_is_refused_rather_than_silently_repeated()
    {
        var (community, membership) = Following();
        membership.Join(community, requiresApproval: false, Now);

        membership.Join(community, requiresApproval: false, Now)
            .Error.Code.Should().Be(CommunityErrorCodes.AlreadyMember);
    }

    // ── The three refusal states ────────────────────────────────────────────────────────────

    [Fact]
    public void Leaving_is_the_members_own_decision_and_says_so()
    {
        var membership = Member();

        membership.Leave(Now).IsSuccess.Should().BeTrue();

        membership.State.Should().Be(CommunityMembershipState.Left);
        membership.EndedAt.Should().Be(Now);
        membership.DecisionReason.Should().BeNull("nobody decided this but them");
        membership.DecidedBy.Should().BeNull();
    }

    [Fact]
    public void Revoking_is_the_communitys_decision_and_records_who_and_why()
    {
        var membership = Member();
        var moderator = UserId.New();

        membership.Revoke(moderator, "Repeated breaches of the community rules.", Now)
            .IsSuccess.Should().BeTrue();

        membership.State.Should().Be(CommunityMembershipState.Revoked);
        membership.DecidedBy.Should().Be(moderator);
        membership.DecisionReason.Should().Be("Repeated breaches of the community rules.");
    }

    [Fact]
    public void Declining_a_request_is_neither_of_the_above()
    {
        var (community, membership) = Following();
        membership.Join(community, requiresApproval: true, Now);

        membership.Decline(UserId.New(), "This community is for alumni only.")
            .IsSuccess.Should().BeTrue();

        membership.State.Should().Be(CommunityMembershipState.Declined);
        membership.JoinedAt.Should().BeNull("they never got in");
    }

    [Fact]
    public void A_revoked_member_cannot_rejoin_by_asking()
    {
        // The reason the row is kept rather than deleted: leaving and coming back must not launder
        // a revocation.
        var membership = Member();
        var community = Opened();
        membership.Revoke(UserId.New(), "Removed.", Now);

        membership.Join(community, requiresApproval: false, Now)
            .Error.Code.Should().Be(CommunityErrorCodes.MembershipRevoked);
    }

    [Fact]
    public void Somebody_who_left_may_rejoin()
    {
        // Leaving is not a punishment, and the row keeps the history so the community can tell a
        // returning member from a new one.
        var membership = Member();
        var community = Opened();
        membership.Leave(Now);

        membership.Join(community, requiresApproval: false, Now.AddDays(30)).IsSuccess.Should().BeTrue();

        membership.State.Should().Be(CommunityMembershipState.Member);
        membership.JoinedAt.Should().Be(Now.AddDays(30));
        membership.EndedAt.Should().BeNull();
    }

    // ── Approval ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Approving_a_request_makes_them_a_member_from_that_moment()
    {
        var (community, membership) = Following();
        membership.Join(community, requiresApproval: true, Now);

        membership.Approve(UserId.New(), Now.AddHours(6)).IsSuccess.Should().BeTrue();

        membership.State.Should().Be(CommunityMembershipState.Member);
        membership.JoinedAt.Should().Be(Now.AddHours(6), "they joined when they were let in");
    }

    [Fact]
    public void Approving_somebody_who_never_asked_is_refused()
    {
        Member().Approve(UserId.New(), Now).Error.Code.Should().Be(CommunityErrorCodes.NotAMember);
    }

    [Fact]
    public void Verification_is_a_statement_about_somebody_who_already_belongs()
    {
        var membership = Member();

        membership.Verify().IsSuccess.Should().BeTrue();
        membership.State.Should().Be(CommunityMembershipState.VerifiedMember);
        membership.IsCurrentMember.Should().BeTrue();

        var follower = Following().Membership;
        follower.Verify().Error.Code.Should().Be(CommunityErrorCodes.NotAMember);
    }

    // ── Primary ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_primary_community_must_be_one_you_belong_to()
    {
        // Otherwise a profile could lead with a community the person has no standing in.
        Following().Membership.MakePrimary()
            .Error.Code.Should().Be(CommunityErrorCodes.PrimaryRequiresMembership);

        Member().MakePrimary().IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Leaving_clears_primary()
    {
        // A primary community you have left is a claim on your profile you did not make.
        var membership = Member();
        membership.MakePrimary();

        membership.Leave(Now);

        membership.IsPrimary.Should().BeFalse();
    }

    [Fact]
    public void Being_revoked_clears_primary_too()
    {
        var membership = Member();
        membership.MakePrimary();

        membership.Revoke(UserId.New(), "Removed.", Now);

        membership.IsPrimary.Should().BeFalse();
    }

    [Fact]
    public void Clearing_a_primary_that_is_not_set_is_not_an_error()
    {
        // Clearing what is already clear is exactly what was asked for.
        var membership = Member();

        membership.ClearPrimary();

        membership.IsPrimary.Should().BeFalse();
    }

    // -- Stepping back to no standing -----------------------------------------------------

    [Fact]
    public void Somebody_who_left_may_follow_without_rejoining()
    {
        var membership = Member();
        membership.Leave(Now);

        membership.Restart(CommunityMembershipState.Following).IsSuccess.Should().BeTrue();

        membership.State.Should().Be(CommunityMembershipState.Following);
        membership.EndedAt.Should().BeNull("the row is no longer an ended one");
    }

    [Fact]
    public void Restarting_clears_a_refusals_reason()
    {
        // The client renders stateReason verbatim. "This community is for alumni only" under a
        // Following badge is a sentence nobody meant to write.
        var (community, membership) = Following();
        membership.Join(community, requiresApproval: true, Now);
        membership.Decline(UserId.New(), "This community is for alumni only.");

        membership.Restart(CommunityMembershipState.Interested);

        membership.DecisionReason.Should().BeNull();
        membership.DecidedBy.Should().BeNull();
    }

    [Fact]
    public void A_member_is_never_quietly_demoted_to_a_follower()
    {
        // The client sends the act, not the target state. Reading "follow" as "become a follower"
        // would remove somebody from their own community.
        Member().Restart(CommunityMembershipState.Following)
            .Error.Code.Should().Be(CommunityErrorCodes.AlreadyMember);
    }

    [Fact]
    public void Somebody_waiting_on_approval_does_not_lose_their_place()
    {
        var (community, membership) = Following();
        membership.Join(community, requiresApproval: true, Now);

        membership.Restart(CommunityMembershipState.Following)
            .Error.Code.Should().Be(CommunityErrorCodes.AlreadyMember);
    }

    [Fact]
    public void A_revocation_cannot_be_laundered_by_following()
    {
        var membership = Member();
        membership.Revoke(UserId.New(), "Removed.", Now);

        membership.Restart(CommunityMembershipState.Following)
            .Error.Code.Should().Be(CommunityErrorCodes.MembershipRevoked);
    }

    [Theory]
    [InlineData(CommunityMembershipState.Member)]
    [InlineData(CommunityMembershipState.VerifiedMember)]
    [InlineData(CommunityMembershipState.PendingApproval)]
    public void Restart_cannot_mint_a_standing(CommunityMembershipState state)
    {
        var membership = Member();
        membership.Leave(Now);

        membership.Restart(state).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Removing_an_interest_and_unfollowing_are_the_same_step_back()
    {
        // The client has two verbs for it; the domain has one transition.
        var (community, membership) = Following();
        membership.Unfollow().IsSuccess.Should().BeTrue();
        membership.State.Should().Be(CommunityMembershipState.None);

        var interested = CommunityMembership.Start(
            CommunityMembershipId.New(), community.Id, UserId.New(),
            CommunityMembershipState.Interested).Value;

        interested.Unfollow().IsSuccess.Should().BeTrue();
        interested.State.Should().Be(CommunityMembershipState.None);
    }

    [Fact]
    public void A_member_cannot_unfollow_their_way_out_of_a_community()
    {
        // Leaving records a departure and moves the count. Unfollow does neither.
        Member().Unfollow().Error.Code.Should().Be(CommunityErrorCodes.NotAMember);
    }

    // -- Verification -----------------------------------------------------------------------

    [Fact]
    public void Verification_records_when_it_happened()
    {
        // The client shows "verified member since"; a verified state with no date cannot say it.
        var membership = Member();

        membership.Verify(Now.AddDays(3));

        membership.VerifiedAt.Should().Be(Now.AddDays(3));
    }

    // -- Visibility -------------------------------------------------------------------------

    [Fact]
    public void A_new_membership_publishes_nothing()
    {
        // §41: the member opts in to being visible rather than out of it, and the enum's zero is
        // Private so a path that forgets this field fails closed.
        Member().Visibility.Should().Be(CommunityMembershipVisibility.Private);
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

    private static CommunityMembership Member()
    {
        var (community, membership) = Following();
        membership.Join(community, requiresApproval: false, Now);
        return membership;
    }
}
