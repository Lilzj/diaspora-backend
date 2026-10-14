namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// One area a provider will serve beyond wherever it sits.
/// <para>
/// The concept the client has no equivalent of, and the reason every provider in its mock data
/// is a shop: without service areas, a mobile hairdresser is findable only on her own street.
/// </para>
/// </summary>
public sealed record ProviderServiceAreaResponse
{
    public required Guid ServiceAreaId { get; init; }

    /// <summary>The <c>ServiceAreaKind</c> name — <c>Country</c>, <c>Area</c>, <c>Radius</c>, <c>Remote</c>.</summary>
    public required string Kind { get; init; }

    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    /// <summary>What to call it on screen. "South London", "Within the M25".</summary>
    public string? Label { get; init; }

    /// <summary>Added 14 Oct 2026. The town a <c>Town</c> service area names.</summary>
    public string? CityName { get; init; }

    public double? CentreLatitude { get; init; }

    public double? CentreLongitude { get; init; }

    public double? RadiusKm { get; init; }

    public decimal? TravelSurcharge { get; init; }

    public string? TravelSurchargeCurrency { get; init; }
}
