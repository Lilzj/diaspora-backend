namespace Diaspora.Modules.Community.Integration;

/// <summary>
/// How many safety reports members have made about a place, that other members corroborated.
/// <para>
/// <b>A count, not a verdict.</b> The user decided on 16 Sep 2026 that the platform publishes sourced
/// incident counts and no safety score. This is the platform's own source: community alerts in the
/// Police and Emergency categories that reached community verification, over a stated window, filed
/// in the place. It says how often members reported something others confirmed, and nothing about
/// how safe anyone is.
/// </para>
/// </summary>
public interface ICommunitySafetyReports
{
    /// <summary>
    /// Corroborated Police and Emergency reports filed at <paramref name="hierarchyPath"/> or beneath
    /// it, published within the last <paramref name="window"/> before <paramref name="asOf"/>.
    /// </summary>
    Task<CommunitySafetyReportCounts> CountCorroboratedAsync(
        string hierarchyPath,
        DateTimeOffset asOf,
        TimeSpan window,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Corroborated Police and Emergency reports pinned within <paramref name="radiusKm"/> of a
    /// point, published within the last <paramref name="window"/> before <paramref name="asOf"/>
    /// (14 Oct 2026, the area around a point). Measured at each report's own pin; a report with no
    /// pin is within nothing.
    /// </summary>
    Task<CommunitySafetyReportCounts> CountCorroboratedWithinAsync(
        double latitude,
        double longitude,
        double radiusKm,
        DateTimeOffset asOf,
        TimeSpan window,
        CancellationToken cancellationToken = default);
}

/// <summary>Corroborated safety reports in a place over a period, per category.</summary>
public sealed record CommunitySafetyReportCounts
{
    public required DateTimeOffset From { get; init; }

    public required DateTimeOffset To { get; init; }

    /// <summary>Keyed by category name: <c>Police</c>, <c>Emergency</c>. Every category is present, zero included.</summary>
    public required IReadOnlyDictionary<string, int> ByCategory { get; init; }

    public int Total => ByCategory.Values.Sum();

    /// <summary>
    /// True when the count stopped at its reading cap, so the true figure is at least
    /// <see cref="Total"/>. Never set by the path-based count.
    /// </summary>
    public bool IsLowerBound { get; init; }
}
