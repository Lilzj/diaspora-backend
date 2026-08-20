using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.ValueObjects;

/// <summary>
/// Which place in B4's hierarchy the property sits in.
/// <para>
/// <b>Housing's neighbourhood is B4's neighbourhood, and this type is the whole of the
/// relationship.</b> It stores the country the property is jurisdictionally in, the identifier of
/// the administrative area the landlord picked, that area's materialised path, and the human
/// readable names a card renders. It resolves nothing: every authoritative geographic answer comes
/// from B4 through the scope resolver and the area catalogue.
/// </para>
/// <para>
/// <b>The distinction from B10's equivalent is the one that defines this module.</b> Marketplace
/// stores a free-text neighbourhood, because a seller saying "near the market" is describing where
/// to collect a bicycle and the catalogue has no row for it. Housing stores
/// <see cref="NeighborhoodAreaId"/> — a real node in B4's hierarchy — because a neighbourhood here
/// is not a description, it is the thing being chosen: a member picks Peckham and then looks at
/// what is in it. Free text would make "everything in Peckham" a string comparison over whatever
/// landlords happened to type, and would make the neighbourhood profile unjoinable.
/// </para>
/// <para>
/// <b>The names are denormalised and the identifier is authoritative.</b> Copying the names avoids
/// a join on every card render; when the two disagree because a place was renamed, the identifier
/// wins and the names are refreshed on the listing's next write. Nothing decides anything from the
/// copied names.
/// </para>
/// <para>
/// <b><see cref="AreaPath"/> is a plain string, matched with <c>LIKE 'prefix%'</c>.</b> That is
/// B4's own lesson recorded by every vertical since: a materialised path stored as text is what
/// makes "every property under London" an indexed prefix scan rather than a recursive descent.
/// </para>
/// </summary>
public sealed class HousingLocationContext : ValueObject
{
    private HousingLocationContext(
        string countryCode,
        string? regionName,
        string? cityName,
        string? neighborhoodName,
        Guid? neighborhoodAreaId,
        string? areaPath)
    {
        CountryCode = countryCode;
        RegionName = regionName;
        CityName = cityName;
        NeighborhoodName = neighborhoodName;
        NeighborhoodAreaId = neighborhoodAreaId;
        AreaPath = areaPath;
    }

    private HousingLocationContext() { }

    /// <summary>ISO 3166-1 alpha-2, upper case. The one geographic fact a property must have.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    public string? RegionName { get; private set; }

    public string? CityName { get; private set; }

    /// <summary>
    /// The neighbourhood's name, copied from B4 for rendering. Never the source of truth — see
    /// <see cref="NeighborhoodAreaId"/>.
    /// </summary>
    public string? NeighborhoodName { get; private set; }

    /// <summary>
    /// B4's administrative area, at neighbourhood level where the landlord chose one. The join key
    /// to the neighbourhood profile, and the reason this module can talk about neighbourhoods
    /// without owning them.
    /// </summary>
    public Guid? NeighborhoodAreaId { get; private set; }

    /// <summary>B4's materialised path, stored so scope narrowing is an indexed prefix match.</summary>
    public string? AreaPath { get; private set; }

    /// <summary>Whether the property has been placed in a real neighbourhood rather than a city.</summary>
    public bool HasNeighborhood => NeighborhoodAreaId is not null;

    public static Result<HousingLocationContext> Create(
        string? countryCode,
        string? regionName = null,
        string? cityName = null,
        string? neighborhoodName = null,
        Guid? neighborhoodAreaId = null,
        string? areaPath = null)
    {
        var country = NormalizeCountry(countryCode);

        if (country is null)
        {
            return Result.Failure<HousingLocationContext>(HousingErrors.CountryInvalid);
        }

        var region = Clamp(regionName, HousingLimits.MaxLocationName);
        var city = Clamp(cityName, HousingLimits.MaxLocationName);
        var neighborhood = Clamp(neighborhoodName, HousingLimits.MaxLocationName);
        var path = Clamp(areaPath, HousingLimits.MaxAreaPath);

        if (region is null && !string.IsNullOrWhiteSpace(regionName))
        {
            return Result.Failure<HousingLocationContext>(HousingErrors.LocationInvalid);
        }

        if (city is null && !string.IsNullOrWhiteSpace(cityName))
        {
            return Result.Failure<HousingLocationContext>(HousingErrors.LocationInvalid);
        }

        if (neighborhood is null && !string.IsNullOrWhiteSpace(neighborhoodName))
        {
            return Result.Failure<HousingLocationContext>(HousingErrors.LocationInvalid);
        }

        return Result.Success(new HousingLocationContext(
            country,
            region,
            city,
            neighborhood,
            neighborhoodAreaId is { } id && id != Guid.Empty ? id : null,
            path));
    }

    /// <summary>
    /// The same place with B4's resolved area attached. Called after the area catalogue has
    /// answered, so the listing stores what B4 said rather than what the client claimed.
    /// </summary>
    public HousingLocationContext WithNeighborhood(
        Guid? areaId, string? areaPath, string? neighborhoodName = null) =>
        new(CountryCode,
            RegionName,
            CityName,
            Clamp(neighborhoodName, HousingLimits.MaxLocationName) ?? NeighborhoodName,
            areaId is { } id && id != Guid.Empty ? id : null,
            Clamp(areaPath, HousingLimits.MaxAreaPath));

    /// <summary>The card's one-line place, as specific as B4 could make it.</summary>
    public string DisplayName =>
        string.Join(", ", new[] { NeighborhoodName, CityName, RegionName, CountryCode }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    public static string? NormalizeCountry(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return null;
        }

        var trimmed = countryCode.Trim().ToUpperInvariant();

        return trimmed.Length == 2 && trimmed.All(char.IsAsciiLetterUpper) ? trimmed : null;
    }

    private static string? Clamp(string? value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > maximum ? null : trimmed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CountryCode;
        yield return RegionName;
        yield return CityName;
        yield return NeighborhoodName;
        yield return NeighborhoodAreaId;
        yield return AreaPath;
    }
}
