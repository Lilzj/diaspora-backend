using System.Diagnostics;
using Diaspora.Application.Abstractions;
using Diaspora.Modules.GeographicIntelligence.Contracts.V1.Responses;
using Diaspora.Modules.GeographicIntelligence.Domain.Errors;
using Diaspora.Modules.GeographicIntelligence.Domain.Routing;
using Diaspora.Modules.GeographicIntelligence.Observability;
using Diaspora.Modules.GeographicIntelligence.Options;
using Diaspora.Modules.Location.Domain.Spatial;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.GeographicIntelligence.Application.Routing;

/// <summary>A journey to plan. Coordinates are raw here and validated before anything else runs.</summary>
public sealed record PlanRouteQuery : IQuery<RouteResponse>
{
    public required double OriginLatitude { get; init; }

    public required double OriginLongitude { get; init; }

    public required double DestinationLatitude { get; init; }

    public required double DestinationLongitude { get; init; }

    public IReadOnlyList<(double Latitude, double Longitude)> Waypoints { get; init; } = [];

    public required string TravelMode { get; init; }

    public bool Alternatives { get; init; }

    public DateTimeOffset? DepartAt { get; init; }

    public string? Language { get; init; }
}

/// <summary>
/// Validates a routing request, bounds it, and asks the configured provider.
/// <para>
/// <b>Everything expensive happens after everything cheap.</b> Coordinates are range-checked, the
/// mode is parsed, the waypoints are counted and the straight-line distance is measured — all
/// before a provider is called, so a request for a route across an ocean costs one trigonometric
/// function rather than a vendor call, a timeout and a bill. The straight line is also a genuine
/// lower bound on any route, which is why it is the right thing to bound against.
/// </para>
/// <para>
/// <b>The straight line is used to refuse a request and never to answer one.</b> That is the
/// single rule this handler exists to keep, and the line between an estimate and a lie.
/// </para>
/// <para>
/// <b>A provider condition is a status, not an exception.</b> Timeouts are caught here and reported
/// as <see cref="RouteOutcome.Unavailable"/>; a vendor being down is an ordinary Tuesday and must
/// not become a 500 on a screen that had six other things to show.
/// </para>
/// </summary>
public sealed class PlanRouteQueryHandler : IQueryHandler<PlanRouteQuery, RouteResponse>
{
    private readonly IRoutingProvider _provider;
    private readonly IGeoDistanceCalculator _distance;
    private readonly IOptions<GeographicIntelligenceOptions> _options;

    public PlanRouteQueryHandler(
        IRoutingProvider provider,
        IGeoDistanceCalculator distance,
        IOptions<GeographicIntelligenceOptions> options)
    {
        _provider = provider;
        _distance = distance;
        _options = options;
    }

    public async Task<Result<RouteResponse>> Handle(
        PlanRouteQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        GeographicIntelligenceTelemetry.Requests.Add(
            1, new KeyValuePair<string, object?>("surface", "routing"));

        var settings = _options.Value;

        if (!TryParseMode(request.TravelMode, out var mode))
        {
            return Result.Failure<RouteResponse>(
                GeographicIntelligenceErrors.TravelModeUnsupported(request.TravelMode ?? string.Empty));
        }

        var origin = Coordinate(request.OriginLatitude, request.OriginLongitude, "Origin");

        if (origin.IsFailure)
        {
            return Result.Failure<RouteResponse>(origin.Error);
        }

        var destination = Coordinate(
            request.DestinationLatitude, request.DestinationLongitude, "Destination");

        if (destination.IsFailure)
        {
            return Result.Failure<RouteResponse>(destination.Error);
        }

        if (request.Waypoints.Count > settings.MaximumRouteWaypoints)
        {
            GeographicIntelligenceTelemetry.BoundsRejected.Add(
                1, new KeyValuePair<string, object?>("surface", "routing"));

            return Result.Failure<RouteResponse>(
                GeographicIntelligenceErrors.TooManyWaypoints(
                    request.Waypoints.Count, settings.MaximumRouteWaypoints));
        }

        var waypoints = new List<GeoCoordinate>(request.Waypoints.Count);

        foreach (var (latitude, longitude) in request.Waypoints)
        {
            var point = Coordinate(latitude, longitude, "Waypoint");

            if (point.IsFailure)
            {
                return Result.Failure<RouteResponse>(point.Error);
            }

            waypoints.Add(point.Value);
        }

        // The straight line through every leg. A lower bound on the real route, which is exactly
        // what a ceiling should be checked against — a request the crow could not make is one no
        // provider should be asked about.
        var span = StraightLineKm([origin.Value, .. waypoints, destination.Value]);

        if (span > settings.MaximumRouteDistanceKm)
        {
            GeographicIntelligenceTelemetry.BoundsRejected.Add(
                1, new KeyValuePair<string, object?>("surface", "routing"));

            return Result.Failure<RouteResponse>(
                GeographicIntelligenceErrors.RouteTooLong(span, settings.MaximumRouteDistanceKm));
        }

        var result = await AskAsync(
            new RoutePlanRequest
            {
                Origin = origin.Value,
                Destination = destination.Value,
                Waypoints = waypoints,
                Mode = mode,
                Alternatives = request.Alternatives,
                DepartAt = request.DepartAt,
                Language = request.Language
            },
            settings,
            cancellationToken);

        return Result.Success(Map(result, mode, _provider.Attribution));
    }

