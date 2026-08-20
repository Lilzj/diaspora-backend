using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.CommunityLife;

/// <summary>
/// The arithmetic behind every membership figure an organization shows.
/// <para>
/// The test that earns its keep here is the first one: a newly opened membership raises its status
/// event with the previous status <em>equal</em> to the current one, and a caller that passed that
/// through unchanged would compute a zero movement — so an organization would never count its first
/// member, who is its owner.
/// </para>
/// </summary>
public class MembershipMovementTests
{
    /// <summary>A new active membership counts, which is the case the equal-statuses trap breaks.</summary>
    [Fact]
    public void A_new_active_membership_counts_a_member()
    {
        var movement = MembershipStatusTransitions.Movement(
            fromStatus: null, fromRole: null, MembershipStatus.Active, MembershipRole.Owner);

        movement.Members.Should().Be(1);
        movement.Pending.Should().Be(0);
        movement.Administrators.Should().Be(1);
        movement.Volunteers.Should().Be(1);
    }

    /// <summary>
    /// Passing the equal statuses a new membership arrives with computes nothing, which is exactly
    /// the defect the handler's null exists to avoid.
    /// </summary>
    [Fact]
    public void Equal_statuses_compute_no_movement() =>
        MembershipStatusTransitions.Movement(
                MembershipStatus.Active,
                MembershipRole.Owner,
                MembershipStatus.Active,
                MembershipRole.Owner)
            .IsEmpty.Should().BeTrue();

    /// <summary>A request is pending, not a member.</summary>
    [Fact]
    public void A_request_counts_as_pending_and_not_as_a_member()
    {
        var movement = MembershipStatusTransitions.Movement(
            fromStatus: null, fromRole: null, MembershipStatus.Requested, MembershipRole.Member);

        movement.Members.Should().Be(0);
        movement.Pending.Should().Be(1);
    }

    /// <summary>Approving moves the person out of the queue and into the body in one movement.</summary>
    [Fact]
    public void Approving_moves_pending_to_member()
    {
        var movement = MembershipStatusTransitions.Movement(
            MembershipStatus.Requested,
            MembershipRole.Member,
            MembershipStatus.Active,
            MembershipRole.Member);

        movement.Members.Should().Be(1);
        movement.Pending.Should().Be(-1);
    }

    /// <summary>Leaving, being removed and lapsing all take the member off the count.</summary>
    [Theory]
    [InlineData(MembershipStatus.Left)]
    [InlineData(MembershipStatus.Removed)]
    [InlineData(MembershipStatus.Lapsed)]
    public void Ending_a_membership_removes_the_member(MembershipStatus outcome)
    {
        var movement = MembershipStatusTransitions.Movement(
            MembershipStatus.Active, MembershipRole.Member, outcome, MembershipRole.Member);

        movement.Members.Should().Be(-1);
    }

    /// <summary>
    /// The role counters are "at least", so an administrator is counted among the volunteers.
    /// That is what makes the number answer "how many people here can work the door", and it is
    /// what the recount does too.
    /// </summary>
    [Fact]
    public void Promoting_a_volunteer_to_administrator_leaves_them_a_volunteer()
    {
        var movement = MembershipStatusTransitions.Movement(
            MembershipStatus.Active,
            MembershipRole.Volunteer,
            MembershipStatus.Active,
            MembershipRole.Administrator);

        movement.Members.Should().Be(0);
        movement.Administrators.Should().Be(1);
        movement.Volunteers.Should().Be(0);
    }

    /// <summary>
    /// A role change on a membership that has not been accepted moves nothing. An invitation names
    /// a role, but nobody who has not joined belongs in a body's administrator count.
    /// </summary>
    [Fact]
    public void A_role_change_on_an_invitation_moves_no_counter() =>
        MembershipStatusTransitions.Movement(
                MembershipStatus.Invited,
                MembershipRole.Member,
                MembershipStatus.Invited,
                MembershipRole.Administrator)
            .IsEmpty.Should().BeTrue();

    /// <summary>An ordinary member who leaves takes their volunteer standing with them.</summary>
    [Fact]
    public void An_administrator_who_leaves_clears_both_role_counters()
    {
        var movement = MembershipStatusTransitions.Movement(
            MembershipStatus.Active,
            MembershipRole.Administrator,
            MembershipStatus.Left,
            MembershipRole.Administrator);

        movement.Members.Should().Be(-1);
        movement.Administrators.Should().Be(-1);
        movement.Volunteers.Should().Be(-1);
    }
}
