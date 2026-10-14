using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Analytics;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Modules.Housing.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Drafts a property.
/// <para>
/// <b>The longest handler in the module, because a home has more that must be true about it than
/// anything else the platform lists.</b> Each check below refuses a listing that would waste
/// somebody's time or mislead them: an unknown accommodation type, an amenity the deployment does
/// not have, a neighbourhood that is not a neighbourhood, a rent with no currency, a gender
/// preference on a whole flat.
/// </para>
/// <para>
/// <b>Always lands as a private draft.</b> Publishing is a separate act with its own permission,
/// its own readiness checks and its own fraud pass; a create that could publish would skip all
/// three.
/// </para>
/// <para>
/// <b>The analytics record is opened here rather than at publication.</b> Views on a draft are
/// still views — a landlord sharing an unlisted link to get a friend's opinion generates them — and
/// a record created later would silently start counting from the wrong moment.
/// </para>
/// </summary>
public sealed class CreateHousingListingCommandHandler
    : ICommandHandler<CreateHousingListingCommand, HousingListingResponse>
{
    private readonly IHousingListingRepository _listings;
    private readonly IHousingAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingAuthorizationService _authorization;
    private readonly HousingCatalogResolver _catalog;
    private readonly HousingNeighborhoodResolver _neighborhoods;
    private readonly HousingProviderResolver _providers;
    private readonly HousingSlugAllocator _slugs;
    private readonly HousingMediaClaimer _media;
    private readonly HousingListingReadContext _read;
    private readonly IOptions<HousingOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateHousingListingCommandHandler(
        IHousingListingRepository listings,
        IHousingAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        HousingAuthorizationService authorization,
        HousingCatalogResolver catalog,
        HousingNeighborhoodResolver neighborhoods,
        HousingProviderResolver providers,
        HousingSlugAllocator slugs,
        HousingMediaClaimer media,
        HousingListingReadContext read,
        IOptions<HousingOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _listings = listings;
        _analytics = analytics;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _catalog = catalog;
        _neighborhoods = neighborhoods;
        _providers = providers;
        _slugs = slugs;
        _media = media;
        _read = read;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<HousingListingResponse>> Handle(
        CreateHousingListingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var landlord = await _authorization.RequireLandlordAsync(cancellationToken);

        if (landlord.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(landlord.Error);
        }

        var request = command.Request;
        var now = _clock.UtcNow;

        var held = await _listings.CountAgainstListingQuotaAsync(landlord.Value.Id, cancellationToken);

        if (held >= _options.Value.MaxActiveListingsPerLandlord)
        {
            return Result.Failure<HousingListingResponse>(HousingErrors.TooManyListings);
        }

        // Exact-title duplicates within one landlord. Fuzzy matching is the fraud seam's job, where
        // a false positive is a moderator's note rather than a refusal.
        if (await _listings.HasDuplicateAsync(
                landlord.Value.Id, request.Title, excluding: null, cancellationToken))
        {
            return Result.Failure<HousingListingResponse>(HousingErrors.DuplicateListing);
        }

        var type = await _catalog.RequireTypeAsync(
            request.AccommodationTypeKey, cancellationToken);

        if (type.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(type.Error);
        }

        var content = BuildContent(request);

        if (content.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(content.Error);
        }

        var location = await BuildLocationAsync(request, cancellationToken);

        if (location.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(location.Error);
        }

        var listing = HousingListing.Draft(
            HousingListingId.From(_guids.NewSequentialGuid()),
            landlord.Value.Id,
            _authorization.CallerId!,
            content.Value.Title,
            await _slugs.ForListingAsync(request.Title, cancellationToken),
            content.Value.Description,
            type.Value.Key,
            content.Value.Price,
            content.Value.Capacity,
            content.Value.Availability,
            location.Value.Context,
            location.Value.Address,
            content.Value.StayPolicy,
            now);

        if (listing.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(listing.Error);
        }

        var applied = await ApplyDetailAsync(
            listing.Value, request, type.Value.IsShared, cancellationToken);

        if (applied.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(applied.Error);
        }

        await _listings.AddAsync(listing.Value, cancellationToken);

        // Opened now rather than at publication. See the class summary.
        await _analytics.AddAsync(
            HousingAnalytics.Open(
                HousingAnalyticsId.From(_guids.NewSequentialGuid()),
                listing.Value.Id,
                landlord.Value.Id,
                location.Value.Context.CountryCode,
                type.Value.Key,
                location.Value.Context.NeighborhoodAreaId,
                now),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // After the save, so an unclaimed asset is the worst outcome of a Media outage rather than
        // a refused listing.
        await ClaimMediaAsync(listing.Value, request, cancellationToken);

        var response = await _read.ToResponseAsync(listing.Value, cancellationToken);

        return response is null
            ? Result.Failure<HousingListingResponse>(HousingErrors.ListingNotFound)
            : Result.Success(response);
    }

    /// <summary>
    /// Builds the value objects the aggregate is constructed from.
    /// <para>
    /// Separated because they fail independently and a caller sending a bad rent should be told
    /// about the rent rather than about whichever check happened to run first.
    /// </para>
    /// </summary>
    private static Result<ListingContent> BuildContent(CreateHousingListingRequest request)
    {
        var title = HousingTitle.Create(request.Title);

        if (title.IsFailure)
        {
            return Result.Failure<ListingContent>(title.Error);
        }

        var description = HousingDescription.Create(request.Summary, request.Description);

        if (description.IsFailure)
        {
            return Result.Failure<ListingContent>(description.Error);
        }

        var price = BuildPrice(request);

        if (price.IsFailure)
        {
            return Result.Failure<ListingContent>(price.Error);
        }

        var capacity = HousingCapacity.Create(
            request.Bedrooms,
            request.Bathrooms,
            request.MaximumOccupants,
            request.FloorAreaSquareMetres);

        if (capacity.IsFailure)
        {
            return Result.Failure<ListingContent>(capacity.Error);
        }

        var availability = HousingAvailability.Create(
            request.AvailableFrom,
            request.AvailableUntil,
            request.TotalUnits,
            request.MinimumStayNights,
            request.MaximumStayNights);

        if (availability.IsFailure)
        {
            return Result.Failure<ListingContent>(availability.Error);
        }

        var stayPolicy = StayPolicy.Create(
            request.AcceptsShortStay,
            request.AcceptsLongStay,
            request.IsStudentFriendly,
            request.IsFamilyFriendly,
            request.IsSharedAccommodation,
            request.IsLookingForRoommate);

        if (stayPolicy.IsFailure)
        {
            return Result.Failure<ListingContent>(stayPolicy.Error);
        }

        return Result.Success(new ListingContent(
            title.Value,
            description.Value,
            price.Value,
            capacity.Value,
            availability.Value,
            stayPolicy.Value));
    }

    private static Result<HousingPrice> BuildPrice(CreateHousingListingRequest request)
    {
        if (request.RentAmount is null)
        {
            return Result.Success(HousingPrice.OnApplication(
                request.UtilitiesIncluded, request.InternetIncluded));
        }

        if (!Enum.TryParse<RentPeriod>(request.RentPeriod, ignoreCase: true, out var period)
            || period == RentPeriod.None)
        {
            // Names the period, not the rent. See `HousingErrors.PricePeriodRequired`.
            return Result.Failure<HousingPrice>(HousingErrors.PricePeriodRequired);
        }

        return HousingPrice.Create(
            request.RentAmount,
            request.CurrencyCode,
            period,
            request.DepositAmount,
            request.IsNegotiable,
            request.UtilitiesIncluded,
            request.InternetIncluded);
    }

    /// <summary>
    /// Resolves the geography through B4 and builds the address.
    /// <para>
    /// Since 14 Oct 2026 no catalogue node is resolved or written: the property is placed at its
    /// own coordinates, its country and the town the landlord typed.
    /// </para>
    /// </summary>
    private async Task<Result<ListingPlace>> BuildLocationAsync(
        CreateHousingListingRequest request, CancellationToken cancellationToken)
    {
        var context = HousingLocationContext.Create(
            request.CountryCode, request.RegionName, request.CityName);

        if (context.IsFailure)
        {
            return Result.Failure<ListingPlace>(context.Error);
        }

        // ── No catalogue filing — 14 Oct 2026 ────────────────────────────────────────────────
        //
        // A property is stored at its own coordinates, its country and the town the landlord
        // typed, and is found by them. A neighbourhood area id from an old app build is accepted
        // and not written, and the property is no longer filed under the node its pin is nearest:
        // the place catalogue is being retired, and nothing in discovery reads either.
        if (!await _neighborhoods.CountryExistsAsync(request.CountryCode, cancellationToken))
        {
            return Result.Failure<ListingPlace>(HousingErrors.CountryInvalid);
        }

        var placed = context.Value;

        var visibility = Enum.TryParse<AddressVisibility>(
            request.AddressVisibility, ignoreCase: true, out var parsed)
            ? parsed
            : AddressVisibility.NeighborhoodOnly;

        var address = PropertyAddress.Create(
            request.AddressLine1,
            request.AddressLine2,
            request.PostalCode,
            request.Latitude,
            request.Longitude,
            visibility);

        return address.IsFailure
            ? Result.Failure<ListingPlace>(address.Error)
            : Result.Success(new ListingPlace(placed, address.Value));
    }

    /// <summary>Applies the collections and the optional detail the aggregate takes separately.</summary>
    private async Task<Result> ApplyDetailAsync(
        HousingListing listing,
        CreateHousingListingRequest request,
        bool typeIsShared,
        CancellationToken cancellationToken)
    {
        var updated = listing.UpdateContent(
            baseRevision: null,
            _authorization.CallerId,
            leaseType: Parse<LeaseType>(request.LeaseType),
            furnishedStatus: Parse<FurnishedStatus>(request.FurnishedStatus),
            parking: Parse<ParkingAvailability>(request.Parking),
            smokingPolicy: Parse<SmokingPolicy>(request.SmokingPolicy),
            petPolicy: Parse<PetPolicy>(request.PetPolicy),
            requiresViewingBeforeReservation: request.RequiresViewingBeforeReservation);

        if (updated.IsFailure)
        {
            return Result.Failure(updated.Error);
        }

        // The accommodation type can imply shared accommodation even when the landlord did not
        // tick the box — a "Shared Room" is shared by definition, and roommate preferences should
        // be reachable without asking them to say so twice.
        if (typeIsShared && !listing.StayPolicy.IsSharedAccommodation)
        {
            var policy = StayPolicy.Create(
                listing.StayPolicy.AcceptsShortStay,
                listing.StayPolicy.AcceptsLongStay,
                listing.StayPolicy.IsStudentFriendly,
                listing.StayPolicy.IsFamilyFriendly,
                isSharedAccommodation: true,
                listing.StayPolicy.IsLookingForRoommate);

            if (policy.IsSuccess)
            {
                listing.SetStayPolicy(policy.Value, baseRevision: null);
            }
        }

        var amenities = await _catalog.ResolveAmenitiesAsync(request.Amenities, cancellationToken);

        if (amenities.IsFailure)
        {
            return Result.Failure(amenities.Error);
        }

        var applied = listing.SetAmenities(amenities.Value, baseRevision: null);

        if (applied.IsFailure)
        {
            return applied;
        }

        if (request.HouseRules is { Count: > 0 })
        {
            var rules = request.HouseRules
                .Select((rule, index) => HouseRule.Create(
                    _guids.NewSequentialGuid(), rule.Text, index))
                .ToList();

            var setRules = listing.SetHouseRules(rules, baseRevision: null);

            if (setRules.IsFailure)
            {
                return setRules;
            }
        }

        if (request.Media is { Count: > 0 })
        {
            foreach (var media in request.Media)
            {
                var role = Parse<HousingMediaRole>(media.Role) ?? HousingMediaRole.Gallery;
                var attached = listing.AttachMedia(
                    media.MediaId, role, media.Caption, _clock.UtcNow);

                if (attached.IsFailure)
                {
                    return attached;
                }
            }
        }

        var providers = await _providers.ResolveAsync(request.ProviderIds, cancellationToken);

        return providers.Count == 0
            ? Result.Success()
            : listing.SetProviderReferences(providers);
    }

    private async Task ClaimMediaAsync(
        HousingListing listing,
        CreateHousingListingRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Media is not { Count: > 0 })
        {
            return;
        }

        await _media.ClaimManyAsync(
            listing.Id.Value,
            [.. request.Media.Select(media => (media.MediaId, media.Role))],
            cancellationToken);
    }

    private static TEnum? Parse<TEnum>(string? value) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : null;

    private sealed record ListingContent(
        HousingTitle Title,
        HousingDescription Description,
        HousingPrice Price,
        HousingCapacity Capacity,
        HousingAvailability Availability,
        StayPolicy StayPolicy);

    private sealed record ListingPlace(HousingLocationContext Context, PropertyAddress Address);
}
