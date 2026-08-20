using System.Globalization;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.GeographicIntelligence.Domain.Errors;

/// <summary>
/// The errors this module returns, in the same shape as every other module's.
/// <para>
/// The routing family deserves a note. A provider that is <em>not configured</em>, one that is
/// <em>down</em>, one that <em>refused the quota</em> and one that <em>found no route</em> are four
/// different facts, and collapsing them into "routing failed" is what produces a client that
/// retries a road that does not exist and gives up on a vendor that is merely rate-limiting it.
/// Each has its own code, and <see cref="RoutingNotConfigured"/> is deliberately not an error type
/// a client should retry.
/// </para>
/// </summary>
public static class GeographicIntelligenceErrors
{
    public static readonly Error NeighborhoodNotFound =
        Error.NotFound(
            GeographicIntelligenceErrorCodes.NeighborhoodNotFound,
            "This neighbourhood could not be located in our geographic directory.");

    public static Error CoordinateOutOfRange(string field, double value, double min, double max) =>
        Error.Validation(
            GeographicIntelligenceErrorCodes.CoordinateOutOfRange,
            string.Format(
                CultureInfo.InvariantCulture,
                "{0} must be between {1} and {2}; {3} is outside that range.",
                field, min, max, value));

    public static Error CoordinateNotFinite(string field) =>
        Error.Validation(
            GeographicIntelligenceErrorCodes.CoordinateNotFinite,
            $"{field} must be a finite number.");

    public static readonly Error BoundsInvalid =
        Error.Validation(
            GeographicIntelligenceErrorCodes.BoundsInvalid,
            "A viewport must be four comma-separated numbers: north,south,east,west.");

    public static Error BoundsTooLarge(double spanDegrees, double maximum) =>
        Error.Validation(
            GeographicIntelligenceErrorCodes.BoundsTooLarge,
            string.Format(
                CultureInfo.InvariantCulture,
                "That viewport spans {0:0.##}° and this deployment aggregates at most {1:0.##}°. "
                + "Zoom in, or ask for a country instead.",
                spanDegrees, maximum));

    public static Error LayerUnsupported(string layer, string supported) =>
        Error.Validation(
            GeographicIntelligenceErrorCodes.LayerUnsupported,
            $"'{layer}' is not a density layer this platform can compute. Supported: {supported}.");

    public static Error TravelModeUnsupported(string mode) =>
        Error.Validation(
            GeographicIntelligenceErrorCodes.TravelModeUnsupported,
            $"'{mode}' is not a travel mode. Use walking, driving, cycling or transit.");

    public static Error TooManyWaypoints(int given, int maximum) =>
        Error.Validation(
            GeographicIntelligenceErrorCodes.TooManyWaypoints,
            $"A route may carry {maximum} intermediate waypoints; {given} were given.");

    public static Error RouteTooLong(double kilometres, double maximum) =>
        Error.Validation(
            GeographicIntelligenceErrorCodes.RouteTooLong,
            string.Format(
                CultureInfo.InvariantCulture,
                "That route spans {0:0.#} km in a straight line and this deployment plans at most "
                + "{1:0.#} km.",
                kilometres, maximum));

    public static Error TooManySubjects(int given, int maximum) =>
        Error.Validation(
            GeographicIntelligenceErrorCodes.ComparisonTooManySubjects,
            $"A comparison holds at most {maximum} neighbourhoods; {given} were asked for.");

    public static readonly Error ComparisonEmpty =
        Error.Validation(
            GeographicIntelligenceErrorCodes.ComparisonEmpty,
            "Name at least one neighbourhood to compare.");

    /// <summary>
    /// No routing vendor is contracted for this deployment.
    /// <para>
    /// <b><see cref="ErrorType.ServiceUnavailable"/> rather than a validation failure, and never a
    /// fabricated route.</b> The request was well formed and the platform simply cannot answer it.
    /// The frontend's own register is explicit that a straight-line figure presented as a walking
    /// time is the one thing it will not do, and a backend that answered with one would take that
    /// decision away from it.
    /// </para>
    /// </summary>
    public static readonly Error RoutingNotConfigured =
        Error.ServiceUnavailable(
            GeographicIntelligenceErrorCodes.RoutingNotConfigured,
            "Routing is not configured for this deployment. No travel time is available.");

    public static Error RoutingUnavailable(string provider) =>
        Error.ServiceUnavailable(
            GeographicIntelligenceErrorCodes.RoutingUnavailable,
            $"The routing provider '{provider}' could not be reached.");

    public static Error RoutingRateLimited(string provider) =>
        Error.RateLimited(
            GeographicIntelligenceErrorCodes.RoutingRateLimited,
            $"The routing provider '{provider}' is refusing further requests just now.");

    /// <summary>
    /// The provider answered, and the answer is that there is no route.
    /// <para>
    /// A distinct code from every failure above, because it is not one: an island, a closed border
    /// and a pedestrian origin in the middle of a motorway all produce this, and a client that
    /// retried it would retry forever.
    /// </para>
    /// </summary>
    public static readonly Error RoutingNoRoute =
        Error.NotFound(
            GeographicIntelligenceErrorCodes.RoutingNoRoute,
            "No route exists between those points for that travel mode.");
}
