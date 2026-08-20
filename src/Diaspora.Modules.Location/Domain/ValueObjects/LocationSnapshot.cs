using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.ValueObjects;

/// <summary>
/// Where someone or something is, captured as a self-contained record rather than as a
/// reference to a catalogue node.
/// <para>
/// This is the backend form of the frontend's <c>PlatformLocation</c>, and it keeps that
/// type's most important property: it stands alone. A member's current location, a saved
/// "Work", or a listing's address must still render correctly when the catalogue node behind
/// it is renamed, merged or retired — a saved location that turns into "Unknown area" because
/// a boundary was redrawn is a bug the member experiences as data loss.
/// </para>
/// <para>
/// <see cref="AreaId"/> is therefore a link, not the identity: present when the snapshot was
/// resolved against the catalogue and useful for joining, absent when it was not, and never
/// required for the snapshot to be meaningful. That is the difference between a snapshot and
/// a foreign key, and it is deliberate.
/// </para>
/// </summary>
public sealed class LocationSnapshot : ValueObject
{
    public const int MaxNameLength = 160;

    public CountryCode CountryCode { get; private set; } = default!;

    /// <summary>Denormalised so a client can render without resolving the country.</summary>
    public string CountryName { get; private set; } = default!;

    /// <summary>The first-level subdivision, when known. The frontend's <c>state</c>.</summary>
    public string? StateName { get; private set; }

    public string? CityName { get; private set; }

    /// <summary>The frontend calls this <c>neighborhood</c>; module DTOs also call it
    /// <c>areaName</c>. Same value, two names — see naming finding #15.</summary>
    public string? Neighborhood { get; private set; }

    public string? Street { get; private set; }

    /// <summary>An owned navigation, so it cannot bind through the constructor.</summary>
    public GeoCoordinate? Coordinate { get; private set; }

    /// <summary>The catalogue node this was resolved against, when it was.</summary>
    public AreaId? AreaId { get; private set; }

    /// <summary>What the client shows: "Peckham, London".</summary>
    public string DisplayName { get; private set; } = default!;

    // EF Core constructor.
    private LocationSnapshot() { }

    private LocationSnapshot(
        CountryCode countryCode,
        string countryName,
        string? stateName,
        string? cityName,
        string? neighborhood,
        string? street,
        GeoCoordinate? coordinate,
        AreaId? areaId,
        string displayName)
    {
        CountryCode = countryCode;
        CountryName = countryName;
        StateName = stateName;
        CityName = cityName;
        Neighborhood = neighborhood;
        Street = street;
        Coordinate = coordinate;
        AreaId = areaId;
        DisplayName = displayName;
    }

    public static Result<LocationSnapshot> TryCreate(
        CountryCode countryCode,
        string countryName,
        string? stateName = null,
        string? cityName = null,
        string? neighborhood = null,
        string? street = null,
        GeoCoordinate? coordinate = null,
        AreaId? areaId = null,
        string? displayName = null)
    {
        ArgumentNullException.ThrowIfNull(countryCode);

        if (string.IsNullOrWhiteSpace(countryName))
        {
            return Result.Failure<LocationSnapshot>(Error.Validation(
                LocationErrorCodes.CoordinatesInvalid, "A country name is required."));
        }

        var resolvedState = Clean(stateName);
        var resolvedCity = Clean(cityName);
        var resolvedNeighborhood = Clean(neighborhood);

        return Result.Success(new LocationSnapshot(
            countryCode,
            Clean(countryName)!,
            resolvedState,
            resolvedCity,
            resolvedNeighborhood,
            Clean(street),
            coordinate,
            areaId,
            Clean(displayName) ?? BuildDisplayName(
                resolvedNeighborhood, resolvedCity, Clean(countryName)!)));
    }

    /// <summary>
    /// "Peckham, London" when both are known, falling back through city and then country.
    /// <para>
    /// Two components at most. The frontend's own display name is exactly two — a four-part
    /// "Peckham, London, Greater London, United Kingdom" does not fit the chip it renders in.
    /// </para>
    /// </summary>
    private static string BuildDisplayName(string? neighborhood, string? city, string countryName)
    {
        if (neighborhood is not null && city is not null)
        {
            return $"{neighborhood}, {city}";
        }

        if (city is not null)
        {
            return $"{city}, {countryName}";
        }

        return neighborhood is not null ? $"{neighborhood}, {countryName}" : countryName;
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= MaxNameLength ? trimmed : trimmed[..MaxNameLength];
    }

    /// <summary>This snapshot with its coordinate reduced to the disclosed precision.</summary>
    public LocationSnapshot AtPrecision(CoordinatePrecision precision) =>
        Coordinate is null
            ? this
            : new LocationSnapshot(
                CountryCode,
                CountryName,
                StateName,
                CityName,
                Neighborhood,
                // A street is a precise address by definition. Disclosing it beside a rounded
                // coordinate would defeat the rounding entirely.
                precision == CoordinatePrecision.Approximate ? null : Street,
                Coordinate.AtPrecision(precision),
                AreaId,
                DisplayName);

    /// <summary>Whether two snapshots name the same place, for recent-location de-duplication.</summary>
    public bool SamePlaceAs(LocationSnapshot other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (AreaId is not null && other.AreaId is not null)
        {
            return AreaId == other.AreaId;
        }

        return string.Equals(CountryCode.Value, other.CountryCode.Value, StringComparison.Ordinal) &&
               string.Equals(CityName, other.CityName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Neighborhood, other.Neighborhood, StringComparison.OrdinalIgnoreCase);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CountryCode;
        yield return StateName;
        yield return CityName;
        yield return Neighborhood;
        yield return Street;
        yield return Coordinate;
        yield return AreaId;
    }

    public override string ToString() => DisplayName;
}
