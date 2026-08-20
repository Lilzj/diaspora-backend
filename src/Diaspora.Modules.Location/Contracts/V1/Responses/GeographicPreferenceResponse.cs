namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>How a member wants geography to behave for them.</summary>
public sealed record GeographicPreferenceResponse
{
    /// <summary>
    /// The radius in kilometres, or the frontend's sentinel — <c>-1</c> for city-wide,
    /// <c>-2</c> for country-wide. Emitted in the client's own encoding so
    /// <c>RadiusSlider</c> binds without translation.
    /// </summary>
    public required double SearchRadius { get; init; }

    /// <summary><c>Radius</c>, <c>City</c> or <c>Country</c>. The honest form of the same fact.</summary>
    public required string RadiusScope { get; init; }

    /// <summary><c>Kilometers</c> or <c>Miles</c>. Display only; everything is stored in km.</summary>
    public required string DistanceUnit { get; init; }

    /// <summary><c>Exact</c> or <c>Approximate</c>. Matches the privacy setting Identity owns.</summary>
    public required string CoordinatePrecision { get; init; }

    public string? PreferredCountryCode { get; init; }

    public Guid? PreferredAreaId { get; init; }
}
