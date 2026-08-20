using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.UnitTests.Housing.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Housing;

/// <summary>
/// The relocation workflow: viewings, then reservations.
/// <para>
/// The two are separate aggregates and the tests here are mostly about why. A viewing is cheap and
/// holds nothing; a reservation is expensive and holds a home. It is precisely because the first is
/// cheap that the second can afford not to be.
/// </para>
/// </summary>
public class HousingRelocationWorkflowTests
{
    private static readonly DateTimeOffset Now = HousingFixtures.Now;

    // ── Viewings ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_new_viewing_is_requested_and_holds_nothing()
    {
        var viewing = HousingFixtures.Viewing();

        viewing.Status.Should().Be(HousingViewingStatus.Requested);
        viewing.IsOpen.Should().BeTrue();
        viewing.UnlocksAddress.Should().BeFalse("nobody has agreed a time yet");
        viewing.History.Should().ContainSingle();
    }

    /// <summary>
    /// Scheduled is enough to unlock the address: a member cannot attend an address they have not
    /// been given, so waiting for completion would make the requirement unsatisfiable.
    /// </summary>
    [Fact]
    public void Scheduling_unlocks_the_address()
    {
        var viewing = HousingFixtures.Viewing();
        var landlord = HousingFixtures.NewUserId();

        viewing.Schedule(landlord, Now.AddDays(3), "At the front door", Now, Guid.NewGuid())
            .IsSuccess.Should().BeTrue();

        viewing.Status.Should().Be(HousingViewingStatus.Scheduled);
        viewing.UnlocksAddress.Should().BeTrue();
        viewing.RespondedAt.Should().Be(Now);
    }

    /// <summary>
    /// The original ask is kept beside the confirmation. A member disputing a no-show needs to point
    /// at the fact that the landlord offered Tuesday when they asked for Saturday.
    /// </summary>
    [Fact]
    public void Scheduling_keeps_the_original_request_time()
    {
        var preferred = Now.AddDays(2);
        var viewing = HousingFixtures.Viewing(preferredAt: preferred);

        viewing.Schedule(
            HousingFixtures.NewUserId(), Now.AddDays(5), null, Now, Guid.NewGuid());

        viewing.Window.PreferredAt.Should().Be(preferred);
        viewing.Window.ConfirmedAt.Should().Be(Now.AddDays(5));
    }

    /// <summary>Only a completed viewing satisfies a landlord's viewing-first requirement.</summary>
    [Fact]
    public void Only_a_completed_viewing_satisfies_the_reservation_requirement()
    {
        var viewing = HousingFixtures.Viewing();
        var landlord = HousingFixtures.NewUserId();

        viewing.Schedule(landlord, Now.AddDays(3), null, Now, Guid.NewGuid());
        viewing.SatisfiesReservationRequirement.Should().BeFalse(
            "a scheduled viewing is a promise, not a visit");

        viewing.Complete(landlord, Now.AddDays(3), Guid.NewGuid());
        viewing.SatisfiesReservationRequirement.Should().BeTrue();
    }

    /// <summary>
    /// A pattern of landlord no-shows is what an advance-fee rental fraud looks like from the
    /// outside, so which party failed is recorded rather than inferred.
    /// </summary>
    [Fact]
    public void A_no_show_records_which_party_failed_to_appear()
    {
        var viewing = HousingFixtures.Viewing();
        var landlord = HousingFixtures.NewUserId();

        viewing.Schedule(landlord, Now.AddDays(1), null, Now, Guid.NewGuid());

        viewing.RecordNoShow(
                viewing.SeekerUserId,
                landlordFailedToAttend: true,
                "Nobody was there.",
                Now.AddDays(1),
                Guid.NewGuid())
            .IsSuccess.Should().BeTrue();

        viewing.LandlordFailedToAttend.Should().BeTrue();
        viewing.Status.Should().Be(HousingViewingStatus.NoShow);
    }

