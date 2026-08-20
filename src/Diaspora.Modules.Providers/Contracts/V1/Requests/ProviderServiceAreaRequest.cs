namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>Adds one area a provider will serve beyond wherever it sits.</summary>
public sealed record ProviderServiceAreaRequest
{
    /// <summary>The <c>ServiceAreaKind</c> name — <c>Country</c>, <c>Area</c>, <c>Radius</c>, <c>Remote</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Required for <c>Country</c>.</summary>
    public string? CountryCode { get; init; }

    /// <summary>Required for <c>Area</c>. Resolved against Location's catalogue.</summary>
    public Guid? AreaId { get; init; }

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
