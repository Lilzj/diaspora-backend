using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.ValueObjects;

/// <summary>
/// Where the item is.
/// <para>
/// <b>Denormalised names beside a Location identifier, and that combination is deliberate.</b>
/// B4 owns the hierarchy; this holds the country code the listing is jurisdictionally in, the
/// area identifier when the seller picked one, and the human-readable names for the card. The
/// names are a rendering convenience — every authoritative answer about geography comes from B4
/// through the scope resolver, and nothing here resolves a city, parses a radius or computes a
/// distance.
/// </para>
/// <para>
/// <b><see cref="AreaPath"/> is a plain string, matched with <c>LIKE 'prefix%'</c>.</b> That is
/// B4's own lesson recorded by every vertical since: a materialised path stored as text is what
/// makes "everything under London" an indexed prefix scan rather than a recursive descent.
/// </para>
/// <para>
/// <b><see cref="Neighbourhood"/> is free text and is never a Location entity.</b> Sellers say
/// "near the market" and "Brixton Hill end"; the catalogue has no row for either, and inventing
/// one from a seller's typing would put unverified places into B4's hierarchy.
/// </para>
/// </summary>
public sealed class ListingLocationContext : ValueObject
{
    private ListingLocationContext(
        string countryCode,
        string? regionName,
        string? cityName,
        string? neighbourhood,
        Guid? areaId,
        string? areaPath)
    {
        CountryCode = countryCode;
        RegionName = regionName;
        CityName = cityName;
        Neighbourhood = neighbourhood;
        AreaId = areaId;
        AreaPath = areaPath;
    }

    private ListingLocationContext() { }

    /// <summary>ISO 3166-1 alpha-2, upper case. The one geographic fact a listing must have.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    public string? RegionName { get; private set; }

    public string? CityName { get; private set; }

    /// <summary>The seller's own words for the neighbourhood. Never a catalogue entity.</summary>
    public string? Neighbourhood { get; private set; }

    /// <summary>B4's area, when the seller picked one from the hierarchy.</summary>
    public Guid? AreaId { get; private set; }

    /// <summary>B4's materialised path, stored so scope narrowing is an indexed prefix match.</summary>
    public string? AreaPath { get; private set; }

    public static Result<ListingLocationContext> Create(
        string? countryCode,
        string? regionName = null,
        string? cityName = null,
        string? neighbourhood = null,
        Guid? areaId = null,
        string? areaPath = null)
    {
        var country = NormalizeCountry(countryCode);

        if (country is null)
        {
            return Result.Failure<ListingLocationContext>(MarketplaceErrors.CountryInvalid);
        }

        var region = Clamp(regionName, MarketplaceLimits.MaxLocationName);
        var city = Clamp(cityName, MarketplaceLimits.MaxLocationName);
        var area = Clamp(neighbourhood, MarketplaceLimits.MaxLocationName);
        var path = Clamp(areaPath, MarketplaceLimits.MaxAreaPath);

        if (region is null && !string.IsNullOrWhiteSpace(regionName))
        {
            return Result.Failure<ListingLocationContext>(MarketplaceErrors.LocationInvalid);
        }

        if (city is null && !string.IsNullOrWhiteSpace(cityName))
        {
            return Result.Failure<ListingLocationContext>(MarketplaceErrors.LocationInvalid);
        }

        return Result.Success(new ListingLocationContext(
            country,
            region,
            city,
            area,
            areaId is { } id && id != Guid.Empty ? id : null,
            path));
    }

    /// <summary>
    /// The same place with B4's resolved area attached. Called after the scope resolver has
    /// answered, so the listing stores what B4 said rather than what the client claimed.
    /// </summary>
    public ListingLocationContext WithArea(Guid? areaId, string? areaPath) =>
        new(CountryCode,
            RegionName,
            CityName,
            Neighbourhood,
            areaId is { } id && id != Guid.Empty ? id : null,
            Clamp(areaPath, MarketplaceLimits.MaxAreaPath));

    /// <summary>The card's one-line place, as specific as the seller made it.</summary>
    public string DisplayName =>
        string.Join(", ", new[] { Neighbourhood, CityName, RegionName, CountryCode }
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
        yield return Neighbourhood;
        yield return AreaId;
        yield return AreaPath;
    }
}
