using Diaspora.Modules.Community.Domain.Policies;

namespace Diaspora.Modules.Community.Domain.Posts;

/// <summary>
/// The reader a neighbourhood-only post is measured against (14 Oct 2026).
/// <para>
/// A neighbourhood audience is a radius of <see cref="CommunityLimits.NeighborhoodAudienceRadiusKm"/>
/// around the post's own pin, compared with the reader's active location. A post with no pin falls
/// back to its country. The rectangle here is only the indexed prefilter; the feed measures the
/// circle exactly afterwards, and a permalink is decided by the visibility evaluator.
/// </para>
/// </summary>
public sealed record NeighborhoodAudienceViewer
{
    private const double KilometresPerDegreeLatitude = 111.32d;

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>The reader's country and the countries they are a verified resident of.</summary>
    public IReadOnlyCollection<string> Countries { get; init; } = [];

    public bool HasPoint => Latitude is not null && Longitude is not null;

    /// <summary>
    /// The rectangle around the reader's circle, clamped to ±90°/±180° rather than wrapped — a
    /// circle across the antimeridian loses the part beyond it, the narrower direction to fail in.
    /// </summary>
    public (double South, double West, double North, double East)? Box()
    {
        if (Latitude is not { } latitude || Longitude is not { } longitude)
        {
            return null;
        }

        var latitudeSpan = CommunityLimits.NeighborhoodAudienceRadiusKm / KilometresPerDegreeLatitude;
        var shrink = Math.Max(
            Math.Cos(Math.Min(Math.Abs(latitude) + latitudeSpan, 90d) * Math.PI / 180d), 0.01d);
        var longitudeSpan = latitudeSpan / shrink;

        return (
            Math.Max(latitude - latitudeSpan, -90d),
            Math.Max(longitude - longitudeSpan, -180d),
            Math.Min(latitude + latitudeSpan, 90d),
            Math.Min(longitude + longitudeSpan, 180d));
    }
}