    /// <summary>
    /// Calls the provider inside its own budget and turns every failure into an outcome.
    /// <para>
    /// The timeout is linked to the caller's token so a member navigating away still cancels the
    /// call, and separated from it so a provider exceeding its budget is reported as unavailable
    /// rather than surfacing as a cancelled request.
    /// </para>
    /// </summary>
    private async Task<RouteProviderResult> AskAsync(
        RoutePlanRequest request,
        GeographicIntelligenceOptions settings,
        CancellationToken cancellationToken)
    {
        if (!_provider.SupportedModes.Contains(request.Mode))
        {
            var outcome = _provider.SupportedModes.Count == 0
                ? RouteProviderResult.NotConfigured(_provider.Key)
                : RouteProviderResult.Unsupported(_provider.Key);

            Record(outcome, elapsed: 0);

            return outcome;
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(settings.RoutingTimeoutMilliseconds);

        var started = Stopwatch.GetTimestamp();

        try
        {
            var answer = await _provider.PlanAsync(request, budget.Token);

            Record(answer, Stopwatch.GetElapsedTime(started).TotalMilliseconds);

            return answer;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var timedOut = RouteProviderResult.Unavailable(_provider.Key, "The provider timed out.");

            Record(timedOut, Stopwatch.GetElapsedTime(started).TotalMilliseconds);

            return timedOut;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The provider's own message is deliberately not carried through: a vendor error body
            // can contain the request it was given, which for a routing call is where somebody is.
            var failed = RouteProviderResult.Unavailable(_provider.Key, "The provider failed.");

            Record(failed, Stopwatch.GetElapsedTime(started).TotalMilliseconds);

            return failed;
        }
    }

    private void Record(RouteProviderResult result, double elapsed)
    {
        var provider = new KeyValuePair<string, object?>("provider", _provider.Key);

        GeographicIntelligenceTelemetry.ProviderRequests.Add(
            1, provider, new KeyValuePair<string, object?>("capability", "routing"));

        GeographicIntelligenceTelemetry.ProviderLatency.Record(
            elapsed, provider, new KeyValuePair<string, object?>("capability", "routing"));

        if (result.Outcome != RouteOutcome.Success)
        {
            GeographicIntelligenceTelemetry.ProviderFailures.Add(
                1, provider,
                new KeyValuePair<string, object?>("outcome", result.Outcome.ToString()));
        }
    }

    private static RouteResponse Map(RouteProviderResult result, TravelMode mode, string? attribution) =>
        new()
        {
            Status = result.Outcome.ToString(),
            TravelMode = mode.ToString(),
            Provider = result.ProviderKey,
            IsRouted = result.IsSuccess,
            Routes =
            [
                .. result.Routes.Select(route => new PlannedRouteResponse
                {
                    Summary = route.Summary,
                    DistanceMetres = route.DistanceMetres,
                    DurationSeconds = route.DurationSeconds,
                    DurationInTrafficSeconds = route.DurationInTrafficSeconds,
                    Legs =
                    [
                        .. route.Legs.Select(leg => new RouteLegResponse
                        {
                            Mode = leg.Mode.ToString(),
                            DistanceMetres = leg.DistanceMetres,
                            DurationSeconds = leg.DurationSeconds,
                            Steps =
                            [
                                .. leg.Steps.Select(step => new RouteStepResponse
                                {
                                    Manoeuvre = step.Manoeuvre,
                                    Instruction = step.Instruction,
                                    DistanceMetres = step.DistanceMetres,
                                    DurationSeconds = step.DurationSeconds,
                                    At = step.At is null
                                        ? null
                                        : new RoutePointResponse
                                        {
                                            Latitude = step.At.Latitude,
                                            Longitude = step.At.Longitude
                                        }
                                })
                            ],
                            Line = leg.Line,
                            Agency = leg.Agency,
                            Headsign = leg.Headsign,
                            DepartureStop = leg.DepartureStop,
                            ArrivalStop = leg.ArrivalStop,
                            DepartureAt = leg.DepartureAt,
                            ArrivalAt = leg.ArrivalAt,
                            StopCount = leg.StopCount
                        })
                    ],
                    Geometry =
                    [
                        .. route.Geometry.Select(point => new RoutePointResponse
                        {
                            Latitude = point.Latitude,
                            Longitude = point.Longitude
                        })
                    ],
                    Warnings = route.Warnings
                })
            ],
            Attribution = result.IsSuccess ? attribution : null,
            Message = Explain(result.Outcome)
        };

    /// <summary>
    /// A member-readable sentence per outcome.
    /// <para>
    /// None of them offers a substitute figure, and that is deliberate: the client already holds a
    /// straight-line estimate and already labels it as one. A message suggesting the estimate were
    /// as good would undo the labelling from the server side.
    /// </para>
    /// </summary>
    private static string? Explain(RouteOutcome outcome) => outcome switch
    {
        RouteOutcome.Success => null,
        RouteOutcome.NotConfigured =>
            "Diaspora does not provide directions. No routing provider is configured.",
        RouteOutcome.Unavailable =>
            "Directions are temporarily unavailable. Please try again shortly.",
        RouteOutcome.Unsupported =>
            "Directions are not available for this travel mode in this area.",
        RouteOutcome.RateLimited =>
            "Too many directions requests just now. Please try again in a moment.",
        RouteOutcome.NoRoute =>
            "No route was found between those two points for that travel mode.",
        RouteOutcome.AllowanceExhausted =>
            "Directions have reached this month's limit and will be back next month.",
        _ => "That directions request could not be understood."
    };

    private static bool TryParseMode(string? value, out TravelMode mode)
    {
        mode = default;

        return !string.IsNullOrWhiteSpace(value)
            && Enum.TryParse(value.Trim(), ignoreCase: true, out mode)
            && Enum.IsDefined(mode);
    }

    /// <summary>
    /// Validates one coordinate.
    /// <para>
    /// Through B4's <see cref="GeoCoordinate"/>, so there is one WGS 84 range check on this
    /// platform rather than two that could drift. It refuses NaN, infinity and out-of-range on both
    /// axes — which is what stops a longitude arriving in a latitude field, the one malformed
    /// coordinate that is silent everywhere else because both are plausible numbers.
    /// </para>
    /// </summary>
    private static Result<GeoCoordinate> Coordinate(double latitude, double longitude, string field)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude))
        {
            GeographicIntelligenceTelemetry.InvalidCoordinates.Add(
                1, new KeyValuePair<string, object?>("rule", "not-finite"));

            return Result.Failure<GeoCoordinate>(
                GeographicIntelligenceErrors.CoordinateNotFinite(field));
        }

        if (latitude is < GeoCoordinate.MinimumLatitude or > GeoCoordinate.MaximumLatitude)
        {
            GeographicIntelligenceTelemetry.InvalidCoordinates.Add(
                1, new KeyValuePair<string, object?>("rule", "latitude-range"));

            return Result.Failure<GeoCoordinate>(
                GeographicIntelligenceErrors.CoordinateOutOfRange(
                    $"{field} latitude",
                    latitude,
                    GeoCoordinate.MinimumLatitude,
                    GeoCoordinate.MaximumLatitude));
        }

        if (longitude is < GeoCoordinate.MinimumLongitude or > GeoCoordinate.MaximumLongitude)
        {
            GeographicIntelligenceTelemetry.InvalidCoordinates.Add(
                1, new KeyValuePair<string, object?>("rule", "longitude-range"));

            return Result.Failure<GeoCoordinate>(
                GeographicIntelligenceErrors.CoordinateOutOfRange(
                    $"{field} longitude",
                    longitude,
                    GeoCoordinate.MinimumLongitude,
                    GeoCoordinate.MaximumLongitude));
        }

        var created = GeoCoordinate.TryCreate(latitude, longitude);

        return created.IsSuccess
            ? created
            : Result.Failure<GeoCoordinate>(created.Error);
    }

    private double StraightLineKm(IReadOnlyList<GeoCoordinate> points)
    {
        var total = 0d;

        for (var index = 1; index < points.Count; index++)
        {
            total += _distance.Between(points[index - 1], points[index]).Kilometers;
        }

        return total;
    }
}
