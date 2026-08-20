using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.CommunityLife;

/// <summary>
/// The arithmetic every attendance figure on the platform is built from.
/// <para>
/// These are the tests that would have caught the phase's worst defect: a first response settling
/// twice, so an event with forty responses reports eighty places gone and refuses the forty-first
/// member a seat that exists. The movement is the only place the deltas are computed, so testing it
/// tests the settlement path, the sweep and the cancellation release at once.
/// </para>
/// </summary>
public class RsvpPlaceMovementTests
{
    /// <summary>A first commitment takes its whole party, not one place.</summary>
    [Fact]
    public void A_first_commitment_takes_the_whole_party()
    {
        var movement = RsvpStatusTransitions.Movement(from: null, RsvpStatus.Going, places: 4);

        movement.Going.Should().Be(4);
        movement.Interested.Should().Be(0);
        movement.Waitlisted.Should().Be(0);
        movement.CheckedIn.Should().Be(0);
    }

    /// <summary>
    /// The synthetic previous status a first response arrives with contributes nothing, which is
    /// what lets creation and change travel one code path.
    /// </summary>
    [Fact]
    public void The_synthetic_previous_status_of_a_first_response_takes_nothing_away()
    {
        var synthetic = RsvpStatusTransitions.Movement(
            RsvpStatus.Cancelled, RsvpStatus.Going, places: 4);

        var fresh = RsvpStatusTransitions.Movement(from: null, RsvpStatus.Going, places: 4);

        synthetic.Should().Be(fresh);
    }

    /// <summary>Interest takes no place. An event is not filled by people thinking about it.</summary>
    [Fact]
    public void Interest_takes_no_place()
    {
        var movement = RsvpStatusTransitions.Movement(
            from: null, RsvpStatus.Interested, places: 3);

        movement.Going.Should().Be(0);
        movement.Interested.Should().Be(3);
    }

    /// <summary>
    /// A check-in does not release the place it occupies. Counting the two apart would make an
    /// event's going count fall as the hall filled up.
    /// </summary>
    [Fact]
    public void Checking_in_moves_nobody_out_of_the_going_count()
    {
        var movement = RsvpStatusTransitions.Movement(
            RsvpStatus.Going, RsvpStatus.CheckedIn, places: 2);

        movement.Going.Should().Be(0);
        movement.CheckedIn.Should().Be(2);
    }

    /// <summary>Settling a check-in into an attendance moves nothing: both buckets already hold it.</summary>
    [Fact]
    public void Settling_an_attendance_moves_nothing()
    {
        var movement = RsvpStatusTransitions.Movement(
            RsvpStatus.CheckedIn, RsvpStatus.Attended, places: 2);

        movement.IsEmpty.Should().BeTrue();
    }

    /// <summary>A withdrawal and a no-show release the same place, and differ only in why.</summary>
    [Theory]
    [InlineData(RsvpStatus.Cancelled)]
    [InlineData(RsvpStatus.NoShow)]
    public void Leaving_releases_the_place(RsvpStatus outcome)
    {
        var movement = RsvpStatusTransitions.Movement(RsvpStatus.Going, outcome, places: 5);

        movement.Going.Should().Be(-5);
        movement.CheckedIn.Should().Be(0);
    }

    /// <summary>
    /// A promotion off the waiting list takes a place and gives back a waitlist slot in one
    /// movement, so a freed place cannot be counted twice.
    /// </summary>
    [Fact]
    public void A_promotion_moves_both_counters_at_once()
    {
        var movement = RsvpStatusTransitions.Movement(
            RsvpStatus.Waitlisted, RsvpStatus.Going, places: 2);

        movement.Going.Should().Be(2);
        movement.Waitlisted.Should().Be(-2);
    }

    /// <summary>Standing down from going to interested gives the place back.</summary>
    [Fact]
    public void Stepping_down_to_interest_gives_the_place_back()
    {
        var movement = RsvpStatusTransitions.Movement(
            RsvpStatus.Going, RsvpStatus.Interested, places: 3);

        movement.Going.Should().Be(-3);
        movement.Interested.Should().Be(3);
    }

    /// <summary>A movement to the same status is empty, so a redundant write costs nothing.</summary>
    [Fact]
    public void A_movement_to_the_same_status_is_empty() =>
        RsvpStatusTransitions.Movement(RsvpStatus.Going, RsvpStatus.Going, places: 4)
            .IsEmpty.Should().BeTrue();

    /// <summary>A party of zero or fewer is not a party. Guards against a bad request moving a count.</summary>
    [Fact]
    public void A_party_of_none_moves_nothing() =>
        RsvpStatusTransitions.Movement(from: null, RsvpStatus.Going, places: 0)
            .IsEmpty.Should().BeTrue();

    /// <summary>
    /// The buckets agree with what the recount would produce: a going status holds a place and is
    /// not present, a checked-in status is both.
    /// </summary>
    [Theory]
    [InlineData(RsvpStatus.Going, true, false)]
    [InlineData(RsvpStatus.CheckedIn, true, true)]
    [InlineData(RsvpStatus.Attended, true, true)]
    [InlineData(RsvpStatus.Interested, false, false)]
    [InlineData(RsvpStatus.Waitlisted, false, false)]
    [InlineData(RsvpStatus.Cancelled, false, false)]
    [InlineData(RsvpStatus.NoShow, false, false)]
    public void The_buckets_match_the_recount(
        RsvpStatus status, bool holdsCapacity, bool isPresent)
    {
        RsvpStatusTransitions.HoldsCapacity(status).Should().Be(holdsCapacity);
        RsvpStatusTransitions.IsPresent(status).Should().Be(isPresent);
    }
}
