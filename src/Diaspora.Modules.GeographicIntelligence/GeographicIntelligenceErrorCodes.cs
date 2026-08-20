namespace Diaspora.Modules.GeographicIntelligence;

/// <summary>
/// The frozen error codes this module returns. Descriptions may be reworded; these may not.
/// </summary>
public static class GeographicIntelligenceErrorCodes
{
    private const string Prefix = "geo.";

    public const string NeighborhoodNotFound = Prefix + "neighborhood.not_found";

    public const string CoordinateOutOfRange = Prefix + "coordinate.out_of_range";

    public const string CoordinateNotFinite = Prefix + "coordinate.not_finite";

    public const string BoundsInvalid = Prefix + "bounds.invalid";

    public const string BoundsTooLarge = Prefix + "bounds.too_large";

    public const string LayerUnsupported = Prefix + "layer.unsupported";

    public const string TravelModeUnsupported = Prefix + "travel_mode.unsupported";

    public const string TooManyWaypoints = Prefix + "route.too_many_waypoints";

    public const string RouteTooLong = Prefix + "route.too_long";

    public const string RoutingNotConfigured = Prefix + "routing.not_configured";

    public const string RoutingUnavailable = Prefix + "routing.unavailable";

    public const string RoutingRateLimited = Prefix + "routing.rate_limited";

    public const string RoutingNoRoute = Prefix + "routing.no_route";

    public const string ComparisonTooManySubjects = Prefix + "comparison.too_many_subjects";

    public const string ComparisonEmpty = Prefix + "comparison.empty";
}
