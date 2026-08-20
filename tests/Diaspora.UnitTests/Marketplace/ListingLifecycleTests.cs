using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.UnitTests.Marketplace.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Marketplace;

/// <summary>
/// The listing lifecycle, which is where most of the module's rules live.
/// </summary>
public class ListingLifecycleTests
{
    private static readonly DateTimeOffset Now = MarketplaceFixtures.Now;

    [Fact]
    public void A_new_listing_is_a_draft_nobody_but_its_seller_can_see()
    {
        var listing = MarketplaceFixtures.Listing();

        listing.Status.Should().Be(ListingStatus.Draft);
        listing.IsDiscoverable.Should().BeFalse();
        listing.AcceptsCommitments(Now).Should().BeFalse();
    }

    /// <summary>
    /// <b>The photograph is a publication gate, and it is the cheapest fraud control the module
    /// has.</b> A listing with no image is one nobody opens and nobody can sanity-check against its
    /// description.
    /// </summary>
    [Fact]
    public void A_listing_with_no_photograph_cannot_be_published()
    {
        var listing = MarketplaceFixtures.Listing();

        listing.IsPublishable.Should().BeFalse("it has no photograph");

        listing.Publish(Now).IsFailure.Should().BeTrue();
        listing.Status.Should().Be(ListingStatus.Draft);
    }

    [Fact]
    public void Publishing_makes_it_available_by_default()
    {
        var listing = MarketplaceFixtures.PublishableListing();

        listing.Publish(Now).IsSuccess.Should().BeTrue();

        listing.Status.Should().Be(ListingStatus.Active);
        listing.IsDiscoverable.Should().BeTrue();
        listing.AcceptsCommitments(Now).Should().BeTrue();
        listing.PublishedAt.Should().Be(Now);
        listing.FirstPublishedAt.Should().Be(Now);
    }

    /// <summary>
    /// Published and Active are genuinely different states. A shop advertising tomorrow's drop
    /// today is doing something ordinary.
    /// </summary>
    [Fact]
    public void Publishing_without_activating_makes_it_visible_but_shut()
    {
        var listing = MarketplaceFixtures.PublishableListing();

        listing.Publish(Now, publishedBy: null, activateImmediately: false)
            .IsSuccess.Should().BeTrue();

        listing.Status.Should().Be(ListingStatus.Published);
        listing.IsDiscoverable.Should().BeTrue("it is advertised");
        listing.AcceptsCommitments(Now).Should().BeFalse("the window has not opened");
    }

    /// <summary>
    /// A giveaway must state zero rather than state nothing, so the card can read "Free" and a
    /// price filter can find it.
    /// </summary>
    [Fact]
    public void A_giveaway_priced_above_zero_is_not_publishable()
    {
        var listing = MarketplaceFixtures.PublishableListing(
            listingType: ListingType.Giveaway, price: 5m);

        listing.IsPublishable.Should().BeFalse();
    }

    [Fact]
    public void A_giveaway_priced_at_zero_publishes_and_reads_as_free()
    {
        var listing = MarketplaceFixtures.PublishableListing(
            listingType: ListingType.Giveaway, price: 0m);

        listing.IsPublishable.Should().BeTrue();
        listing.Price.IsFree.Should().BeTrue();
    }

