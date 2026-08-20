using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Domain.Providers;

/// <summary>
/// One area a provider will serve, beyond wherever it happens to sit.
/// <para>
/// <b>The field that makes a mobile provider findable.</b> A hairdresser who travels across
/// South London has premises in one postcode and customers across twenty, and a discovery model
/// that only knows where a provider <em>is</em> returns her to nobody outside her own street.
/// The client has no concept of this at all — every mock provider is found by proximity to its
/// own pin — which is why every mock provider is a shop.
/// </para>
/// <para>
/// Every kind resolves against Location's catalogue rather than B7's own geography.
/// <see cref="HierarchyPath"/> is Location's materialised path for <see cref="AreaId"/>, so
/// "does this provider cover Peckham" is a prefix comparison against the member's own path —
/// containment at any depth, one index, and no knowledge in B7 that Peckham is inside Southwark
/// is inside London.
/// </para>
/// </summary>
public sealed class ProviderServiceArea : Entity<ProviderServiceAreaId>
{
    public const int MaxLabelLength = 200;

    private ProviderServiceArea(ProviderServiceAreaId id, ServiceAreaKind kind) : base(id) =>
        Kind = kind;

    private ProviderServiceArea() { }

    public ServiceAreaKind Kind { get; private set; }

    /// <summary>ISO 3166-1 alpha-2. Set for <see cref="ServiceAreaKind.Country"/>, and carried
    /// alongside an area so a country filter never has to walk the tree.</summary>
    public string? CountryCode { get; private set; }

    /// <summary>Location's catalogue node. Set for <see cref="ServiceAreaKind.Area"/>.</summary>
    public Guid? AreaId { get; private set; }

    /// <summary>Location's materialised path for <see cref="AreaId"/>. The containment index.</summary>
    public string? HierarchyPath { get; private set; }

    /// <summary>What to call this area on screen. "South London", "Within the M25".</summary>
    public string? Label { get; private set; }

    /// <summary>Set for <see cref="ServiceAreaKind.Radius"/>.</summary>
    public double? CentreLatitude { get; private set; }

    public double? CentreLongitude { get; private set; }

    /// <summary>Kilometres. Set for <see cref="ServiceAreaKind.Radius"/>.</summary>
    public double? RadiusKm { get; private set; }

    /// <summary>
    /// A surcharge for reaching this area, in the provider's own currency. Optional and rarely
    /// set, but "I'll come, it's £15 extra" is a real answer and the alternative is a phone call.
    /// </summary>
    public decimal? TravelSurcharge { get; private set; }

    public string? TravelSurchargeCurrency { get; private set; }

    public static Result<ProviderServiceArea> ForCountry(
        ProviderServiceAreaId id, string? countryCode, string? label = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        var normalized = NormalizeCountry(countryCode);

        return normalized is null
            ? Result.Failure<ProviderServiceArea>(ProviderErrors.ServiceAreaScopeRequired)
            : Result.Success(new ProviderServiceArea(id, ServiceAreaKind.Country)
            {
                CountryCode = normalized,
                Label = Trim(label)
            });
    }

    public static Result<ProviderServiceArea> ForArea(
        ProviderServiceAreaId id,
        Guid areaId,
        string? hierarchyPath,
        string? countryCode = null,
        string? label = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        return areaId == Guid.Empty
            ? Result.Failure<ProviderServiceArea>(ProviderErrors.ServiceAreaScopeRequired)
            : Result.Success(new ProviderServiceArea(id, ServiceAreaKind.Area)
            {
                AreaId = areaId,
                HierarchyPath = Trim(hierarchyPath),
                CountryCode = NormalizeCountry(countryCode),
                Label = Trim(label)
            });
    }

    public static Result<ProviderServiceArea> ForRadius(
        ProviderServiceAreaId id,
        double latitude,
        double longitude,
        double radiusKm,
        string? countryCode = null,
        string? label = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return Result.Failure<ProviderServiceArea>(ProviderErrors.LocationCoordinatesInvalid);
        }

        return radiusKm is <= 0 or > ProviderLimits.MaxServiceRadiusKm
            ? Result.Failure<ProviderServiceArea>(ProviderErrors.ServiceAreaRadiusInvalid)
            : Result.Success(new ProviderServiceArea(id, ServiceAreaKind.Radius)
            {
                CentreLatitude = latitude,
                CentreLongitude = longitude,
                RadiusKm = radiusKm,
                CountryCode = NormalizeCountry(countryCode),
                Label = Trim(label)
            });
    }

    /// <summary>
    /// A remote service area. No geography at all — the provider serves anyone who can reach
    /// them online, and a proximity filter that excluded them would be wrong rather than strict.
    /// </summary>
    public static ProviderServiceArea Remote(ProviderServiceAreaId id, string? label = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        return new ProviderServiceArea(id, ServiceAreaKind.Remote) { Label = Trim(label) };
    }

    public Result SetTravelSurcharge(decimal? amount, string? currency)
    {
        if (amount is null)
        {
            TravelSurcharge = null;
            TravelSurchargeCurrency = null;
            return Result.Success();
        }

        if (amount < 0)
        {
            return Result.Failure(ProviderErrors.PricingNegative);
        }

        var normalized = currency?.Trim().ToUpperInvariant();

        if (normalized is null || normalized.Length != 3 || !normalized.All(char.IsAsciiLetterUpper))
        {
            return Result.Failure(ProviderErrors.PricingCurrencyInvalid);
        }

        TravelSurcharge = amount;
        TravelSurchargeCurrency = normalized;

        return Result.Success();
    }

    /// <summary>
    /// A comparison key for duplicate detection. Two entries for the same country, or the same
    /// catalogue node, are the same coverage claim written twice.
    /// </summary>
    public string DedupeKey => Kind switch
    {
        ServiceAreaKind.Country => $"country:{CountryCode}",
        ServiceAreaKind.Area => $"area:{AreaId:N}",
        ServiceAreaKind.Radius => $"radius:{CentreLatitude:F4},{CentreLongitude:F4},{RadiusKm:F1}",
        _ => "remote"
    };

    private static string? NormalizeCountry(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return null;
        }

        var normalized = countryCode.Trim().ToUpperInvariant();

        return normalized.Length == 2 && normalized.All(char.IsAsciiLetterUpper) ? normalized : null;
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, MaxLabelLength)];
}
