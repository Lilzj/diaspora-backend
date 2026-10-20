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

    /// <summary>A heat layer was asked for over a time range the client's union does not hold.</summary>
    public const string HeatTimeRangeInvalid = Prefix + "heat.time_range_invalid";

    /// <summary>A heat layer was asked for from a source it does not have.</summary>
    public const string HeatSourceUnsupported = Prefix + "heat.source_unsupported";

    /// <summary>A rent or police heat layer was asked for without the country it compares within.</summary>
    public const string HeatCountryRequired = Prefix + "heat.country_required";

    public const string TravelModeUnsupported = Prefix + "travel_mode.unsupported";

    public const string TooManyWaypoints = Prefix + "route.too_many_waypoints";

    public const string RouteTooLong = Prefix + "route.too_long";

    public const string RoutingNotConfigured = Prefix + "routing.not_configured";

    public const string RoutingUnavailable = Prefix + "routing.unavailable";

    public const string RoutingRateLimited = Prefix + "routing.rate_limited";

    public const string RoutingNoRoute = Prefix + "routing.no_route";

    public const string ComparisonTooManySubjects = Prefix + "comparison.too_many_subjects";

    public const string ComparisonEmpty = Prefix + "comparison.empty";

    /// <summary>An area page was asked for without the country its point is in.</summary>
    public const string AreaCountryRequired = Prefix + "area.country_required";
}
