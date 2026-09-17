using System.ComponentModel.DataAnnotations;

namespace Diaspora.Modules.GeographicIntelligence.Options;

/// <summary>
/// The knobs an operator turns on the geographic intelligence surfaces.
/// <para>
/// Every one of them bounds something. There is no setting here that makes a figure up, and no
/// setting that turns a refusal into an estimate — the two things a geographic module is most
/// tempted to make configurable are exactly the two that must not be.
/// </para>
/// </summary>
public sealed class GeographicIntelligenceOptions
{
    public const string SectionName = "GeographicIntelligence";

    /// <summary>
    /// The fewest profiled neighbourhoods a percentile may be computed against.
    /// <para>
    /// Below this the ranked dimensions are withheld rather than published. A percentile over
    /// three places reads as authoritative and is noise; the floor is what keeps a launch country
    /// with four neighbourhoods from publishing rankings nobody should act on.
    /// </para>
    /// </summary>
    [Range(2, 1000)]
    public int MinimumComparisonPopulation { get; set; } = 5;

    /// <summary>
    /// The most neighbourhoods one comparison request may name.
    /// <para>
    /// Three is what the client's comparison surface renders; the ceiling is a little higher so a
    /// caller is not refused for a reasonable variation, and low enough that the request cannot
    /// become a fan of profile reads.
    /// </para>
    /// </summary>
    [Range(1, 20)]
    public int MaximumComparisonSubjects { get; set; } = 5;

    /// <summary>
    /// The widest viewport, in degrees of latitude span, the density layer will aggregate.
    /// <para>
    /// A public endpoint that accepts the whole planet is a full catalogue scan with a friendly
    /// name. Thirty degrees is a very wide regional view and well past any real map viewport.
    /// </para>
    /// </summary>
    [Range(0.001, 180)]
    public double MaximumHeatSpanDegrees { get; set; } = 30d;

    /// <summary>
    /// The most intermediate waypoints a routing request may carry.
    /// <para>
    /// Bounded because waypoints multiply a provider's cost and most vendors bill by the leg. Eight
    /// is more than any screen in the application offers.
    /// </para>
    /// </summary>
    [Range(0, 25)]
    public int MaximumRouteWaypoints { get; set; } = 8;

    /// <summary>
    /// The longest journey, in straight-line kilometres, this deployment will ask a provider to
    /// plan.
    /// <para>
    /// Checked before the provider is called, so an accidental request for a route across an ocean
    /// costs nothing rather than a vendor call and a timeout.
    /// </para>
    /// </summary>
    [Range(1, 20000)]
    public double MaximumRouteDistanceKm { get; set; } = 2000d;

    /// <summary>
    /// How long a routing provider is given before the request is abandoned.
    /// <para>
    /// A member is waiting on this one, unlike a background aggregation, so the budget is short and
    /// a timeout reports <c>Unavailable</c> rather than blocking a screen.
    /// </para>
    /// </summary>
    [Range(200, 30000)]
    public int RoutingTimeoutMilliseconds { get; set; } = 4000;

    /// <summary>
    /// How long a composed neighbourhood figure may be treated as current, in hours.
    /// <para>
    /// Published as <c>validUntil</c> on every response rather than enforced here — the server does
    /// not hold the cache, the client does, and a figure that says when it goes stale is more use
    /// than one the server silently refreshes.
    /// </para>
    /// </summary>
    [Range(1, 8760)]
    public int FreshnessWindowHours { get; set; } = 24;

    /// <summary>
    /// The fewest homes to rent a neighbourhood needs before its rent index is published.
    /// <para>
    /// A median of two listings is two listings. Below this the response says how many there are
    /// and publishes no index, which is what a place with a handful of homes on the platform should
    /// read as.
    /// </para>
    /// </summary>
    [Range(3, 1000)]
    public int MinimumRentListingsInArea { get; set; } = 5;

    /// <summary>
    /// The fewest homes to rent a country needs before any of its neighbourhoods is indexed
    /// against it. The baseline is a median too, and a baseline of a few homes makes every index
    /// against it noise.
    /// </summary>
    [Range(3, 100000)]
    public int MinimumRentListingsInCountry { get; set; } = 20;

    /// <summary>
    /// How many days back the community safety count looks. Long enough to hold more than a handful
    /// of reports in an active place, short enough that the count describes the place now.
    /// </summary>
    [Range(7, 730)]
    public int SafetyReportWindowDays { get; set; } = 90;
}
