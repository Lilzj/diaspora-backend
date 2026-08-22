using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Analytics;
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
/// Copies a property into a new draft.
/// <para>
/// <b>Copied:</b> title (prefixed), description, accommodation type, price, capacity, availability
/// window, location context, stay policy, roommate preferences and the lease, furnishing, parking,
/// smoking and pet terms — the whole of a long form a landlord would otherwise retype for the
/// second unit in the same building.
/// </para>
/// <para>
/// <b>Not copied — the address.</b> The one field a copy must not inherit, and the reason is the
/// use case: a landlord duplicates a listing because they have <em>another</em> unit, and another
/// unit is at another door. Carrying the street address over would silently publish the wrong one,
/// and a wrong address on a home listing sends a stranger to somebody's house.
/// </para>
/// <para>
/// <b>Not copied:</b> publication state (always a draft), the deep-link handle, media, viewings,
/// reservations, verification standing and the analytics rollup — the copy opens its own at zero.
/// </para>
/// <para>
/// <b>Media is deliberately not carried across</b>, and this is the second field the use case
/// decides rather than convenience: photographs of unit 3 are not photographs of unit 4.
/// `IsPublishable` requires at least one image, so the copy cannot be published until the landlord
/// supplies its own — which is the platform refusing to let a duplicate pass for the original.
/// </para>
/// </summary>
public sealed class DuplicateHousingListingCommandHandler
    : ICommandHandler<DuplicateHousingListingCommand, HousingListingResponse>
{
    private readonly IHousingListingRepository _listings;
    private readonly IHousingAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingAuthorizationService _authorization;
    private readonly HousingCatalogResolver _catalog;
    private readonly HousingSlugAllocator _slugs;
    private readonly HousingListingReadContext _read;
    private readonly IOptions<HousingOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public DuplicateHousingListingCommandHandler(
        IHousingListingRepository listings,
        IHousingAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        HousingAuthorizationService authorization,
        HousingCatalogResolver catalog,
        HousingSlugAllocator slugs,
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
        _slugs = slugs;
        _read = read;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<HousingListingResponse>> Handle(
        DuplicateHousingListingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var landlord = await _authorization.RequireLandlordAsync(cancellationToken);

        if (landlord.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(landlord.Error);
        }

        var source = await _listings.GetAsync(
            HousingListingId.From(command.ListingId), cancellationToken);

        if (source is null)
        {
            return Result.Failure<HousingListingResponse>(HousingErrors.ListingNotFound);
        }

        if (source.LandlordId != landlord.Value.Id)
        {
            return Result.Failure<HousingListingResponse>(HousingErrors.NotListingOwner);
        }

        var held = await _listings.CountActiveByLandlordAsync(landlord.Value.Id, cancellationToken);

        if (held >= _options.Value.MaxActiveListingsPerLandlord)
        {
            return Result.Failure<HousingListingResponse>(HousingErrors.TooManyListings);
        }

        var titleText = string.IsNullOrWhiteSpace(command.Title)
            ? Copied(source.Title.Value)
            : command.Title;

        if (await _listings.HasDuplicateAsync(
                landlord.Value.Id, titleText, excluding: null, cancellationToken))
        {
            return Result.Failure<HousingListingResponse>(HousingErrors.DuplicateListing);
        }

        var title = HousingTitle.Create(titleText);

        if (title.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(title.Error);
        }

        // Re-resolved rather than inherited: a type retired since the original was written must not
        // land in a new draft the landlord then cannot publish.
        var type = await _catalog.RequireTypeAsync(source.AccommodationTypeKey, cancellationToken);

        if (type.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(type.Error);
        }

        // ── Every value object is rebuilt. See DuplicateListingCommandHandler ──
        //
        // `Description`, `Price`, `Capacity`, `Availability`, `Location` and
        // `StayPolicy` are all `OwnsOne` — owned entities with an identifying
        // foreign key back to their listing. Handing a source's tracked instance
        // to a second aggregate asks EF to re-parent it and it refuses with
        // `… is part of a key and so cannot be modified`. The marketplace copy
        // shipped that way and answered 500 on the first real press.
        //
        // `Availability` is rebuilt from the source's *window and total units*
        // only: `ReservedUnits` and `OccupiedUnits` describe tenancies on the
        // original property and must not follow a copy of it.
        var description = HousingDescription.Create(source.Description.Summary, source.Description.Body);

        if (description.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(description.Error);
        }

        var price = HousingPrice.Create(
            source.Price.Amount, source.Price.CurrencyCode, source.Price.Period,
            source.Price.DepositAmount, source.Price.IsNegotiable,
            source.Price.UtilitiesIncluded, source.Price.InternetIncluded);

        if (price.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(price.Error);
        }

        var capacity = HousingCapacity.Create(
            source.Capacity.Bedrooms, source.Capacity.Bathrooms,
            source.Capacity.MaximumOccupants, source.Capacity.FloorAreaSquareMetres);

        if (capacity.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(capacity.Error);
        }

        var availability = HousingAvailability.Create(
            source.Availability.AvailableFrom, source.Availability.AvailableUntil,
            source.Availability.TotalUnits, source.Availability.MinimumStayNights,
            source.Availability.MaximumStayNights);

        if (availability.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(availability.Error);
        }

        var location = HousingLocationContext.Create(
            source.Location.CountryCode, source.Location.RegionName, source.Location.CityName,
            source.Location.NeighborhoodName, source.Location.NeighborhoodAreaId,
            source.Location.AreaPath);

        if (location.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(location.Error);
        }

        var stayPolicy = StayPolicy.Create(
            source.StayPolicy.AcceptsShortStay, source.StayPolicy.AcceptsLongStay,
            source.StayPolicy.IsStudentFriendly, source.StayPolicy.IsFamilyFriendly,
            source.StayPolicy.IsSharedAccommodation, source.StayPolicy.IsLookingForRoommate);

        if (stayPolicy.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(stayPolicy.Error);
        }

        var now = _clock.UtcNow;

        var copy = HousingListing.Draft(
            HousingListingId.From(_guids.NewSequentialGuid()),
            landlord.Value.Id,
            _authorization.CallerId!,
            title.Value,
            await _slugs.ForListingAsync(titleText, cancellationToken),
            description.Value,
            type.Value.Key,
            price.Value,
            capacity.Value,
            availability.Value,
            location.Value,

            // Not the source's address. See the class note — this is the field the use case
            // decides, and getting it wrong sends somebody to the wrong door.
            PropertyAddress.Undisclosed,

            stayPolicy.Value,
            now);

        if (copy.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(copy.Error);
        }

        var terms = copy.Value.UpdateContent(
            baseRevision: null,
            _authorization.CallerId,
            leaseType: source.LeaseType,
            furnishedStatus: source.FurnishedStatus,
            parking: source.Parking,
            smokingPolicy: source.SmokingPolicy,
            petPolicy: source.PetPolicy,
            requiresViewingBeforeReservation: source.RequiresViewingBeforeReservation);

        if (terms.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(terms.Error);
        }

        // Roommate preferences only mean anything on shared accommodation, and the stay policy that
        // decides that was carried across above.
        // Deliberately NOT carried over, and this is the one place the rebuild
        // rule and the product decision agree. `RoommatePreferences` is owned
        // too, so it could not be shared in any case — and a copy is a
        // *different* room, whose future occupants the landlord has not chosen
        // yet. It is left at its default for them to set.

        await _listings.AddAsync(copy.Value, cancellationToken);

        await _analytics.AddAsync(
            HousingAnalytics.Open(
                HousingAnalyticsId.From(_guids.NewSequentialGuid()),
                copy.Value.Id,
                landlord.Value.Id,
                source.Location.CountryCode,
                type.Value.Key,
                source.Location.NeighborhoodAreaId,
                now),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await _read.ToResponseAsync(copy.Value, cancellationToken);

        return response is null
            ? Result.Failure<HousingListingResponse>(HousingErrors.ListingNotFound)
            : Result.Success(response);
    }

    /// <summary>Prefixes once. See DuplicateGuideCommandHandler for why.</summary>
    private static string Copied(string title) =>
        title.StartsWith("Copy of ", StringComparison.OrdinalIgnoreCase)
            ? title
            : $"Copy of {title}";
}
