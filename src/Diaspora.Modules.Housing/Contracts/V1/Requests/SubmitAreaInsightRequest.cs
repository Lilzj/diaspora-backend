namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// Shares one thing a member knows about the area around a point (14 Oct 2026).
/// <para>
/// The point-keyed successor of <see cref="SubmitNeighborhoodInsightRequest"/>: a Google place the
/// member picked, or where they are, in any country, rather than a catalogue neighbourhood.
/// Moderated exactly the same way; nothing renders until a moderator has read it.
/// </para>
/// </summary>
public sealed record SubmitAreaInsightRequest
{
    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    /// <summary>
    /// The Google place name the member picked, kept as they picked it and shown with the insight.
    /// Leave it out for "around here"; the platform never names a point itself.
    /// </summary>
    public string? PlaceName { get; init; }

    /// <summary>The Google place identifier behind <see cref="PlaceName"/>.</summary>
    public string? PlaceId { get; init; }

    /// <summary>The two-letter code of the country the point is in.</summary>
    public string? CountryCode { get; init; }

    /// <summary>
    /// Safety, Transport, CostOfLiving, Community, Noise, Utilities, Amenities, Schools, Nightlife,
    /// Accessibility, Healthcare or Other.
    /// </summary>
    public required string Category { get; init; }

    /// <summary>What the member wants to say. Up to 400 characters.</summary>
    public required string Body { get; init; }

    /// <summary>An optional short chip, such as "quiet after ten".</summary>
    public string? Label { get; init; }
}
