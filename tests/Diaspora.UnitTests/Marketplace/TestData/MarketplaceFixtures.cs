using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Offers;
using Diaspora.Modules.Marketplace.Domain.Reservations;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.UnitTests.Marketplace.TestData;

/// <summary>
/// Valid aggregates, built the way the handlers build them.
/// <para>
/// Every factory goes through the real <c>Create</c> and unwraps the result, so a fixture that
/// stopped being valid is a compile-or-throw here rather than a puzzling failure in a test that was
/// checking something else.
/// </para>
/// </summary>
internal static class MarketplaceFixtures
{
    public static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    // Named New* rather than after the types they return: a method called UserId() shadows the
    // UserId type inside this namespace and makes every other reference need qualifying.
    public static UserId NewUserId() => UserId.New();

    public static SellerProfileId NewSellerId() => SellerProfileId.New();

    public static MarketplaceListingId NewListingId() => MarketplaceListingId.New();

    /// <summary>A publishable draft, but for its photograph — which most tests add explicitly.</summary>
    public static MarketplaceListing Listing(
        SellerProfileId? sellerId = null,
        ListingType listingType = ListingType.Negotiable,
        decimal price = 250m,
        string currency = "EUR",
        string countryCode = "DE",
        decimal? minimumOffer = null) =>
        MarketplaceListing.Create(
            NewListingId(),
            sellerId ?? NewSellerId(),
            NewUserId(),
            ListingTitle.Create("Bosch washing machine").Value,
            ListingSlug.Create("bosch-washing-machine-berlin").Value,
            ListingDescription.Create("Six years old, works perfectly. Collection only.").Value,
            "home-appliances",
            listingType,
            ProductCondition.Good,
            ListingPrice.Create(price, currency, listingType == ListingType.Negotiable, minimumOffer)
                .Value,
            ListingLocationContext.Create(countryCode, cityName: "Berlin").Value,
            Now).Value;

    /// <summary>A listing that has cleared every publication gate, including the photograph.</summary>
    public static MarketplaceListing PublishableListing(
        SellerProfileId? sellerId = null,
        ListingType listingType = ListingType.Negotiable,
        decimal price = 250m,
        decimal? minimumOffer = null)
    {
        var listing = Listing(
            sellerId, listingType, price, minimumOffer: minimumOffer);

        listing.AttachMedia(Guid.NewGuid(), ListingMediaRole.Cover);

        return listing;
    }

    /// <summary>A live listing with stock, ready to take offers and reservations.</summary>
    public static MarketplaceListing ActiveListing(
        SellerProfileId? sellerId = null,
        int quantity = 1,
        decimal price = 250m,
        decimal? minimumOffer = null)
    {
        var listing = PublishableListing(sellerId, price: price, minimumOffer: minimumOffer);

        if (quantity != 1)
        {
            listing.SetQuantity(quantity, isTracked: true, baseRevision: null);
        }

        listing.Publish(Now);

        return listing;
    }

    public static SellerProfile Seller(
        UserId? ownerId = null,
        string displayName = "Amara's Electricals",
        SellerKind kind = SellerKind.Individual,
        string countryCode = "DE") =>
        SellerProfile.Create(
            NewSellerId(),
            ownerId ?? NewUserId(),
            SellerIdentity.Create(displayName, null, null, kind).Value,
            ListingSlug.Create("amaras-electricals").Value,
            countryCode,
            Now).Value;

    public static ListingOffer Offer(
        MarketplaceListingId? listingId = null,
        SellerProfileId? sellerId = null,
        UserId? buyerId = null,
        UserId? sellerUserId = null,
        decimal amount = 200m,
        int quantity = 1,
        DateTimeOffset? expiresAt = null) =>
        ListingOffer.Open(
            ListingOfferId.New(),
            listingId ?? NewListingId(),
            sellerId ?? NewSellerId(),
            buyerId ?? NewUserId(),
            sellerUserId ?? NewUserId(),
            OfferTerms.Create(amount, "EUR", quantity, "Would you take this?").Value,
            expiresAt ?? Now.AddDays(3),
            Now,
            Guid.NewGuid()).Value;

    public static ListingReservation Reservation(
        MarketplaceListingId? listingId = null,
        SellerProfileId? sellerId = null,
        UserId? buyerId = null,
        int quantity = 1,
        DateTimeOffset? until = null) =>
        ListingReservation.Open(
            ListingReservationId.New(),
            listingId ?? NewListingId(),
            sellerId ?? NewSellerId(),
            buyerId ?? NewUserId(),
            quantity,
            CollectionWindow.Create(Now, until ?? Now.AddDays(2), "Outside the station").Value,
            Now,
            Guid.NewGuid()).Value;
}
