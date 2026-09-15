using Diaspora.Modules.Guides.Integration;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Attributes;
using Diaspora.Modules.Marketplace.Domain.Catalog;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.Modules.Providers.Integration;

namespace Diaspora.Modules.Marketplace.Application.Mapping;

/// <summary>
/// Turns listing aggregates into wire responses.
/// <para>
/// <b>Two shapes, and the difference is the whole point.</b> <see cref="ToCard"/> renders what a
/// result set needs and touches only the graph the repository's card query loaded;
/// <see cref="ToResponse"/> renders the detail screen. A single mapper would either make cards
/// carry a body nobody reads, or make the detail screen miss half its fields.
/// </para>
/// <para>
/// <b>Three things are hidden from anybody but the owner, and each for a stated reason.</b> The
/// seller's private minimum offer, because a visible floor is just a lower asking price; the SKU,
/// because a shop's internal code means nothing to a buyer and occasionally means something to a
/// competitor; and the offer and reservation counts, because they are a negotiating position.
/// </para>
/// <para>
/// A static class with no state — B6.0.1's lesson about registering stateless types.
/// </para>
/// </summary>
internal static class ListingMapper
{
    /// <summary>What a result set renders.</summary>
    public static ListingCardResponse ToCard(
        MarketplaceListing listing,
        SellerProfile? seller,
        bool? isSaved = null,
        double? distanceKm = null)
    {
        ArgumentNullException.ThrowIfNull(listing);

        return new ListingCardResponse
        {
            ListingId = listing.Id.Value,
            Title = listing.Title.Value,
            Slug = listing.Slug.Value,
            Summary = listing.Description.Summary,
            CategoryKey = listing.CategoryKey,
            ListingType = listing.ListingType.ToString(),
            SwapWants = listing.SwapWants,
            SwapTopUp = listing.SwapTopUp?.ToString(),
            Condition = listing.Condition.ToString(),
            Status = listing.Status.ToString(),
            Price = ToPrice(listing.Price, includeMinimum: false),
            Available = listing.Inventory.Available,
            Location = ToLocation(listing.Location),
            CoverMediaId = listing.CoverImage?.MediaId,
            Tags = [.. listing.Tags.Select(tag => tag.Slug)],
            Seller = seller is null
                ? SellerMapper.UnknownSeller(listing.SellerId.Value)
                : SellerMapper.ToSummary(seller),
            IsFeatured = listing.IsFeatured,
            IsVerified = listing.TrustMark.IsVerified,
            OffersDelivery = listing.Fulfillment.OffersDelivery,
            OffersShipping = listing.Fulfillment.OffersShipping,
            ViewCount = listing.Engagement.ViewCount,
            SaveCount = listing.Engagement.SaveCount,
            PublishedAt = listing.PublishedAt,
            ExpiresAt = listing.ExpiresAt,
            IsSaved = isSaved,
            DistanceKm = distanceKm
        };
    }

