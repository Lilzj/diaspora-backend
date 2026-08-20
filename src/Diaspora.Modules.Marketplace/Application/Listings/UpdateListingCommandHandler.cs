using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Applies a seller's edit, under the revision they loaded.
/// <para>
/// <b>The optimistic check is the whole reason this is a replacement rather than a patch.</b> The
/// client sends back the revision it rendered, and a save based on a stale one is refused with a
/// conflict it can merge from — rather than silently overwriting whatever somebody else changed in
/// the meantime.
/// </para>
/// </summary>
public sealed class UpdateListingCommandHandler
    : ICommandHandler<UpdateListingCommand, ListingResponse>
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly ListingCategoryResolver _categories;
    private readonly IClock _clock;

    public UpdateListingCommandHandler(
        IMarketplaceListingRepository listings,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        ListingCategoryResolver categories,
        IClock clock)
    {
        _listings = listings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _categories = categories;
        _clock = clock;
    }

    public async Task<Result<ListingResponse>> Handle(
        UpdateListingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;

        var listing = await _listings.GetAsync(
            MarketplaceListingId.From(command.ListingId), cancellationToken);

        if (listing is null)
        {
            return Result.Failure<ListingResponse>(MarketplaceErrors.ListingNotFound);
        }

        var seller = await _authorization.RequireListingAccessAsync(listing, cancellationToken);

        if (seller.IsFailure)
        {
            return Result.Failure<ListingResponse>(seller.Error);
        }

        var title = ListingTitle.Create(request.Title);

        if (title.IsFailure)
        {
            return Result.Failure<ListingResponse>(title.Error);
        }

        var description = ListingDescription.Create(request.Description, request.Summary);

        if (description.IsFailure)
        {
            return Result.Failure<ListingResponse>(description.Error);
        }

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
            request.AreaId,

            // The resolved path is kept from what Location already told the listing. Re-resolving
            // on every edit would make a text change wait on a geography call.
            listing.Location.AreaPath);

        if (location.IsFailure)
        {
            return Result.Failure<ListingResponse>(location.Error);
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
            return Result.Failure<ListingResponse>(fulfillment.Error);
        }

        var product = ProductIdentity.Create(
            request.Brand, request.Model, request.Sku, request.Year);

        if (product.IsFailure)
        {
            return Result.Failure<ListingResponse>(product.Error);
        }

        var updated = listing.UpdateDetails(
            title.Value,
            description.Value,
            MarketplaceWireNames.ParseCondition(request.Condition) ?? listing.Condition,
            price.Value,
            location.Value,
            fulfillment.Value,
            product.Value,
            MarketplaceWireNames.ParseContactPreference(request.ContactPreference)
                ?? listing.ContactPreference,
            MarketplaceWireNames.ParseVisibility(request.Visibility) ?? listing.Visibility,
            request.BaseRevision,
            _authorization.CallerId);

        if (updated.IsFailure)
        {
            return Result.Failure<ListingResponse>(updated.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var category = await _categories.FindAsync(listing.CategoryKey, cancellationToken);

        return Result.Success(ListingMapper.ToResponse(
            listing,
            seller.Value,
            _clock.UtcNow,
            canManage: true,
            _authorization.IsStaff,
            category: category));
    }
}
