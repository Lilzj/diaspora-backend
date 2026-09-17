using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;

namespace Diaspora.Modules.Location.Application.Mapping;

/// <summary>
/// Aggregate-to-DTO mapping. Hand-written; the module owns its own edges.
/// <para>
/// Every method takes everything it needs as a parameter and reaches for nothing. That is
/// what makes the mapping testable without a database and, more importantly, what makes the
/// N+1 impossible: a mapper that could fetch a country name would fetch one per row, and
/// nobody would notice until the page had fifty rows on it.
/// </para>
/// </summary>
public static class LocationMapper
{
    public static CountrySummaryResponse ToSummary(Country country)
    {
        ArgumentNullException.ThrowIfNull(country);

        return new CountrySummaryResponse
        {
            Id = country.Id.Value,
            Code = country.Code.Value,
            Name = country.Name.Value,
            Flag = country.Flag,
            CallingCode = country.Metadata.CallingCode?.Value,
            Currency = country.Metadata.Currency?.Value,
            IsSupported = country.IsSupported,
            IsFeatured = country.IsFeatured
        };
    }

    public static CountryMetadataResponse ToMetadata(Country country)
    {
        ArgumentNullException.ThrowIfNull(country);

        return new CountryMetadataResponse
        {
            CallingCode = country.Metadata.CallingCode?.Value,
            Currency = country.Metadata.Currency?.Value,
            PrimaryLanguage = country.Metadata.PrimaryLanguage?.Value,
            DefaultLocale = country.Metadata.DefaultLocale?.Value,
            PrimaryTimeZone = country.Metadata.PrimaryTimeZone?.Value,
            Continent = country.Metadata.Continent,
            Subregion = country.Metadata.Subregion,
            SpokenLanguages = country.SpokenLanguages,
            TimeZones = country.TimeZones
        };
    }

    public static CountryDetailResponse ToDetail(
        Country country, IReadOnlyDictionary<string, int> counts)
    {
        ArgumentNullException.ThrowIfNull(country);
        ArgumentNullException.ThrowIfNull(counts);

        return new CountryDetailResponse
        {
            Country = ToSummary(country),
            Metadata = ToMetadata(country),
            Latitude = country.Center?.Latitude,
            Longitude = country.Center?.Longitude,
            Extent = ToBounds(country.Extent),
            LandmarkImageUrl = country.LandmarkImageUrl,
            Counts = counts
        };
    }

    public static BoundingBoxResponse? ToBounds(BoundingBox? bounds) =>
        bounds is null
            ? null
            : new BoundingBoxResponse
            {
                North = bounds.North,
                South = bounds.South,
                East = bounds.East,
                West = bounds.West
            };

    /// <summary>
    /// A hierarchy node as a list entry.
    /// <para>
    /// <paramref name="ancestors"/> is required rather than optional because the display name
    /// is composed from it. A summary rendered as a bare "Peckham" is ambiguous in exactly the
    /// place it matters most — a search result list where three of them are Peckham.
    /// </para>
    /// </summary>
    public static AreaSummaryResponse ToSummary(
        AdministrativeArea area,
        IReadOnlyList<AdministrativeArea> ancestors,
        Distance? distance = null)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(ancestors);