    /// <summary>The detail screen.</summary>
    public static ListingResponse ToResponse(
        MarketplaceListing listing,
        SellerProfile? seller,
        DateTimeOffset now,
        bool canManage,
        bool isStaff = false,
        bool? isSaved = null,
        Guid? myOpenOfferId = null,
        Guid? myReservationId = null,
        ListingCategory? category = null,
        IReadOnlyList<ListingAttributeDefinition>? definitions = null,
        IReadOnlyDictionary<Guid, ProviderSummary>? providers = null,
        IReadOnlyDictionary<Guid, GuideSummary>? guides = null)
    {
        ArgumentNullException.ThrowIfNull(listing);

        return new ListingResponse
        {
            ListingId = listing.Id.Value,
            Title = listing.Title.Value,
            Slug = listing.Slug.Value,
            Description = listing.Description.Body,
            Summary = listing.Description.Summary,
            CategoryKey = listing.CategoryKey,
            CategoryName = category?.Name,
            CategoryPath = category?.Path,
            ListingType = listing.ListingType.ToString(),
            SwapWants = listing.SwapWants,
            SwapTopUp = listing.SwapTopUp?.ToString(),
            Condition = listing.Condition.ToString(),
            Status = listing.Status.ToString(),
            Visibility = listing.Visibility.ToString(),
            Price = ToPrice(listing.Price, includeMinimum: canManage),
            Inventory = ToInventory(listing.Inventory),
            Location = ToLocation(listing.Location),
            Fulfillment = ToFulfillment(listing.Fulfillment),
            Product = ToProduct(listing.Product, canManage),
            Attributes = ToAttributes(listing.Attributes, definitions),
            Tags = [.. listing.Tags.Select(tag => tag.Slug)],
            Media = [.. listing.MediaReferences.Select(ToMedia)],
            Providers = [.. listing.ProviderReferences.Select(
                reference => ToProviderReference(reference, providers))],
            Guides = [.. listing.GuideReferences.Select(
                reference => ToGuideReference(reference, guides))],
            Seller = seller is null
                ? SellerMapper.UnknownSeller(listing.SellerId.Value)
                : SellerMapper.ToSummary(seller),
            Engagement = ToEngagement(listing.Engagement, canManage),
            Trust = ToTrust(listing.TrustMark, canManage || isStaff),
            ContactPreference = listing.ContactPreference.ToString(),
            IsFeatured = listing.IsFeatured,
            PublishedAt = listing.PublishedAt,
            ExpiresAt = listing.ExpiresAt,
            SoldAt = listing.SoldAt,
            CreatedAt = listing.CreatedAt,
            Revision = listing.Revision,
            IsAvailable = listing.AcceptsCommitments(now),
            AcceptsOffers = listing.AcceptsOffers(now),
            AcceptsReservations = listing.AcceptsReservations(now),
            CanManage = canManage,
            IsSaved = isSaved,
            MyOpenOfferId = myOpenOfferId,
            MyReservationId = myReservationId,
            ModerationState = canManage || isStaff ? listing.ModerationState.ToString() : null,
            ReportCount = isStaff ? listing.ReportCount : null
        };
    }

    public static ListingPriceResponse ToPrice(ListingPrice price, bool includeMinimum)
    {
        ArgumentNullException.ThrowIfNull(price);

        return new ListingPriceResponse
        {
            IsDisclosed = price.IsDisclosed,
            Amount = price.Amount,
            Currency = price.CurrencyCode,
            IsNegotiable = price.IsNegotiable,
            IsFree = price.IsFree,

            // A visible floor is just a lower asking price. Owner's view only.
            MinimumOffer = includeMinimum ? price.MinimumOffer : null
        };
    }

    public static ListingInventoryResponse ToInventory(ListingInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        return new ListingInventoryResponse
        {
            Quantity = inventory.Quantity,
            Available = inventory.Available,
            Reserved = inventory.Reserved,
            Sold = inventory.Sold,
            IsTracked = inventory.IsTracked
        };
    }

    public static ListingLocationResponse ToLocation(ListingLocationContext location)
    {
        ArgumentNullException.ThrowIfNull(location);

        return new ListingLocationResponse
        {
            CountryCode = location.CountryCode,
            RegionName = location.RegionName,
            CityName = location.CityName,
            Neighbourhood = location.Neighbourhood,
            AreaId = location.AreaId,
            DisplayName = location.DisplayName
        };
    }

    public static ListingFulfillmentResponse ToFulfillment(FulfillmentOptions fulfillment)
    {
        ArgumentNullException.ThrowIfNull(fulfillment);

        return new ListingFulfillmentResponse
        {
            OffersPickup = fulfillment.OffersPickup,
            OffersDelivery = fulfillment.OffersDelivery,
            OffersShipping = fulfillment.OffersShipping,
            IsDigital = fulfillment.IsDigital,
            DeliveryRadiusKm = fulfillment.DeliveryRadiusKm,
            Notes = fulfillment.Notes
        };
    }

