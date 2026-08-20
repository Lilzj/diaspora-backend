using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Analytics;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.Modules.Marketplace.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Creates a listing and its analytics rollup.
/// <para>
/// Every step is load-bearing: confirm the caller may sell, hold them to the listing ceiling, refuse
/// an exact duplicate they already have live, validate the category against the catalogue, validate
/// the attributes against the category, allocate a free deep-link handle, and give the listing a
/// rollup to be measured against.
/// </para>
/// <para>
/// The rollup is created here rather than lazily on first view, because "create it if it is
/// missing" is a race two concurrent views will lose — and the row it produces twice violates the
/// one-per-listing constraint.
/// </para>
/// </summary>
public sealed class CreateListingCommandHandler
    : ICommandHandler<CreateListingCommand, ListingResponse>
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly IListingAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly ListingCategoryResolver _categories;
    private readonly ListingAttributeValidator _attributes;
    private readonly ListingSlugAllocator _slugs;
    private readonly MarketplaceGeographicScopeAdapter _geography;
    private readonly IOptions<MarketplaceOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateListingCommandHandler(
        IMarketplaceListingRepository listings,
        IListingAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        ListingCategoryResolver categories,
        ListingAttributeValidator attributes,
        ListingSlugAllocator slugs,
        MarketplaceGeographicScopeAdapter geography,
        IOptions<MarketplaceOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _listings = listings;
        _analytics = analytics;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _categories = categories;
        _attributes = attributes;
        _slugs = slugs;
        _geography = geography;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<ListingResponse>> Handle(
        CreateListingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;

        var seller = await _authorization.RequireSellerAsync(cancellationToken);

        if (seller.IsFailure)
        {
            return Result.Failure<ListingResponse>(seller.Error);
        }

        // An abuse bound rather than an editorial one: it is drafts a script can create for free
        // that are counted, not published listings a person wrote.
        var openCount = await _listings.CountActiveBySellerAsync(
            seller.Value.Id, cancellationToken);

        if (openCount >= _options.Value.MaxActiveListingsPerSeller)
        {
            return Result.Failure<ListingResponse>(MarketplaceErrors.TooManyListings);
        }

        var title = ListingTitle.Create(request.Title);

        if (title.IsFailure)
        {
            return Result.Failure<ListingResponse>(title.Error);
        }

        // An exact repeat of a listing the seller already has live is a double-submit, not a second
        // item. Near-duplicates are a fraud provider's job, where a false positive costs a note
        // rather than a refusal.
        if (await _listings.HasDuplicateAsync(
                seller.Value.Id, title.Value.Value, excluding: null, cancellationToken))
        {
            return Result.Failure<ListingResponse>(MarketplaceErrors.DuplicateListing);
        }

        var description = ListingDescription.Create(request.Description, request.Summary);

        if (description.IsFailure)
        {
            return Result.Failure<ListingResponse>(description.Error);
        }

        var category = await _categories.ResolveActiveAsync(request.CategoryKey, cancellationToken);

        if (category.IsFailure)
        {
            return Result.Failure<ListingResponse>(category.Error);
        }

        var listingType = MarketplaceWireNames.ParseListingType(request.ListingType)
            ?? ListingType.FixedPrice;

        var condition = MarketplaceWireNames.ParseCondition(request.Condition)
            ?? ProductCondition.Good;

        var price = ListingPrice.Create(
            request.Price, request.Currency, request.IsNegotiable, request.MinimumOffer);

        if (price.IsFailure)
        {
            return Result.Failure<ListingResponse>(price.Error);
        }

        var location = ListingLocationContext.Create(
            request.CountryCode,
            request.RegionName,
            request.CityName,
            request.Neighbourhood,
            request.AreaId);

        if (location.IsFailure)
        {
            return Result.Failure<ListingResponse>(location.Error);
        }

        // B4 resolves the area and hands back its materialised path. The listing stores what
        // Location said rather than what the client claimed, which is what makes the prefix filter
        // trustworthy.
        var resolved = await ResolveAreaAsync(location.Value, request.AreaId, cancellationToken);

        var slug = await _slugs.AllocateListingSlugAsync(
            request.Slug, title.Value.Value, resolved.CityName, cancellationToken);

        if (slug.IsFailure)
        {
            return Result.Failure<ListingResponse>(slug.Error);
        }

        var now = _clock.UtcNow;

        var listing = MarketplaceListing.Create(
            MarketplaceListingId.From(_guids.NewSequentialGuid()),
            seller.Value.Id,
            _authorization.CallerId!,
            title.Value,
            slug.Value,
            description.Value,
            category.Value.Key,
            listingType,
            condition,
            price.Value,
            resolved,
            now);

        if (listing.IsFailure)
        {
            return Result.Failure<ListingResponse>(listing.Error);
        }

        var configured = await ConfigureAsync(
            listing.Value, command, category.Value.Path, cancellationToken);

        if (configured.IsFailure)
        {
            return Result.Failure<ListingResponse>(configured.Error);
        }

        await _listings.AddAsync(listing.Value, cancellationToken);

        await _analytics.AddAsync(
            ListingAnalytics.Create(
                ListingAnalyticsId.From(_guids.NewSequentialGuid()),
                listing.Value.Id,
                seller.Value.Id,
                category.Value.Key,
                resolved.CountryCode,
                now),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ListingMapper.ToResponse(
            listing.Value, seller.Value, now, canManage: true, category: category.Value));
    }

    /// <summary>
    /// Applies everything the aggregate takes after construction — stock, fulfilment, product
    /// identity, attributes, tags and the initial photographs.
    /// <para>
    /// Separate from <c>Create</c> because the constructor's job is to produce a valid listing and
    /// this is the seller's optional detail. A failure here fails the whole command, because a
    /// listing created with half the form applied is worse than one the seller has to submit again.
    /// </para>
    /// </summary>
    private async Task<Result> ConfigureAsync(
        MarketplaceListing listing,
        CreateListingCommand command,
        string categoryPath,
        CancellationToken cancellationToken)
    {
        var request = command.Request;

        if (request.Quantity is { } quantity && quantity != 1)
        {
            var stock = listing.SetQuantity(quantity, isTracked: true, baseRevision: null);

            if (stock.IsFailure)
            {
                return stock;
            }
        }

        var fulfillment = FulfillmentOptions.Create(
            request.OffersPickup,
            request.OffersDelivery,
            request.OffersShipping,
            request.IsDigital,
            request.DeliveryRadiusKm,
            request.FulfillmentNotes);

        if (fulfillment.IsFailure)
        {
            return Result.Failure(fulfillment.Error);
        }

        var product = ProductIdentity.Create(
            request.Brand, request.Model, request.Sku, request.Year);

        if (product.IsFailure)
        {
            return Result.Failure(product.Error);
        }

        var details = listing.UpdateDetails(
            listing.Title,
            listing.Description,
            listing.Condition,
            listing.Price,
            listing.Location,
            fulfillment.Value,
            product.Value,
            MarketplaceWireNames.ParseContactPreference(request.ContactPreference)
                ?? ContactPreference.InApp,
            MarketplaceWireNames.ParseVisibility(request.Visibility) ?? ListingVisibility.Public,
            baseRevision: null,
            _authorization.CallerId);

        if (details.IsFailure)
        {
            return details;
        }

        if (request.Attributes is { Count: > 0 })
        {
            var drafts = request.Attributes
                .Select(attribute => new ListingAttributeDraft
                {
                    Key = attribute.Key,
                    Value = attribute.Value,
                    DisplayName = attribute.DisplayName
                })
                .ToList();

            var valid = await _attributes.ValidateAsync(categoryPath, drafts, cancellationToken);

            if (valid.IsFailure)
            {
                return valid;
            }

            var applied = listing.SetAttributes(drafts, baseRevision: null);

            if (applied.IsFailure)
            {
                return applied;
            }
        }

        if (request.Tags is { Count: > 0 })
        {
            var tags = listing.SetTags([.. request.Tags], baseRevision: null);

            if (tags.IsFailure)
            {
                return tags;
            }
        }

        if (request.ExpiresAt is { } expiry)
        {
            var expires = listing.SetExpiry(expiry, _clock.UtcNow, baseRevision: null);

            if (expires.IsFailure)
            {
                return expires;
            }
        }

        // The first photograph becomes the cover, which is what a seller means by uploading it
        // first. Claiming happens in the media handler, after the write commits.
        if (request.MediaIds is { Count: > 0 })
        {
            var index = 0;

            foreach (var mediaId in request.MediaIds)
            {
                var attached = listing.AttachMedia(
                    mediaId, ListingSearchProjectionService.DefaultRoleFor(index == 0));

                if (attached.IsFailure)
                {
                    return attached;
                }

                index++;
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Asks Location to resolve the area the seller picked, and stamps its path onto the listing.
    /// <para>
    /// Fails soft: a listing whose area cannot be resolved keeps the names the seller typed and
    /// simply does not participate in area-scoped narrowing. Refusing the listing because the
    /// geography service was briefly unavailable would be a dependency inverted.
    /// </para>
    /// </summary>
    private async Task<ListingLocationContext> ResolveAreaAsync(
        ListingLocationContext location, Guid? areaId, CancellationToken cancellationToken)
    {
        if (areaId is null && string.IsNullOrWhiteSpace(location.CityName))
        {
            return location;
        }

        var scope = await _geography.ResolveAsync(
            location.CityName,
            location.CountryCode,
            areaId,
            latitude: null,
            longitude: null,
            radiusKm: null,
            _authorization.CallerId,
            cancellationToken);

        if (scope.IsFailure || scope.Value.Path is not { } path)
        {
            return location;
        }

        return location.WithArea(scope.Value.AreaId?.Value ?? areaId, path.Value);
    }
}
