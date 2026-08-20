using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Domain.ValueObjects;

/// <summary>
/// Where in B4's hierarchy an event or an organization sits.
/// <para>
/// <b>Identifiers and cached names, never a Location aggregate.</b> The neighbourhood is B4's, its
/// profile is B11's, and this module holds the key plus enough text to render a chip without a
/// second call. An architecture test asserts no Location type reaches the domain layer, because a
/// <c>CommunityEvent</c> holding an <c>Area</c> instead of an <c>AreaId</c> is the change that
/// would quietly make geography this module's property.
/// </para>
/// <para>
/// <b><see cref="AreaPath"/> is B4's materialised path stored verbatim</b>, and it is the only
/// reason "everything happening in London" is an indexed prefix scan rather than a recursive
/// descent through the hierarchy on every browse.
/// </para>
/// </summary>
public sealed class CommunityLifeLocationContext : ValueObject
{
    private CommunityLifeLocationContext(
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

    private CommunityLifeLocationContext() { }

    public string CountryCode { get; private set; } = string.Empty;

    public string? RegionName { get; private set; }

    public string? CityName { get; private set; }

    /// <summary>B4's name for the neighbourhood, cached so a card renders without a join.</summary>
    public string? NeighborhoodName { get; private set; }

    /// <summary>B4's area. The identifier every module on this platform already shares.</summary>
    public Guid? NeighborhoodAreaId { get; private set; }

    /// <summary>B4's materialised path. What makes subtree scoping an indexed prefix.</summary>
    public string? AreaPath { get; private set; }

    public bool HasNeighborhood => NeighborhoodAreaId is not null;

    /// <summary>The most specific place name available, for a one-line rendering.</summary>
    public string DisplayName =>
        NeighborhoodName ?? CityName ?? RegionName ?? CountryCode;

    public static Result<CommunityLifeLocationContext> Create(
        string? countryCode, string? regionName = null, string? cityName = null)
    {
        var country = countryCode?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(country) || country.Length != 2)
        {
            return Result.Failure<CommunityLifeLocationContext>(
                CommunityLifeErrors.CountryInvalid);
        }

        return Result.Success(new CommunityLifeLocationContext(
            country,
            Clip(regionName),
            Clip(cityName),
            neighborhoodName: null,
            neighborhoodAreaId: null,
            areaPath: null));
    }

    /// <summary>
    /// Places the subject in a B4 neighbourhood, taking the country from B4's node rather than from
    /// what the client typed. A subject whose stated country disagrees with the neighbourhood it
    /// names is in the wrong place, and B4 is the authority on which.
    /// </summary>
    public CommunityLifeLocationContext WithNeighborhood(
        Guid areaId, string? areaPath, string? neighborhoodName, string? countryCode = null) =>
        new(
            string.IsNullOrWhiteSpace(countryCode)
                ? CountryCode
                : countryCode.Trim().ToUpperInvariant(),
            RegionName,
            CityName,
            Clip(neighborhoodName),
            areaId,
            areaPath is null ? null : Clip(areaPath, CommunityLifeLimits.MaxAreaPath));

    /// <summary>Restates the human-readable place names, leaving the B4 keys alone.</summary>
    public CommunityLifeLocationContext WithPlaceNames(string? regionName, string? cityName) =>
        new(
            CountryCode,
            Clip(regionName),
            Clip(cityName),
            NeighborhoodName,
            NeighborhoodAreaId,
            AreaPath);

    private static string? Clip(string? value, int max = CommunityLifeLimits.MaxLocationName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > max ? trimmed[..max] : trimmed;
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
