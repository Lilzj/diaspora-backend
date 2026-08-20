using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.UnitTests.Marketplace.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Marketplace;

/// <summary>
/// Stock, which is the invariant the whole commerce side rests on.
/// <para>
/// The rule under test everywhere here is <c>Reserved + Sold &lt;= Quantity</c>. Every way of
/// breaking it is a way of selling one phone to two people.
/// </para>
/// </summary>
public class ListingInventoryTests
{
    private static readonly DateTimeOffset Now = MarketplaceFixtures.Now;

    [Fact]
    public void Available_is_derived_and_never_stored()
    {
        var inventory = ListingInventory.Of(5);

        inventory.Available.Should().Be(5);

        var held = inventory.Hold(2)!;

        held.Available.Should().Be(3);
        held.Reserved.Should().Be(2);
        held.Quantity.Should().Be(5, "holding does not consume stock, only spoken-for-ness");
    }

    /// <summary>
    /// <b>The value object refuses rather than clamps.</b> Clamping would hide a caller's bug while
    /// quietly inventing stock, which is the failure mode that ends with two buyers at one door.
    /// </summary>
    [Fact]
    public void Holding_more_than_is_available_is_refused()
    {
        ListingInventory.Of(2).Hold(3).Should().BeNull();
    }

    [Fact]
    public void Releasing_more_than_is_held_is_refused()
    {
        ListingInventory.Of(5).Hold(1)!.Release(2).Should().BeNull();
    }

    /// <summary>
    /// Settling is the only path from held to sold, which is what stops a completed reservation
    /// double-counting and a cancelled one selling anything.
    /// </summary>
    [Fact]
    public void Settling_converts_held_units_into_sold_units()
    {
        var settled = ListingInventory.Of(3).Hold(2)!.Settle(2)!;

        settled.Reserved.Should().Be(0);
        settled.Sold.Should().Be(2);
        settled.Available.Should().Be(1);
    }

    [Fact]
    public void Settling_more_than_is_held_is_refused()
    {
        ListingInventory.Of(3).Hold(1)!.Settle(2).Should().BeNull();
    }

    /// <summary>
    /// A seller reducing the total below what buyers already hold is correcting the wrong number:
    /// units somebody is holding cannot be un-held by editing a form.
    /// </summary>
    [Fact]
    public void The_total_cannot_be_set_below_what_is_already_committed()
    {
        var held = ListingInventory.Of(5).Hold(3)!;

        held.WithQuantity(2).Should().BeNull();
        held.WithQuantity(3).Should().NotBeNull("three is exactly what is committed");
    }

    /// <summary>
    /// Untracked stock cannot run out. A wanted advert with three interested sellers is not
    /// oversold, and refusing the third would be the module misreading what the listing is.
    /// </summary>
    [Fact]
    public void Untracked_stock_never_refuses_a_hold()
    {
        var untracked = ListingInventory.Untracked;

        untracked.Hold(1).Should().NotBeNull();
        untracked.Hold(50).Should().NotBeNull();
    }

    // ── The aggregate's own stock lifecycle ─────────────────────────────────────────────────

    /// <summary>
    /// Running out of available units moves the listing to Reserved, and a release brings it
    /// straight back. Neither is a command anybody can call.
    /// </summary>
    [Fact]
    public void The_last_held_unit_moves_the_listing_to_reserved_and_a_release_reopens_it()
    {
        var listing = MarketplaceFixtures.ActiveListing();

        listing.HoldStock(1, Now).IsSuccess.Should().BeTrue();
        listing.Status.Should().Be(ListingStatus.Reserved);
        listing.AcceptsCommitments(Now).Should().BeFalse();

        listing.ReleaseStock(1, Now).IsSuccess.Should().BeTrue();
        listing.Status.Should().Be(ListingStatus.Active);
        listing.AcceptsCommitments(Now).Should().BeTrue();
    }

    [Fact]
    public void A_listing_with_stock_left_stays_active_while_units_are_held()
    {
        var listing = MarketplaceFixtures.ActiveListing(quantity: 4);

        listing.HoldStock(2, Now).IsSuccess.Should().BeTrue();

        listing.Status.Should().Be(ListingStatus.Active);
        listing.Inventory.Available.Should().Be(2);
    }

    [Fact]
    public void Settling_the_last_unit_sells_the_listing_out()
    {
        var listing = MarketplaceFixtures.ActiveListing();

        listing.HoldStock(1, Now).IsSuccess.Should().BeTrue();

        listing.SettleStock(1, Now, null, null, 250m, "EUR").IsSuccess.Should().BeTrue();

        listing.Status.Should().Be(ListingStatus.Sold);
        listing.Inventory.IsSoldOut.Should().BeTrue();
        listing.SoldAt.Should().Be(Now);
    }

    /// <summary>
    /// The direct-sale path, which is how most peer-to-peer sales actually happen: two people meet
    /// and the seller records it afterwards.
    /// </summary>
    [Fact]
    public void Marking_sold_with_no_quantity_sells_everything_that_is_left()
    {
        var listing = MarketplaceFixtures.ActiveListing(quantity: 3);

        listing.MarkSold(units: null, Now).IsSuccess.Should().BeTrue();

        listing.Status.Should().Be(ListingStatus.Sold);
        listing.Inventory.Sold.Should().Be(3);
        listing.Inventory.Available.Should().Be(0);
    }

    [Fact]
    public void Marking_more_sold_than_exists_is_refused()
    {
        var listing = MarketplaceFixtures.ActiveListing(quantity: 2);

        var result = listing.MarkSold(units: 3, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("marketplace.stock.insufficient");
    }

    /// <summary>
    /// <b>A hold is not an edit either.</b> A buyer reserving an item must not make the seller's
    /// in-flight save fail.
    /// </summary>
    [Fact]
    public void Holding_stock_does_not_bump_the_revision()
    {
        var listing = MarketplaceFixtures.ActiveListing(quantity: 4);
        var revision = listing.Revision;

        listing.HoldStock(1, Now).IsSuccess.Should().BeTrue();

        listing.Revision.Should().Be(revision);
    }
}
