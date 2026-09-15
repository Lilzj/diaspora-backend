using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;

namespace Diaspora.Modules.Housing.Domain.Listings;

/// <summary>
/// Just enough of a listing to decide where it falls in a "nearest first" search.
/// <para>
/// <b>Coordinates as stored, with the visibility that governs them.</b> Never rendered: the
/// engine turns them into the published coordinate with
/// <see cref="ValueObjects.PropertyAddress.PublicCoordinates"/> before measuring anything, and
/// only listing identifiers leave the engine.
/// </para>
/// </summary>
public sealed record HousingNearestCandidate(
    HousingListingId Id,
    string Slug,
    double? Latitude,
    double? Longitude,
    AddressVisibility Visibility);
