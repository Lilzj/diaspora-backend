using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.UnitTests.Marketplace.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Marketplace;

/// <summary>
/// Reservations, and the one rule that matters: every exit settles the stock exactly once.
/// </summary>
public class ReservationWorkflowTests
{
    private static readonly DateTimeOffset Now = MarketplaceFixtures.Now;

    [Fact]
    public void A_new_reservation_is_pending_and_holding()
    {
        var reservation = MarketplaceFixtures.Reservation();

        reservation.Status.Should().Be(ReservationStatus.Pending);
        reservation.IsHoldingStock.Should().BeTrue();
        reservation.History.Should().ContainSingle();
    }

    [Fact]
    public void The_seller_acknowledges_and_the_hand_off_is_expected()
    {
        var seller = MarketplaceFixtures.NewUserId();
        var reservation = MarketplaceFixtures.Reservation();

        reservation.Acknowledge(seller, null, Now, Guid.NewGuid()).IsSuccess.Should().BeTrue();

        reservation.Status.Should().Be(ReservationStatus.AwaitingCollection);
        reservation.AcknowledgedAt.Should().Be(Now);
        reservation.IsHoldingStock.Should().BeTrue("nothing has changed hands yet");
    }

    /// <summary>
    /// Acknowledging is exactly when a seller says "come Saturday between ten and two", so it may
    /// restate the window in the same call.
    /// </summary>
    [Fact]
    public void Acknowledging_may_restate_the_collection_window()
    {
        var reservation = MarketplaceFixtures.Reservation();

        var window = CollectionWindow.Create(
            Now.AddDays(3), Now.AddDays(3).AddHours(4), "The shop, Saturday").Value;

        reservation.Acknowledge(
            MarketplaceFixtures.NewUserId(), window, Now, Guid.NewGuid())
            .IsSuccess.Should().BeTrue();

        reservation.Window.Place.Should().Be("The shop, Saturday");
        reservation.Window.Until.Should().Be(Now.AddDays(3).AddHours(4));
    }

    [Fact]
    public void Completing_closes_it_and_stops_it_holding_stock()
    {
        var reservation = MarketplaceFixtures.Reservation();
        var seller = MarketplaceFixtures.NewUserId();

        reservation.Acknowledge(seller, null, Now, Guid.NewGuid());
        reservation.Complete(seller, Now.AddDays(1), Guid.NewGuid()).IsSuccess.Should().BeTrue();

        reservation.Status.Should().Be(ReservationStatus.Completed);
        reservation.IsHoldingStock.Should().BeFalse();
        reservation.CompletedAt.Should().Be(Now.AddDays(1));
    }

    /// <summary>
    /// <b>Completing straight from Pending is refused.</b> A hand-off the seller never agreed to is
    /// not a hand-off, and allowing it would let a buyer mark themselves as having collected.
    /// </summary>
    [Fact]
    public void Completing_before_the_seller_acknowledges_is_refused()
    {
        var reservation = MarketplaceFixtures.Reservation();

        var result = reservation.Complete(
            MarketplaceFixtures.NewUserId(), Now, Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("marketplace.reservation.transition-invalid");
    }

    /// <summary>
    /// Cancelling is available from both live states to both parties, and is never withheld — a
    /// reservation nobody could cancel would hold stock hostage until its window lapsed.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cancelling_is_available_from_either_live_state(bool acknowledgeFirst)
    {
        var reservation = MarketplaceFixtures.Reservation();

        if (acknowledgeFirst)
        {
            reservation.Acknowledge(
                MarketplaceFixtures.NewUserId(), null, Now, Guid.NewGuid());
        }

        reservation.Cancel(
            MarketplaceFixtures.NewUserId(), "Changed my mind.", Now, Guid.NewGuid())
            .IsSuccess.Should().BeTrue();

        reservation.Status.Should().Be(ReservationStatus.Cancelled);
        reservation.ClosureReason.Should().Be("Changed my mind.");
        reservation.IsHoldingStock.Should().BeFalse();
    }

    [Fact]
    public void A_closed_reservation_never_reopens()
    {
        var reservation = MarketplaceFixtures.Reservation();

        reservation.Cancel(null, null, Now, Guid.NewGuid()).IsSuccess.Should().BeTrue();

        var reopened = reservation.Acknowledge(
            MarketplaceFixtures.NewUserId(), null, Now, Guid.NewGuid());

        reopened.IsFailure.Should().BeTrue();
        reopened.Error.Code.Should().Be("marketplace.reservation.already-closed");
    }

    /// <summary>
    /// <b>An expiry is nobody's decision, and the history says so.</b> That is what keeps a buyer
    /// who never turned up off the seller's cancellation rate.
    /// </summary>
    [Fact]
    public void An_expiry_is_recorded_with_no_actor()
    {
        var reservation = MarketplaceFixtures.Reservation(until: Now.AddHours(2));

        reservation.Expire(Now, Guid.NewGuid()).IsSuccess.Should().BeTrue();
        reservation.Status.Should().Be(ReservationStatus.Pending, "the window has not closed");

        reservation.Expire(Now.AddHours(3), Guid.NewGuid()).IsSuccess.Should().BeTrue();

        reservation.Status.Should().Be(ReservationStatus.Expired);
        reservation.History[^1].ActorId.Should().BeNull();
    }

    /// <summary>
    /// Every terminal state settles the stock, and there is no fourth exit — a fourth would be units
    /// that are neither on the market nor accounted for.
    /// </summary>
    [Fact]
    public void There_are_exactly_three_exits_and_all_of_them_are_terminal()
    {
        ReservationStatusTransitions.From(ReservationStatus.Completed).Should().BeEmpty();
        ReservationStatusTransitions.From(ReservationStatus.Cancelled).Should().BeEmpty();
        ReservationStatusTransitions.From(ReservationStatus.Expired).Should().BeEmpty();

        ReservationStatusTransitions.IsHoldingStock(ReservationStatus.Pending)
            .Should().BeTrue();
        ReservationStatusTransitions.IsHoldingStock(ReservationStatus.AwaitingCollection)
            .Should().BeTrue();
    }

    /// <summary>
    /// The end-to-end path the reservation handler drives: a hold takes the listing off the market,
    /// and settling it converts the units and sells the listing out.
    /// </summary>
    [Fact]
    public void A_completed_reservation_converts_held_units_into_a_sale()
    {
        var listing = MarketplaceFixtures.ActiveListing();

        listing.HoldStock(1, Now).IsSuccess.Should().BeTrue();
        listing.Status.Should().Be(ListingStatus.Reserved);

        listing.SettleStock(1, Now.AddDays(1), null, null, 250m, "EUR")
            .IsSuccess.Should().BeTrue();

        listing.Status.Should().Be(ListingStatus.Sold);
        listing.Inventory.Sold.Should().Be(1);
        listing.Inventory.Reserved.Should().Be(0);
    }

    /// <summary>
    /// And the cancellation path: the stock goes back and the listing is buyable again, which is
    /// what makes a cancellation harmless to everybody but the buyer who cancelled.
    /// </summary>
    [Fact]
    public void A_cancelled_reservation_puts_the_stock_back_on_the_market()
    {
        var listing = MarketplaceFixtures.ActiveListing();

        listing.HoldStock(1, Now);
        listing.ReleaseStock(1, Now.AddHours(1)).IsSuccess.Should().BeTrue();

        listing.Status.Should().Be(ListingStatus.Active);
        listing.Inventory.Available.Should().Be(1);
        listing.Inventory.Sold.Should().Be(0);
    }
}
