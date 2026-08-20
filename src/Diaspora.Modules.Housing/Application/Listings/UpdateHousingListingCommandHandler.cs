using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Revises a property.
/// <para>
/// Every field is optional and null means leave it alone, so the aggregate is asked to change only
/// what actually moved and the update event names exactly those fields — which is what lets a
/// consumer notify a watching member about a rent change and stay quiet about a corrected typo.
/// </para>
/// <para>
/// <b>Moving a property to a different neighbourhood is a location change, not a content one.</b>
/// It is applied separately so the new area is resolved through B4 rather than trusted, and so the
/// neighbourhood counts on both the old and the new profile are refreshed by the event handler.
/// </para>
/// </summary>
public sealed class UpdateHousingListingCommandHandler
    : ICommandHandler<UpdateHousingListingCommand, HousingListingResponse>
{
    private readonly IHousingListingRepository _listings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingAuthorizationService _authorization;
    private readonly HousingCatalogResolver _catalog;
    private readonly HousingNeighborhoodResolver _neighborhoods;
    private readonly HousingListingReadContext _read;

    public UpdateHousingListingCommandHandler(
        IHousingListingRepository listings,
        IUnitOfWork unitOfWork,
        HousingAuthorizationService authorization,
        HousingCatalogResolver catalog,
        HousingNeighborhoodResolver neighborhoods,
        HousingListingReadContext read)
    {
        _listings = listings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _catalog = catalog;
        _neighborhoods = neighborhoods;
        _read = read;
    }

    public async Task<Result<HousingListingResponse>> Handle(
        UpdateHousingListingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var listing = await _listings.GetAsync(
            HousingListingId.From(command.ListingId), cancellationToken);

        if (listing is null)
        {
            return Result.Failure<HousingListingResponse>(HousingErrors.ListingNotFound);
        }

        var access = await _authorization.RequireListingAccessAsync(listing, cancellationToken);

        if (access.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(access.Error);
        }

        var request = command.Request;

        var content = BuildContent(listing, request);

        if (content.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(content.Error);
        }

        var updated = listing.UpdateContent(
            request.BaseRevision,
            _authorization.CallerId,
            content.Value.Title,
            content.Value.Description,
            content.Value.Price,
            content.Value.Capacity,
            Parse<LeaseType>(request.LeaseType),
            Parse<FurnishedStatus>(request.FurnishedStatus),
            Parse<ParkingAvailability>(request.Parking),
            Parse<SmokingPolicy>(request.SmokingPolicy),
            Parse<PetPolicy>(request.PetPolicy),
            request.RequiresViewingBeforeReservation);

        if (updated.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(updated.Error);
        }

        if (request.AccommodationTypeKey is { Length: > 0 })
        {
            var type = await _catalog.RequireTypeAsync(
                request.AccommodationTypeKey, cancellationToken);

            if (type.IsFailure)
            {
                return Result.Failure<HousingListingResponse>(type.Error);
            }

            var changed = listing.ChangeAccommodationType(type.Value.Key, baseRevision: null);

            if (changed.IsFailure)
            {
                return Result.Failure<HousingListingResponse>(changed.Error);
            }
        }

        var relocated = await ApplyLocationAsync(listing, request, cancellationToken);

        if (relocated.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(relocated.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await _read.ToResponseAsync(listing, cancellationToken);

        return response is null
            ? Result.Failure<HousingListingResponse>(HousingErrors.ListingNotFound)
            : Result.Success(response);
    }

    /// <summary>
    /// Rebuilds only the value objects the caller actually touched.
    /// <para>
    /// A null here means "unchanged" all the way down to the aggregate, which is what stops an
    /// update that mentioned only the rent from rewriting the description with itself and firing an
    /// event that claims the description moved.
    /// </para>
    /// </summary>
    private static Result<ListingEdit> BuildContent(
        HousingListing listing, UpdateHousingListingRequest request)
    {
        HousingTitle? title = null;

        if (request.Title is { Length: > 0 })
        {
            var parsed = HousingTitle.Create(request.Title);

            if (parsed.IsFailure)
            {
                return Result.Failure<ListingEdit>(parsed.Error);
            }

            title = parsed.Value;
        }

        HousingDescription? description = null;

        if (request.Summary is not null || request.Description is not null)
        {
            var parsed = HousingDescription.Create(
                request.Summary ?? listing.Description.Summary,
                request.Description ?? listing.Description.Body);

            if (parsed.IsFailure)
            {
                return Result.Failure<ListingEdit>(parsed.Error);
            }

            description = parsed.Value;
        }

        var price = BuildPrice(listing, request);

        if (price.IsFailure)
        {
            return Result.Failure<ListingEdit>(price.Error);
        }

        HousingCapacity? capacity = null;

        if (request.Bedrooms is not null
            || request.Bathrooms is not null
            || request.MaximumOccupants is not null
            || request.FloorAreaSquareMetres is not null)
        {
            var parsed = HousingCapacity.Create(
                request.Bedrooms ?? listing.Capacity.Bedrooms,
                request.Bathrooms ?? listing.Capacity.Bathrooms,
                request.MaximumOccupants ?? listing.Capacity.MaximumOccupants,
                request.FloorAreaSquareMetres ?? listing.Capacity.FloorAreaSquareMetres);

            if (parsed.IsFailure)
            {
                return Result.Failure<ListingEdit>(parsed.Error);
            }

            capacity = parsed.Value;
        }

        return Result.Success(new ListingEdit(title, description, price.Value, capacity));
    }

    private static Result<HousingPrice?> BuildPrice(
        HousingListing listing, UpdateHousingListingRequest request)
    {
        var touched = request.RentAmount is not null
            || request.CurrencyCode is not null
            || request.RentPeriod is not null
            || request.DepositAmount is not null
            || request.IsNegotiable is not null
            || request.UtilitiesIncluded is not null
            || request.InternetIncluded is not null;

        if (!touched)
        {
            return Result.Success<HousingPrice?>(null);
        }

        var amount = request.RentAmount ?? listing.Price.Amount;
        var utilities = request.UtilitiesIncluded ?? listing.Price.UtilitiesIncluded;
        var internet = request.InternetIncluded ?? listing.Price.InternetIncluded;

        if (amount is null)
        {
            return Result.Success<HousingPrice?>(
                HousingPrice.OnApplication(utilities, internet));
        }

        var period = Parse<RentPeriod>(request.RentPeriod)
            ?? (listing.Price.Period == RentPeriod.None ? null : listing.Price.Period);

        if (period is null)
        {
            return Result.Failure<HousingPrice?>(HousingErrors.PriceRequired);
        }

        var built = HousingPrice.Create(
            amount,
            request.CurrencyCode ?? listing.Price.CurrencyCode,
            period.Value,
            request.DepositAmount ?? listing.Price.DepositAmount,
            request.IsNegotiable ?? listing.Price.IsNegotiable,
            utilities,
            internet);

        return built.IsFailure
            ? Result.Failure<HousingPrice?>(built.Error)
            : Result.Success<HousingPrice?>(built.Value);
    }

    private async Task<Result> ApplyLocationAsync(
        HousingListing listing,
        UpdateHousingListingRequest request,
        CancellationToken cancellationToken)
    {
        var touchedPlace = request.RegionName is not null
            || request.CityName is not null
            || request.NeighborhoodAreaId is not null;

        var touchedAddress = request.AddressLine1 is not null
            || request.AddressLine2 is not null
            || request.PostalCode is not null
            || request.Latitude is not null
            || request.Longitude is not null
            || request.AddressVisibility is not null;

        if (!touchedPlace && !touchedAddress)
        {
            return Result.Success();
        }

        var context = HousingLocationContext.Create(
            listing.Location.CountryCode,
            request.RegionName ?? listing.Location.RegionName,
            request.CityName ?? listing.Location.CityName,
            listing.Location.NeighborhoodName,
            listing.Location.NeighborhoodAreaId,
            listing.Location.AreaPath);

        if (context.IsFailure)
        {
            return Result.Failure(context.Error);
        }

        var resolved = request.NeighborhoodAreaId is { } areaId
            ? await _neighborhoods.ResolveAsync(context.Value, areaId, cancellationToken)
            : Result.Success(context.Value);

        if (resolved.IsFailure)
        {
            return Result.Failure(resolved.Error);
        }

        PropertyAddress? address = null;

        if (touchedAddress)
        {
            var visibility = Parse<AddressVisibility>(request.AddressVisibility)
                ?? listing.Address.Visibility;

            var built = PropertyAddress.Create(
                request.AddressLine1 ?? listing.Address.Line1,
                request.AddressLine2 ?? listing.Address.Line2,
                request.PostalCode ?? listing.Address.PostalCode,
                request.Latitude ?? listing.Address.Latitude,
                request.Longitude ?? listing.Address.Longitude,
                visibility);

            if (built.IsFailure)
            {
                return Result.Failure(built.Error);
            }

            address = built.Value;
        }

        return listing.ChangeLocation(resolved.Value, address, baseRevision: null);
    }

    private static TEnum? Parse<TEnum>(string? value) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : null;

    private sealed record ListingEdit(
        HousingTitle? Title,
        HousingDescription? Description,
        HousingPrice? Price,
        HousingCapacity? Capacity);
}
