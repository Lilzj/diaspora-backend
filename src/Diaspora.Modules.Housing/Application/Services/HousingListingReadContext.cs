using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Catalog;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Reservations;

namespace Diaspora.Modules.Housing.Application.Services;

/// <summary>
/// Assembles everything a listing response needs, once, for whichever handler asked.
/// <para>
/// <b>Rendering one property means answering seven questions that live in six places:</b> who the
/// landlord is, what the amenity keys mean, what the accommodation type is called, whether the
/// caller owns it, whether they hold an address-unlocking viewing, whether they already have a
/// reservation, and how much of the address they may see. Every command handler that returns a
/// listing needs all seven, and written out at each call site it would be written correctly at most
/// of them — with the address being the one that matters if it is wrong.
/// </para>
/// <para>
/// <b>Card rendering is separate and deliberately cheaper.</b> A page of twenty cards resolves its
/// landlords and catalogues in two batched reads and asks none of the viewer questions, because a
/// card carries no address and no viewer state.
/// </para>
/// </summary>
public sealed class HousingListingReadContext
{
    private readonly ILandlordProfileRepository _landlords;
    private readonly IHousingReservationRepository _reservations;
    private readonly HousingCatalogResolver _catalog;
    private readonly HousingAddressDisclosureService _disclosure;
    private readonly HousingAuthorizationService _authorization;
    private readonly Platform.Services.Abstractions.IClock _clock;

    public HousingListingReadContext(
        ILandlordProfileRepository landlords,
        IHousingReservationRepository reservations,
        HousingCatalogResolver catalog,
        HousingAddressDisclosureService disclosure,
        HousingAuthorizationService authorization,
        Platform.Services.Abstractions.IClock clock)
    {
        _landlords = landlords;
        _reservations = reservations;
        _catalog = catalog;
        _disclosure = disclosure;
        _authorization = authorization;
        _clock = clock;
    }

    /// <summary>The full detail response for one property.</summary>
    public async Task<HousingListingResponse?> ToResponseAsync(
        HousingListing listing, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listing);

        var landlord = await _landlords.GetForReadAsync(listing.LandlordId, cancellationToken);

        if (landlord is null)
        {
            return null;
        }

        var isOwner = await _authorization.OwnsAsync(listing, cancellationToken);
        var hasViewing = await _disclosure.HasUnlockingViewingAsync(listing, cancellationToken);

        var hasReservation = _authorization.CallerId is { } callerId
            && await _reservations.GetActiveForSeekerAsync(
                listing.Id, callerId, cancellationToken) is not null;

        var address = await _disclosure.DiscloseAsync(
            listing, isOwner, hasViewing, cancellationToken);

        var amenities = await _catalog.AmenityLookupAsync(cancellationToken);
        var types = await _catalog.TypeLookupAsync(cancellationToken);

        return HousingListingMapper.ToResponse(
            listing,
            landlord,
            address,
            amenities,
            types,
            isOwner,
            _authorization.IsStaff,
            hasViewing,
            hasReservation,
            _clock.UtcNow);
    }

    /// <summary>
    /// Cards for a page of properties.
    /// <para>
    /// Two batched reads for the landlords and the catalogues, and no per-listing query at all. A
    /// results page that resolved its landlords one at a time would be twenty round trips to render
    /// twenty names.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<HousingListingCardResponse>> ToCardsAsync(
        IReadOnlyList<HousingListing> listings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listings);

        if (listings.Count == 0)
        {
            return [];
        }

        var landlords = await _landlords.GetManyAsync(
            [.. listings.Select(listing => listing.LandlordId).Distinct()], cancellationToken);

        var amenities = await _catalog.AmenityLookupAsync(cancellationToken);
        var types = await _catalog.TypeLookupAsync(cancellationToken);

        return
        [
            .. listings.Select(listing => HousingListingMapper.ToCard(
                listing,
                landlords.GetValueOrDefault(listing.LandlordId),
                amenities,
                types))
        ];
    }

    /// <summary>The catalogue lookups on their own, for a caller assembling something else.</summary>
    public async Task<(IReadOnlyDictionary<string, HousingAmenity> Amenities,
        IReadOnlyDictionary<string, AccommodationType> Types)> CataloguesAsync(
        CancellationToken cancellationToken = default) =>
        (await _catalog.AmenityLookupAsync(cancellationToken),
            await _catalog.TypeLookupAsync(cancellationToken));
}
