using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.UnitTests.Housing.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Housing;

/// <summary>
/// Unit accounting, and the one invariant the whole module is built to protect: no exit ever loses
/// a unit.
/// <para>
/// A unit that is neither on the market nor lived in is a home the platform can never let again,
/// and nothing in the product would surface it — the listing would simply stop attracting
/// reservations and nobody would know why.
/// </para>
/// </summary>
public class HousingAvailabilityTests
{
    private static readonly DateTimeOffset Now = HousingFixtures.Now;

    [Fact]
    public void A_single_unit_listing_degenerates_to_a_boolean()
    {
        var availability = HousingAvailability.Immediate;

        availability.IsSingleUnit.Should().BeTrue();
        availability.AvailableUnits.Should().Be(1);
        availability.HasUnitsRemaining.Should().BeTrue();
    }

    /// <summary>
    /// The reason the counter exists. A hostel is not a flat, and modelling only the flat would
    /// force twelve identical listings for one dormitory.
    /// </summary>
    [Fact]
    public void A_hostel_holds_many_units_on_one_listing()
    {
        var availability = HousingAvailability.Create(totalUnits: 12).Value;

        var held = availability.TryHold(3).Value;

        held.ReservedUnits.Should().Be(3);
        held.AvailableUnits.Should().Be(9);
        held.IsFullyCommitted.Should().BeFalse();
    }

    [Fact]
    public void Holding_the_last_unit_makes_the_listing_fully_committed()
    {
        var availability = HousingAvailability.Create(totalUnits: 2).Value;

        var held = availability.TryHold(2).Value;

        held.IsFullyCommitted.Should().BeTrue();
        held.HasUnitsRemaining.Should().BeFalse();
    }

    [Fact]
    public void Holding_more_than_remains_is_refused()
    {
        var availability = HousingAvailability.Create(totalUnits: 2).Value;

        availability.TryHold(3).IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// The invariant. Held to occupied to vacated to free, and the total never changes.
    /// </summary>
    [Fact]
    public void Every_transition_conserves_the_total()
    {
        var availability = HousingAvailability.Create(totalUnits: 4).Value;

        var held = availability.TryHold(3).Value;
        var occupied = held.Occupy(2);
        var released = occupied.Release(1);
        var vacated = released.Vacate(1);

        Total(held).Should().Be(4);
        Total(occupied).Should().Be(4);
        Total(released).Should().Be(4);
        Total(vacated).Should().Be(4);
    }

    /// <summary>
    /// Releasing more than is held is clamped rather than refused. A release that could fail would
    /// leave the units held forever, which is the failure this method exists to prevent.
    /// </summary>
    [Fact]
    public void Releasing_more_than_is_held_clamps_rather_than_going_negative()
    {
        var availability = HousingAvailability.Create(totalUnits: 2).Value
            .TryHold(1).Value;

        var released = availability.Release(5);

        released.ReservedUnits.Should().Be(0);
        released.AvailableUnits.Should().Be(2);
    }

    /// <summary>
    /// The transition B10 has no equivalent of. A sold bicycle does not come back; a let flat
    /// always does.
    /// </summary>
    [Fact]
    public void A_tenancy_ending_returns_the_unit_to_the_market()
    {
        var availability = HousingAvailability.Create(totalUnits: 1).Value
            .TryHold(1).Value
            .Occupy(1);

        availability.IsFullyOccupied.Should().BeTrue();

        var vacated = availability.Vacate(1);

        vacated.OccupiedUnits.Should().Be(0);
        vacated.AvailableUnits.Should().Be(1);
    }

    /// <summary>
    /// A hostel cannot shrink to four beds while five people are booked into them: the counts would
    /// never reconcile again.
    /// </summary>
    [Fact]
    public void Total_units_cannot_be_reduced_below_what_is_committed()
    {
        var availability = HousingAvailability.Create(totalUnits: 6).Value
            .TryHold(5).Value;

        availability.WithTotalUnits(4).IsFailure.Should().BeTrue();
        availability.WithTotalUnits(5).IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// The calendar seam. Callers ask these two questions rather than reading the dates, so a real
    /// calendar can answer them from a table of dates later without a single caller changing.
    /// </summary>
    [Fact]
    public void Availability_is_asked_rather_than_read()
    {
        var availability = HousingAvailability.Create(
            availableFrom: Now.AddMonths(1),
            minimumStayNights: 7,
            maximumStayNights: 90).Value;

        availability.IsAvailableOn(Now).Should().BeFalse();
        availability.IsAvailableOn(Now.AddMonths(2)).Should().BeTrue();

        availability.AcceptsStay(3).Should().BeFalse("below the minimum stay");
        availability.AcceptsStay(30).Should().BeTrue();
        availability.AcceptsStay(365).Should().BeFalse("above the maximum stay");
    }

    /// <summary>
    /// Booking a June flat in March is the single most common thing a relocating member does, so a
    /// future move-in date must not block a reservation.
    /// </summary>
    [Fact]
    public void A_property_available_in_future_is_still_reservable_now()
    {
        var availability = HousingAvailability.Create(
            availableFrom: Now.AddMonths(3)).Value;

        availability.AcceptsReservationAt(Now).Should().BeTrue();
    }

    /// <summary>
    /// The owned-type trap the platform has carried since B6: a cached static instance shared by
    /// two aggregates makes EF write nulls into one of them.
    /// </summary>
    [Fact]
    public void The_default_availability_is_a_fresh_instance_every_time()
    {
        HousingAvailability.Immediate.Should().NotBeSameAs(HousingAvailability.Immediate);
        PropertyAddress.Undisclosed.Should().NotBeSameAs(PropertyAddress.Undisclosed);
        HousingEngagement.Empty.Should().NotBeSameAs(HousingEngagement.Empty);
        HousingTrustMark.Unverified.Should().NotBeSameAs(HousingTrustMark.Unverified);
    }

    private static int Total(HousingAvailability availability) =>
        availability.AvailableUnits + availability.ReservedUnits + availability.OccupiedUnits;
}
