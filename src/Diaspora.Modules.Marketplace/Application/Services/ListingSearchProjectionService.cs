using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Extensibility.Search;

namespace Diaspora.Modules.Marketplace.Application.Services;

/// <summary>
/// Flattens a listing into the document a search engine ingests.
/// <para>
/// <b>The one place the write model becomes an index document.</b> Every indexing path — the
/// publish handler, the update handler, the reconciliation sweep — goes through here, so a field an
/// engine relies on cannot exist on one path and be missing on another.
/// </para>
/// <para>
/// <b>A listing that is not discoverable projects to null rather than to a document.</b> Callers
/// treat null as "remove it from the index", which is what makes hiding, archiving and selling all
/// de-index through the same call rather than through three remembered special cases.
/// </para>
/// <para>
/// A static class with no state. B6.0.1's lesson: a stateless type in the container is an entry
/// nobody resolves, and CA1822 in Release is what pushes it static.
/// </para>
/// </summary>
internal static class ListingSearchProjectionService
{
    /// <summary>
    /// The document, or null when the listing has no business being in an index.
    /// </summary>
    public static MarketplaceSearchDocument? Project(
        MarketplaceListing listing, SellerProfile seller, string? categoryPath = null)
    {
        ArgumentNullException.ThrowIfNull(listing);
        ArgumentNullException.ThrowIfNull(seller);

        // A suspended seller's listings come out of the index for as long as the suspension lasts,
        // and go back in when it lifts, because neither aggregate's own state changed.
        if (!listing.IsDiscoverable || !seller.CanTrade || listing.PublishedAt is null)
        {
            return null;
        }

        return new MarketplaceSearchDocument
        {
            ListingId = listing.Id.Value,
            SellerId = listing.SellerId.Value,
            Title = listing.Title.Value,
            Slug = listing.Slug.Value,
            Summary = listing.Description.Summary,
            Body = listing.Description.Body,
            CategoryKey = listing.CategoryKey,
            CategoryPath = categoryPath,
            Tags = [.. listing.Tags.Select(tag => tag.Slug)],
            Attributes = listing.Attributes.ToDictionary(
                attribute => attribute.Key,
                attribute => attribute.Value,
                StringComparer.Ordinal),
            Brand = listing.Product.Brand,
            Model = listing.Product.Model,
            Year = listing.Product.Year,
            ListingType = listing.ListingType.ToString(),
            Condition = listing.Condition.ToString(),
            IsPriceDisclosed = listing.Price.IsDisclosed,
            PriceAmount = listing.Price.Amount,
            CurrencyCode = listing.Price.CurrencyCode,
            IsNegotiable = listing.Price.IsNegotiable,
            IsFree = listing.Price.IsFree,
            AvailableQuantity = listing.Inventory.Available,
            CountryCode = listing.Location.CountryCode,
            RegionName = listing.Location.RegionName,
            CityName = listing.Location.CityName,
            Neighbourhood = listing.Location.Neighbourhood,
            AreaPath = listing.Location.AreaPath,
            OffersPickup = listing.Fulfillment.OffersPickup,
            OffersDelivery = listing.Fulfillment.OffersDelivery,
            OffersShipping = listing.Fulfillment.OffersShipping,
            IsSellerVerified = seller.IsVerified,
            SellerType = seller.SellerType.ToString(),
            SellerName = seller.Identity.DisplayName,
            IsFeatured = listing.IsFeatured,
            CoverMediaId = listing.CoverImage?.MediaId,
            ViewCount = listing.Engagement.ViewCount,
            SaveCount = listing.Engagement.SaveCount,
            PublishedAt = listing.PublishedAt.Value,
            ExpiresAt = listing.ExpiresAt
        };
    }

    /// <summary>
    /// The ranking context for one listing, for a strategy to score.
    /// <para>
    /// Built here rather than in the ranking layer because assembling it needs both aggregates, and
    /// a strategy that could reach for either would be one no test could construct in isolation.
    /// </para>
    /// </summary>
    public static Extensibility.Ranking.ListingRankingContext ToRankingContext(
        MarketplaceListing listing,
        SellerProfile? seller,
        DateTimeOffset now,
        double? distanceKm = null,
        double? textScore = null)
    {
        ArgumentNullException.ThrowIfNull(listing);

        var published = listing.PublishedAt ?? listing.CreatedAt;

        return new Extensibility.Ranking.ListingRankingContext
        {
            ListingId = listing.Id.Value,
            AgeHours = Math.Max(0d, (now - published).TotalHours),
            IsSellerVerified = seller?.IsVerified ?? false,
            IsStorefront = seller?.ProviderId is not null,
            IsFeatured = listing.IsFeatured,
            ViewCount = listing.Engagement.ViewCount,
            SaveCount = listing.Engagement.SaveCount,
            OfferCount = listing.Engagement.OfferCount,
            SellerCompletedSales = seller?.Statistics.SoldListings ?? 0,
            SellerCancellationRate = seller?.Statistics.CancellationRate,
            DistanceKm = distanceKm,
            TextScore = textScore,
            HasCoverImage = listing.CoverImage is not null
        };
    }

    /// <summary>The media role a plain photograph attach uses when the caller named none.</summary>
    public static ListingMediaRole DefaultRoleFor(bool isFirst) =>
        isFirst ? ListingMediaRole.Cover : ListingMediaRole.Gallery;
}