    public static ProductIdentityResponse ToProduct(ProductIdentity product, bool includeSku)
    {
        ArgumentNullException.ThrowIfNull(product);

        return new ProductIdentityResponse
        {
            Brand = product.Brand,
            Model = product.Model,
            Sku = includeSku ? product.Sku : null,
            Year = product.Year
        };
    }

    public static ListingTrustResponse ToTrust(ListingTrustMark mark, bool includeReason)
    {
        ArgumentNullException.ThrowIfNull(mark);

        return new ListingTrustResponse
        {
            State = mark.State.ToString(),
            IsVerified = mark.IsVerified,
            Badge = mark.Badge,

            // A refusal reason is how the claimant fixes what was wrong; it is not for buyers.
            Reason = includeReason ? mark.Reason : null,
            VerifiedAt = mark.VerifiedAt
        };
    }

    public static ListingEngagementResponse ToEngagement(
        ListingEngagement engagement, bool includePrivate)
    {
        ArgumentNullException.ThrowIfNull(engagement);

        return new ListingEngagementResponse
        {
            Views = engagement.ViewCount,
            UniqueViews = engagement.UniqueViewCount,
            Saves = engagement.SaveCount,
            Shares = engagement.ShareCount,

            // How many offers are on the table is a negotiating position. Owner's view only.
            Offers = includePrivate ? engagement.OfferCount : null,
            Reservations = includePrivate ? engagement.ReservationCount : null
        };
    }

    public static ListingMediaResponse ToMedia(ListingMediaReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return new ListingMediaResponse
        {
            MediaId = reference.MediaId,
            Role = reference.Role.ToString(),
            Order = reference.Order,
            Caption = reference.Caption
        };
    }

    /// <summary>
    /// Attributes joined to their definitions, so each carries its label, its unit and whether it
    /// is worth rendering as a tappable filter chip. An attribute with no definition still renders,
    /// labelled by its key — a seller volunteering a fact the catalogue has not thought of is
    /// adding information, not making a mistake.
    /// </summary>
    private static IReadOnlyList<ListingAttributeResponse> ToAttributes(
        IReadOnlyList<ListingAttribute> attributes,
        IReadOnlyList<ListingAttributeDefinition>? definitions)
    {
        var byKey = definitions?.ToDictionary(
            definition => definition.Key, StringComparer.Ordinal);

        return
        [
            .. attributes.Select(attribute =>
            {
                var resolved = byKey?.GetValueOrDefault(attribute.Key);

                return new ListingAttributeResponse
                {
                    Key = attribute.Key,
                    Label = attribute.DisplayName ?? resolved?.Label ?? attribute.Key,
                    Value = attribute.Value,
                    Unit = resolved?.Unit,
                    IsFilterable = resolved?.IsFilterable ?? false
                };
            })
        ];
    }

    private static ListingProviderReferenceResponse ToProviderReference(
        ListingProviderReference reference,
        IReadOnlyDictionary<Guid, ProviderSummary>? providers)
    {
        var summary = providers?.GetValueOrDefault(reference.ProviderId);

        return new ListingProviderReferenceResponse
        {
            ProviderId = reference.ProviderId,
            Name = summary?.Name,
            IsVerified = summary?.IsVerified ?? false,
            Role = reference.Role,
            Note = reference.Note
        };
    }

    private static ListingGuideReferenceResponse ToGuideReference(
        ListingGuideReference reference,
        IReadOnlyDictionary<Guid, GuideSummary>? guides)
    {
        var summary = guides?.GetValueOrDefault(reference.GuideId);

        return new ListingGuideReferenceResponse
        {
            GuideId = reference.GuideId,
            Title = summary?.Title ?? reference.Label,
            Slug = summary?.Slug,
            IsAutomatic = reference.IsAutomatic
        };
    }
}
