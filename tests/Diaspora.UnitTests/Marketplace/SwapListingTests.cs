using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.UnitTests.Marketplace.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Marketplace;

/// <summary>
/// Swap listings, added 15 Sep 2026.
/// <para>
/// <b>The failure this prevents is a swap nobody can answer.</b> The app's swap screen posted a
/// body the server could not bind and then told the member their swap was listed. A swap is now a
/// listing type of its own, and it cannot go live without saying what the seller would take back —
/// the one thing a member reading it needs to know before they write.
/// </para>
/// </summary>
public sealed class SwapListingTests
{
    [Fact]
    public void A_swap_cannot_go_live_without_saying_what_it_wants()
    {
        var listing = MarketplaceFixtures.PublishableListing(listingType: ListingType.Swap);

        listing.UnmetPublishRequirements.Should().Equal(ListingPublishRequirement.SwapWants);

        var published = listing.Publish(MarketplaceFixtures.Now);

        published.IsFailure.Should().BeTrue();
        published.Error.Description.Should().Contain("what you would take in exchange");
        published.Error.Description.Should().Contain("[missing: wants]");
    }

    [Fact]
    public void Saying_what_it_wants_is_enough_to_publish_an_even_swap()
    {
        var listing = EvenSwap();

        Wants(listing, "A road bike in good condition").IsSuccess.Should().BeTrue();

        listing.UnmetPublishRequirements.Should().BeEmpty(
            "an even swap has no price, and none is asked for");
        listing.SwapWants.Should().Be("A road bike in good condition");
        listing.SwapTopUp.Should().Be(SwapTopUpDirection.None);

        listing.Publish(MarketplaceFixtures.Now).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("seller-adds", SwapTopUpDirection.SellerAdds)]
    [InlineData("buyer-adds", SwapTopUpDirection.BuyerAdds)]
    public void A_swap_with_a_top_up_must_say_how_much(string value, SwapTopUpDirection direction)
    {
        var listing = EvenSwap();

        Wants(listing, "A laptop", topUp: value).IsSuccess.Should().BeTrue();

        listing.SwapTopUp.Should().Be(direction);
        listing.UnmetPublishRequirements.Should().Equal(
            [ListingPublishRequirement.Price],
            "somebody is adding money and the listing does not say how much");
    }

    [Fact]
    public void A_top_up_with_an_amount_is_publishable()
    {
        var listing = MarketplaceFixtures.PublishableListing(
            listingType: ListingType.Swap, price: 40m);

        Wants(listing, "A laptop", topUp: "buyer-adds").IsSuccess.Should().BeTrue();

        listing.UnmetPublishRequirements.Should().BeEmpty();
    }

    [Fact]
    public void A_live_swap_takes_neither_offers_nor_reservations()
    {
        var listing = EvenSwap();
        Wants(listing, "A pram").IsSuccess.Should().BeTrue();
        listing.Publish(MarketplaceFixtures.Now).IsSuccess.Should().BeTrue();

        listing.AcceptsCommitments(MarketplaceFixtures.Now).Should().BeTrue();
        listing.AcceptsOffers(MarketplaceFixtures.Now).Should().BeFalse(
            "a swap is answered with what is offered back, not a sum of money");
        listing.AcceptsReservations(MarketplaceFixtures.Now).Should().BeFalse(
            "a reservation would snapshot the top-up as though it were the item's price");
    }

    [Fact]
    public void Other_listing_types_carry_no_swap_terms()
    {
        var listing = MarketplaceFixtures.PublishableListing(listingType: ListingType.Negotiable);

        Wants(listing, "Anything").IsSuccess.Should().BeTrue();

        listing.SwapWants.Should().BeNull();
        listing.SwapTopUp.Should().BeNull();
        listing.UnmetPublishRequirements.Should().BeEmpty(
            "a stray attribute must not add a requirement to a sale");
    }

    [Theory]
    [InlineData("seller-adds", SwapTopUpDirection.SellerAdds)]
    [InlineData("SellerAdds", SwapTopUpDirection.SellerAdds)]
    [InlineData("buyer_adds", SwapTopUpDirection.BuyerAdds)]
    [InlineData("none", SwapTopUpDirection.None)]
    [InlineData("banana", SwapTopUpDirection.None)]
    [InlineData("7", SwapTopUpDirection.None)]
    [InlineData(null, SwapTopUpDirection.None)]
    public void An_unrecognised_top_up_reads_as_an_even_swap(
        string? value, SwapTopUpDirection expected)
    {
        SwapTerms.ParseTopUp(value).Should().Be(expected);
    }

    [Fact]
    public void The_refusal_token_is_a_plain_word_a_client_can_branch_on()
    {
        MarketplaceErrors.ListingNotPublishableBecause([ListingPublishRequirement.SwapWants])
            .Description.Should().Contain("[missing: wants]");
    }

    /// <summary>A swap draft with a photograph and no price at all.</summary>
    private static MarketplaceListing EvenSwap()
    {
        var listing = MarketplaceListing.Create(
            MarketplaceFixtures.NewListingId(),
            MarketplaceFixtures.NewSellerId(),
            MarketplaceFixtures.NewUserId(),
            ListingTitle.Create("Child's bicycle").Value,
            ListingSlug.Create("childs-bicycle-berlin").Value,
            ListingDescription.Create("Outgrown, rides well. Swap for something bigger.").Value,
            "bicycles",
            ListingType.Swap,
            ProductCondition.Good,
            ListingPrice.Create(null, null, isNegotiable: false, minimumOffer: null).Value,
            ListingLocationContext.Create("DE", cityName: "Berlin").Value,
            MarketplaceFixtures.Now).Value;

        listing.AttachMedia(Guid.NewGuid(), ListingMediaRole.Cover);

        return listing;
    }

    private static Diaspora.SharedKernel.Results.Result Wants(
        MarketplaceListing listing, string wants, string? topUp = null)
    {
        var drafts = new List<ListingAttributeDraft>
        {
            new() { Key = SwapTerms.WantsKey, Value = wants, DisplayName = SwapTerms.WantsLabel },
        };

        if (topUp is not null)
        {
            drafts.Add(new() { Key = SwapTerms.TopUpKey, Value = topUp, DisplayName = SwapTerms.TopUpLabel });
        }

        return listing.SetAttributes(drafts, baseRevision: null);
    }
}
