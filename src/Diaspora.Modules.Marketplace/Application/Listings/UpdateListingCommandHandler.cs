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
    private readonly ListingAreaFiling _filing;
    private readonly IClock _clock;

    public UpdateListingCommandHandler(
        IMarketplaceListingRepository listings,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        ListingCategoryResolver categories,
        ListingAreaFiling filing,
        IClock clock)
    {
        _listings = listings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _categories = categories;
        _filing = filing;
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

            // The resolved path is kept from what Location already told the listing, while the
            // listing is still where it was. Re-resolving on every edit would make a text change
            // wait on a geography call.
            listing.Location.AreaPath);

        if (location.IsFailure)
        {
            return Result.Failure<ListingResponse>(location.Error);
        }

        // A listing that moved town is no longer where its path says, and one written before
        // 16 Sep 2026 was never filed at all. Either is filed afresh here.
        var placed = location.Value;

        if (MovedFrom(listing.Location, placed))
        {
            placed = placed.WithArea(request.AreaId, areaPath: null);
        }
        else if (placed.AreaId is null && listing.Location.AreaId is not null)
        {
            // An edit form that does not send the area back has not moved the listing. Dropping the
            // id here, as this handler used to, left a row with a path and no area to explain it.
            placed = placed.WithArea(listing.Location.AreaId, placed.AreaPath);
        }

        placed = await _filing.FileAsync(placed, cancellationToken);

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
            placed,
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

        // Omitted coordinates leave the pin alone, so a client that has never sent one cannot
        // clear a pin a newer client set.
        if (request.Latitude is not null || request.Longitude is not null)
        {
            var pinned = listing.SetCoordinates(request.Latitude, request.Longitude);

            if (pinned.IsFailure)
            {
                return Result.Failure<ListingResponse>(pinned.Error);
            }
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

    /// <summary>Whether an edit names a different place from the one the listing is filed in.</summary>
    private static bool MovedFrom(ListingLocationContext current, ListingLocationContext edited) =>
        edited.CountryCode != current.CountryCode
        || !SamePlace(edited.RegionName, current.RegionName)
        || !SamePlace(edited.CityName, current.CityName)
        || !SamePlace(edited.Neighbourhood, current.Neighbourhood)

        // Only a different area is a move. An absent one is a form that did not echo it back.
        || (edited.AreaId is not null && edited.AreaId != current.AreaId);

    private static bool SamePlace(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
}