    /// <summary>
    /// <b>Auction is declared and refused.</b> The value exists so a future auction module has a
    /// name already in the wire contract and the database enum; accepting one today would create a
    /// listing that quietly behaves like a fixed price.
    /// </summary>
    [Fact]
    public void An_auction_listing_is_refused_with_a_reason_the_seller_can_read()
    {
        var result = Diaspora.Modules.Marketplace.Domain.Listings.MarketplaceListing.Create(
            MarketplaceFixtures.NewListingId(),
            MarketplaceFixtures.NewSellerId(),
            MarketplaceFixtures.NewUserId(),
            ListingTitle.Create("Vintage watch").Value,
            ListingSlug.Create("vintage-watch").Value,
            ListingDescription.Create("A watch.").Value,
            "other",
            ListingType.Auction,
            ProductCondition.Good,
            ListingPrice.Fixed(100m, "EUR").Value,
            ListingLocationContext.Create("DE").Value,
            Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("marketplace.listing-type.auction-unsupported");
    }

    /// <summary>
    /// Expiry is reversible, which is why the sweep can run it without asking anybody. Extending
    /// the date brings the listing back with its offers and its saves intact.
    /// </summary>
    [Fact]
    public void Extending_the_expiry_reopens_an_expired_listing()
    {
        var listing = MarketplaceFixtures.ActiveListing();

        listing.SetExpiry(Now.AddDays(1), Now, baseRevision: null).IsSuccess.Should().BeTrue();
        listing.Expire(Now.AddDays(2)).IsSuccess.Should().BeTrue();
        listing.Status.Should().Be(ListingStatus.Expired);

        listing.SetExpiry(Now.AddDays(30), Now.AddDays(2), baseRevision: null)
            .IsSuccess.Should().BeTrue();

        listing.Status.Should().Be(ListingStatus.Active);
    }

    /// <summary>
    /// A sold listing is terminal but for archival. Re-listing is a new listing: reopening this one
    /// would silently rewrite the history of everybody who offered on it.
    /// </summary>
    [Fact]
    public void A_sold_listing_never_goes_back_on_sale()
    {
        ListingStatusTransitions.CanTransition(ListingStatus.Sold, ListingStatus.Active)
            .Should().BeFalse();

        ListingStatusTransitions.CanTransition(ListingStatus.Sold, ListingStatus.Archived)
            .Should().BeTrue();
    }

    /// <summary>
    /// An archived listing returns as a draft, never straight to live — it wants its price and its
    /// stock looked at before anybody can buy from it again.
    /// </summary>
    [Fact]
    public void An_archived_listing_returns_only_as_a_draft()
    {
        ListingStatusTransitions.From(ListingStatus.Archived)
            .Should().BeEquivalentTo([ListingStatus.Draft]);
    }

    /// <summary>
    /// <b>A view is not an edit.</b> Counter mutations deliberately do not bump the revision, or a
    /// stranger opening the page would make the seller's in-flight save fail with a conflict they
    /// could not explain.
    /// </summary>
    [Fact]
    public void Recording_a_view_does_not_invalidate_an_in_flight_edit()
    {
        var listing = MarketplaceFixtures.ActiveListing();
        var revision = listing.Revision;

        listing.RecordView(unique: true);
        listing.ApplySaveDelta(1);

        listing.Revision.Should().Be(revision);
        listing.Engagement.ViewCount.Should().Be(1);
        listing.Engagement.UniqueViewCount.Should().Be(1);
        listing.Engagement.SaveCount.Should().Be(1);
    }

    [Fact]
    public void An_edit_under_a_stale_revision_is_refused_as_a_conflict()
    {
        var listing = MarketplaceFixtures.ActiveListing();

        listing.SetTags(["vintage"], baseRevision: listing.Revision)
            .IsSuccess.Should().BeTrue();

        var stale = listing.SetTags(["modern"], baseRevision: 0);

        stale.IsFailure.Should().BeTrue();
        stale.Error.Code.Should().Be("marketplace.revision-conflict");
    }

    /// <summary>
    /// A second cover replaces the first. Picking a new main photograph is the obvious thing to do,
    /// and an error telling the seller to remove the old one first would be the module being
    /// pedantic about its own data model.
    /// </summary>
    [Fact]
    public void Attaching_a_second_cover_replaces_the_first()
    {
        var listing = MarketplaceFixtures.Listing();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        listing.AttachMedia(first, ListingMediaRole.Cover).IsSuccess.Should().BeTrue();
        listing.AttachMedia(second, ListingMediaRole.Cover).IsSuccess.Should().BeTrue();

        listing.CoverImage!.MediaId.Should().Be(second);
        listing.MediaReferences.Should().ContainSingle();
    }
}