        return new AreaSummaryResponse
        {
            Id = area.Id.Value,
            Name = area.Name.Value,
            LocalName = area.Name.LocalName,
            Slug = area.Slug.Value,
            Level = area.Level.ToString(),
            CountryCode = area.CountryCode.Value,
            Flag = area.CountryCode.ToFlagEmoji(),
            ParentId = area.ParentId?.Value,
            Path = area.Path.Value,
            Latitude = area.Center?.Latitude,
            Longitude = area.Center?.Longitude,
            DistanceKm = distance?.Kilometers,
            DisplayName = ComposeDisplayName(area, ancestors)
        };
    }

    public static AreaDetailResponse ToDetail(
        AdministrativeArea area,
        IReadOnlyList<AdministrativeArea> ancestors,
        Country country)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(ancestors);
        ArgumentNullException.ThrowIfNull(country);

        return new AreaDetailResponse
        {
            Area = ToSummary(area, ancestors),
            Ancestors = ancestors
                .Select(ancestor => ToSummary(
                    ancestor, ancestors.Where(node => node.Path.Contains(ancestor.Path)).ToList()))
                .ToList(),
            Country = ToSummary(country),
            Extent = ToBounds(area.Extent),
            HasBoundary = area.Boundary is not null,
            Boundary = ToBoundary(area.Boundary, area.BoundaryProvenance),
            PostalCode = area.PostalCode?.Value,
            Overview = area.Overview
        };
    }

    /// <summary>
    /// An outline as a drawable ring, with whose it is. Null when the catalogue holds none.
    /// </summary>
    public static GeographicBoundaryResponse? ToBoundary(
        GeographicBoundary? boundary, BoundaryProvenance? provenance) =>
        boundary is null
            ? null
            : new GeographicBoundaryResponse
            {
                Vertices = boundary.Vertices
                    .Select(vertex => new GeographicVertexResponse
                    {
                        Latitude = vertex.Latitude,
                        Longitude = vertex.Longitude
                    })
                    .ToList(),
                Extent = ToBounds(boundary.Extent)!,
                SimplifiedTo = boundary.Vertices.Count,
                Source = provenance?.Source,
                Licence = provenance?.Licence,
                Attribution = provenance?.Attribution,
                SourceReference = provenance?.SourceReference,
                RetrievedOn = provenance?.RetrievedOn
            };

    /// <summary>
    /// A catalogue node as the canonical location reference.
    /// <para>
    /// Emits <c>neighborhood</c> and <c>areaName</c> with the same value. Five of the
    /// frontend's seven location DTOs call it <c>areaName</c> and the location store calls it
    /// <c>neighborhood</c>; picking one would break the other set for no gain. Naming finding
    /// #15 in the frontend contract report.
    /// </para>
    /// </summary>
    public static LocationReferenceResponse ToReference(
        AdministrativeArea area,
        IReadOnlyList<AdministrativeArea> ancestors,
        Country country,
        Distance? distance = null)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(ancestors);
        ArgumentNullException.ThrowIfNull(country);

        var city = area.Level == AdministrativeLevel.City
            ? area
            : ancestors.FirstOrDefault(node => node.Level == AdministrativeLevel.City);

        var state = ancestors.FirstOrDefault(
            node => node.Level is AdministrativeLevel.StateProvince or AdministrativeLevel.Region);

        var neighborhood = area.Level == AdministrativeLevel.Neighborhood ? area.Name.Value : null;

        return new LocationReferenceResponse
        {
            CountryCode = country.Code.Value,
            CountryName = country.Name.Value,
            Flag = country.Flag,
            State = state?.Name.Value,
            CityName = city?.Name.Value,
            Neighborhood = neighborhood,
            AreaName = neighborhood,
            Latitude = area.Center?.Latitude,
            Longitude = area.Center?.Longitude,
            DisplayName = ComposeDisplayName(area, ancestors),
            AreaId = area.Id.Value,
            CityId = city?.Id.Value,
            CountryId = country.Id.Value,
            Slug = area.Slug.Value,
            Level = area.Level.ToString(),
            DistanceKm = distance?.Kilometers
        };
    }

    /// <summary>A stored snapshot as the canonical location reference.</summary>
    public static LocationReferenceResponse ToReference(
        LocationSnapshot snapshot, CoordinatePrecision precision)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var disclosed = snapshot.AtPrecision(precision);

        return new LocationReferenceResponse
        {
            CountryCode = disclosed.CountryCode.Value,
            CountryName = disclosed.CountryName,
            Flag = disclosed.CountryCode.ToFlagEmoji(),
            State = disclosed.StateName,
            CityName = disclosed.CityName,
            Neighborhood = disclosed.Neighborhood,
            AreaName = disclosed.Neighborhood,
            Street = disclosed.Street,
            Latitude = disclosed.Coordinate?.Latitude,
            Longitude = disclosed.Coordinate?.Longitude,
            DisplayName = disclosed.DisplayName,
            AreaId = disclosed.AreaId?.Value
        };
    }

    /// <summary>
    /// "Peckham, London" — the leaf and its nearest named ancestor, and no more.
    /// <para>
    /// Two components because that is what the client's chip renders and what its own
    /// <c>displayName</c> already contains. A four-part breadcrumb is available separately for
    /// callers that have room for it.
    /// </para>
    /// </summary>
    private static string ComposeDisplayName(
        AdministrativeArea area, IReadOnlyList<AdministrativeArea> ancestors)
    {
        var qualifier = ancestors.LastOrDefault(
            ancestor => ancestor.Level is AdministrativeLevel.City
                or AdministrativeLevel.District
                or AdministrativeLevel.Region
                or AdministrativeLevel.StateProvince);

        return qualifier is null || qualifier.Id == area.Id
            ? area.Name.Value
            : $"{area.Name.Value}, {qualifier.Name.Value}";
    }
}