    /// <summary>
    /// Cancelling must never be blocked, or somebody stands outside a door.
    /// </summary>
    [Fact]
    public void Either_party_may_cancel_at_any_open_point()
    {
        var requested = HousingFixtures.Viewing();
        requested.Cancel(requested.SeekerUserId, "Found somewhere else.", Now, Guid.NewGuid())
            .IsSuccess.Should().BeTrue();

        var scheduled = HousingFixtures.Viewing();
        scheduled.Schedule(
            HousingFixtures.NewUserId(), Now.AddDays(1), null, Now, Guid.NewGuid());

        scheduled.Cancel(null, "Boiler failed.", Now, Guid.NewGuid())
            .IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// A landlord declining promptly is a response, and the record must show it — otherwise a
    /// prompt refusal counts against them exactly as silence does.
    /// </summary>
    [Fact]
    public void Declining_an_unanswered_request_still_counts_as_responding()
    {
        var viewing = HousingFixtures.Viewing();

        viewing.Cancel(
            HousingFixtures.NewUserId(), "Already let.", Now.AddHours(2), Guid.NewGuid());

        viewing.RespondedAt.Should().Be(Now.AddHours(2));
    }

    [Fact]
    public void The_expiry_sweep_is_idempotent_and_quiet()
    {
        var viewing = HousingFixtures.Viewing();

        viewing.Schedule(
            HousingFixtures.NewUserId(), Now.AddDays(1), null, Now, Guid.NewGuid());

        viewing.Expire(Now, Guid.NewGuid()).IsSuccess.Should().BeTrue(
            "the slot has not passed, so nothing happens and nothing fails");

        viewing.Status.Should().Be(HousingViewingStatus.Scheduled);

        viewing.Expire(Now.AddDays(2), Guid.NewGuid()).IsSuccess.Should().BeTrue();
        viewing.Status.Should().Be(HousingViewingStatus.Expired);
    }

    // ── Reservations ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_new_reservation_is_requested_and_holding()
    {
        var reservation = HousingFixtures.Reservation();

        reservation.Status.Should().Be(HousingReservationStatus.Requested);
        reservation.HoldsUnits.Should().BeTrue(
            "units come off the listing the moment a reservation is opened");
    }

    /// <summary>
    /// Accepting is exactly when a landlord says how long they will keep it, so it may restate the
    /// hold in the same call.
    /// </summary>
    [Fact]
    public void Accepting_may_restate_the_hold_expiry()
    {
        var reservation = HousingFixtures.Reservation();

        reservation.Accept(
                HousingFixtures.NewUserId(), Now.AddDays(7), Now, Guid.NewGuid())
            .IsSuccess.Should().BeTrue();

        reservation.Status.Should().Be(HousingReservationStatus.Reserved);
        reservation.Terms.HoldExpiresAt.Should().Be(Now.AddDays(7));
    }

    /// <summary>
    /// The four exits, and the one that does not return units. Every one of them is terminal.
    /// </summary>
    [Theory]
    [InlineData(HousingReservationStatus.Occupied, false)]
    [InlineData(HousingReservationStatus.Cancelled, true)]
    [InlineData(HousingReservationStatus.Declined, true)]
    [InlineData(HousingReservationStatus.Expired, true)]
    public void Every_exit_is_terminal_and_only_occupation_keeps_the_units(
        HousingReservationStatus status, bool returnsUnits)
    {
        HousingReservationStatusTransitions.IsClosed(status).Should().BeTrue();
        HousingReservationStatusTransitions.HoldsUnits(status).Should().BeFalse();
        HousingReservationStatusTransitions.IsAbandoned(status).Should().Be(returnsUnits);
    }

    /// <summary>
    /// A hostel bed taken for tonight never passes through a signed lease, so the path is not a
    /// straight line.
    /// </summary>
    [Fact]
    public void A_reservation_may_go_straight_from_requested_to_occupied()
    {
        var reservation = HousingFixtures.Reservation();

        reservation.RecordOccupied(HousingFixtures.NewUserId(), Now, Guid.NewGuid())
            .IsSuccess.Should().BeTrue();

        reservation.Status.Should().Be(HousingReservationStatus.Occupied);
        reservation.HoldsUnits.Should().BeFalse();
    }

    /// <summary>
    /// Agreements fall through between signing and moving in more often than at any other point.
    /// </summary>
    [Fact]
    public void A_signed_lease_can_still_be_cancelled()
    {
        var reservation = HousingFixtures.Reservation();
        var actor = HousingFixtures.NewUserId();

        reservation.Accept(actor, null, Now, Guid.NewGuid());
        reservation.RecordLeaseSigned(actor, Now.AddDays(1), Guid.NewGuid());

        reservation.HoldsUnits.Should().BeTrue("nobody has moved in yet");

        reservation.Cancel(actor, "Visa refused.", Now.AddDays(2), Guid.NewGuid())
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void A_closed_reservation_refuses_every_further_move()
    {
        var reservation = HousingFixtures.Reservation();
        var actor = HousingFixtures.NewUserId();

        reservation.Cancel(actor, null, Now, Guid.NewGuid());

        reservation.Accept(actor, null, Now, Guid.NewGuid()).IsFailure.Should().BeTrue();
        reservation.RecordOccupied(actor, Now, Guid.NewGuid()).IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// A hold with no end is a home removed from the market by somebody who stopped replying.
    /// </summary>
    [Fact]
    public void A_lapsed_hold_expires_and_an_unlapsed_one_does_not()
    {
        var reservation = HousingFixtures.Reservation(holdExpiresAt: Now.AddDays(3));

        reservation.HasLapsed(Now.AddDays(1)).Should().BeFalse();
        reservation.HasLapsed(Now.AddDays(4)).Should().BeTrue();

        reservation.Expire(Now.AddDays(4), Guid.NewGuid()).IsSuccess.Should().BeTrue();
        reservation.Status.Should().Be(HousingReservationStatus.Expired);
    }

    [Fact]
    public void The_history_is_append_only_and_ordered()
    {
        var reservation = HousingFixtures.Reservation();
        var actor = HousingFixtures.NewUserId();

        reservation.Accept(actor, null, Now.AddHours(1), Guid.NewGuid());
        reservation.RecordOccupied(actor, Now.AddDays(1), Guid.NewGuid());

        reservation.History.Should().HaveCount(3);
        reservation.History.Select(entry => entry.Status).Should().ContainInOrder(
            HousingReservationStatus.Requested,
            HousingReservationStatus.Reserved,
            HousingReservationStatus.Occupied);
    }
}
