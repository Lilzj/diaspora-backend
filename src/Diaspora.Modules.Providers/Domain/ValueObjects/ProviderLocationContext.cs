using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.ValueObjects;

/// <summary>
/// Where a provider is, denormalised from Location's catalogue at the moment it was resolved.
/// <para>
/// <b>Every field is nullable and none of them is authoritative geography.</b> Location owns
/// the catalogue; this is a snapshot B7 keeps so a discovery query can filter and a card can
/// render a breadcrumb without a join across a module boundary on every row. The same shape B6
/// settled on for post location, and for the same reason: a feed that resolved every post's
/// area through <c>ILocationDirectory</c> would make one page into a hundred lookups.
/// </para>
/// <para>
/// <see cref="HierarchyPath"/> is the field that earns its place. Location publishes a
/// materialised path per catalogue node, so "every provider inside London" is
/// <c>hierarchy_path LIKE '<i>londonPath</i>%'</c> — containment at any depth, one index, no
/// recursion, and no knowledge in B7 that Peckham is inside Southwark is inside London.
/// </para>
/// <para>
/// A provider with no location at all is legal and common: a fully remote immigration adviser
/// has no premises, and requiring coordinates would make them unregistrable. Discovery treats a
/// null coordinate as "not distance-sortable", never as "excluded".
/// </para>
/// </summary>
public sealed class ProviderLocationContext : ValueObject
{
    private ProviderLocationContext(
        string? countryCode,
        Guid? areaId,
        string? areaName,
        string? cityName,
        double? latitude,
        double? longitude,
        string? hierarchyPath,
        string? addressLine,
        string? postalCode)
    {
        CountryCode = countryCode;
        AreaId = areaId;
        AreaName = areaName;
        CityName = cityName;
        Latitude = latitude;
        Longitude = longitude;
        HierarchyPath = hierarchyPath;
        AddressLine = addressLine;
        PostalCode = postalCode;
    }

    private ProviderLocationContext() { }

    /// <summary>ISO 3166-1 alpha-2, upper case. Location's <c>CountryCode</c> normalised.</summary>
    public string? CountryCode { get; private set; }

    /// <summary>The catalogue node this provider resolved to.</summary>
    public Guid? AreaId { get; private set; }

    /// <summary>The client's <c>areaName</c> — "Peckham". Also its <c>neighborhood</c>.</summary>
    public string? AreaName { get; private set; }

    /// <summary>The client's <c>cityName</c> — "London".</summary>
    public string? CityName { get; private set; }

    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    /// <summary>Location's materialised path for <see cref="AreaId"/>. The containment index.</summary>
    public string? HierarchyPath { get; private set; }

    /// <summary>The client's <c>addressLine</c> — "42 High Street, SE15".</summary>
    public string? AddressLine { get; private set; }

    public string? PostalCode { get; private set; }

    /// <summary>Whether proximity ordering and distance annotation are possible for this row.</summary>
    public bool HasCoordinates => Latitude is not null && Longitude is not null;

    /// <summary>Whether anything at all is known about where this provider is.</summary>
    public bool IsEmpty =>
        CountryCode is null && AreaId is null && CityName is null && !HasCoordinates;

    public static ProviderLocationContext None =>
        new(null, null, null, null, null, null, null, null, null);

    public static ProviderLocationContext Create(
        string? countryCode = null,
        Guid? areaId = null,
        string? areaName = null,
        string? cityName = null,
        double? latitude = null,
        double? longitude = null,
        string? hierarchyPath = null,
        string? addressLine = null,
        string? postalCode = null) =>
        new(
            Normalize(countryCode)?.ToUpperInvariant(),
            areaId,
            Normalize(areaName),
            Normalize(cityName),
            latitude,
            longitude,
            Normalize(hierarchyPath),
            Normalize(addressLine),
            Normalize(postalCode)?.ToUpperInvariant());

    /// <summary>The card subtitle the client builds by hand today: "Peckham, London".</summary>
    public string? Describe() => (AreaName, CityName) switch
    {
        (null, null) => null,
        (null, { } city) => city,
        ({ } area, null) => area,
        ({ } area, { } city) when string.Equals(area, city, StringComparison.OrdinalIgnoreCase) => city,
        ({ } area, { } city) => $"{area}, {city}"
    };

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CountryCode;
        yield return AreaId;
        yield return AreaName;
        yield return CityName;
        yield return Latitude;
        yield return Longitude;
        yield return HierarchyPath;
        yield return AddressLine;
        yield return PostalCode;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
