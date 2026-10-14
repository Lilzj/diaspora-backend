namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>Adds one area a provider will serve beyond wherever it sits.</summary>
public sealed record ProviderServiceAreaRequest
{
    /// <summary>
    /// The <c>ServiceAreaKind</c> name — <c>Country</c>, <c>Town</c>, <c>Radius</c>, <c>Remote</c>.
    /// <c>Area</c> (a catalogue node) is legacy and no longer created.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>Required for <c>Country</c>.</summary>
    public string? CountryCode { get; init; }

    /// <summary>Legacy, for <c>Area</c>. Ignored since 14 Oct 2026.</summary>
    public Guid? AreaId { get; init; }

    /// <summary>Required for <c>Town</c>, with <see cref="CountryCode"/>.</summary>
    public string? CityName { get; init; }

    /// <summary>What to call this area on screen. "South London", "Within the M25".</summary>
    public string? Label { get; init; }

    /// <summary>Required for <c>Radius</c>. Defaults to the provider's own coordinates.</summary>
    public double? CentreLatitude { get; init; }

    public double? CentreLongitude { get; init; }

    /// <summary>Required for <c>Radius</c>, in kilometres.</summary>
    public double? RadiusKm { get; init; }

    /// <summary>"I'll come, it's £15 extra" — a real answer, and the alternative is a phone call.</summary>
    public decimal? TravelSurcharge { get; init; }

    public string? TravelSurchargeCurrency { get; init; }
}
