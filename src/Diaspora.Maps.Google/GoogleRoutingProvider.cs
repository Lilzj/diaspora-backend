using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Diaspora.Modules.GeographicIntelligence.Domain.Routing;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Platform.Usage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Maps.Google;

/// <summary>
/// Routes API <c>computeRoutes</c>, behind the platform's routing seam.
/// <para>
/// <b>What keeps every request in the Essentials SKU.</b> <c>routingPreference</c> is always
/// <c>TRAFFIC_UNAWARE</c> (and only sent for driving, where it is allowed at all); waypoints are
/// capped by <c>GeographicIntelligence:MaximumRouteWaypoints</c> at 8, under the eleven that make a
/// request Pro; two-wheeler is not a mode this platform has. So
/// <see cref="PlannedRoute.DurationInTrafficSeconds"/> is always null here — a traffic figure
/// would cost the Pro SKU, and the contract forbids inventing one.
/// </para>
/// <para>
/// <b>The allowance is taken after every local refusal and before the call.</b> A transit request
/// with waypoints is refused without spending a unit, because Google would refuse it too.
/// </para>
/// </summary>
internal sealed class GoogleRoutingProvider : IRoutingProvider
{
    public const string HttpClientName = "maps.google.routes";
    public const string ProviderKey = "google";
    public const string Sku = "routes-compute-essentials";

    /// <summary>
    /// Every field read below, and nothing else. The Routes API refuses a request without a mask,
    /// and a wildcard would fetch fields nobody reads.
    /// </summary>
    internal const string FieldMask =
        "routes.distanceMeters,routes.duration,routes.polyline.encodedPolyline,routes.description,"
        + "routes.warnings,routes.legs.distanceMeters,routes.legs.duration,"
        + "routes.legs.steps.distanceMeters,routes.legs.steps.staticDuration,"
        + "routes.legs.steps.polyline.encodedPolyline,routes.legs.steps.startLocation,"
        + "routes.legs.steps.navigationInstruction,routes.legs.steps.travelMode,"
        + "routes.legs.steps.transitDetails";

    private readonly HttpClient _http;
    private readonly IProviderUsageMeter _meter;
    private readonly IOptionsMonitor<GoogleMapsOptions> _options;
    private readonly ILogger<GoogleRoutingProvider> _logger;

    public GoogleRoutingProvider(
        HttpClient http,
        IProviderUsageMeter meter,
        IOptionsMonitor<GoogleMapsOptions> options,
        ILogger<GoogleRoutingProvider> logger)
    {
        _http = http;
        _meter = meter;
        _options = options;
        _logger = logger;
    }

    public string Key => ProviderKey;

    /// <summary>
    /// Google permits the text form where space is short. On a Google map the map's own logo
    /// already satisfies it; the client decides which applies.
    /// </summary>
    public string? Attribution => "Google Maps";

    public IReadOnlyCollection<TravelMode> SupportedModes
    {
        get
        {
            var settings = _options.CurrentValue;
            if (!settings.IsConfigured || !settings.EnableRouting) return [];

            return settings.EnableTransit
                ? [TravelMode.Walking, TravelMode.Cycling, TravelMode.Driving, TravelMode.Transit]
                : [TravelMode.Walking, TravelMode.Cycling, TravelMode.Driving];
        }
    }

    public async Task<RouteProviderResult> PlanAsync(
        RoutePlanRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = _options.CurrentValue;

        if (request.Mode == TravelMode.Transit && request.Waypoints.Count > 0)
        {
            return RouteProviderResult.Unsupported(Key, "Transit routes cannot have intermediate stops.");
        }

        if (!await _meter.TryConsumeAsync(
                Key, Sku, settings.RoutesMonthlyAllowance, settings.BillingTimeZone, cancellationToken))
        {
            _logger.LogWarning("Google routing allowance for this month is spent; refusing without calling.");
            return RouteProviderResult.AllowanceExhausted(Key);
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, "directions/v2:computeRoutes")
        {
            Content = JsonContent.Create(BuildBody(request))
        };

        message.Headers.Add("X-Goog-Api-Key", settings.ApiKey);
        message.Headers.Add("X-Goog-FieldMask", FieldMask);

        HttpResponseMessage response;

        try
        {
            response = await _http.SendAsync(message, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return RouteProviderResult.Unavailable(Key, "The provider could not be reached.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return Refusal(response.StatusCode);
            }

            var body = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
            var routes = ParseRoutes(body, request.Mode);

            // An empty success is how Google says "no route"; the factory turns it into NoRoute.
            return RouteProviderResult.Success(Key, routes);
        }
    }

    private RouteProviderResult Refusal(HttpStatusCode status)
    {
        switch (status)
        {
            case HttpStatusCode.TooManyRequests:
                return RouteProviderResult.RateLimited(Key);
            case HttpStatusCode.BadRequest:
                return RouteProviderResult.InvalidRequest(Key);
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                // An operator problem, never a member's: the key is wrong, restricted, or the API is
                // not enabled on the project. Logged loudly and reported as unavailable.
                _logger.LogError(
                    "Google Routes refused the server key ({Status}). Check the key's API restrictions.",
                    (int)status);
                return RouteProviderResult.Unavailable(Key, "The provider refused the deployment's credentials.");
            default:
                return RouteProviderResult.Unavailable(Key, $"The provider answered {(int)status}.");
        }
    }

    internal static JsonObject BuildBody(RoutePlanRequest request)
    {
        var body = new JsonObject
        {
            ["origin"] = Waypoint(request.Origin),
            ["destination"] = Waypoint(request.Destination),
            ["travelMode"] = request.Mode switch
            {
                TravelMode.Walking => "WALK",
                TravelMode.Cycling => "BICYCLE",
                TravelMode.Transit => "TRANSIT",
                _ => "DRIVE"
            },
            ["computeAlternativeRoutes"] = request.Alternatives,
            ["units"] = "METRIC"
        };

        if (request.Waypoints.Count > 0)
        {
            body["intermediates"] = new JsonArray([.. request.Waypoints.Select(point => (JsonNode)Waypoint(point))]);
        }

        // Only driving takes a preference, and only this one keeps the request in Essentials.
        if (request.Mode == TravelMode.Driving)
        {
            body["routingPreference"] = "TRAFFIC_UNAWARE";
        }

        if (request.Mode == TravelMode.Transit && request.DepartAt is { } departAt)
        {
            body["departureTime"] = departAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(request.Language))
        {
            body["languageCode"] = request.Language;
        }

        return body;
    }

    private static JsonObject Waypoint(GeoCoordinate point) => new()
    {
        ["location"] = new JsonObject
        {
            ["latLng"] = new JsonObject
            {
                ["latitude"] = point.Latitude,
                ["longitude"] = point.Longitude
            }
        }
    };

    internal static IReadOnlyList<PlannedRoute> ParseRoutes(JsonObject? body, TravelMode mode)
    {
        if (body?["routes"] is not JsonArray routes) return [];

        var planned = new List<PlannedRoute>();

        foreach (var route in routes.OfType<JsonObject>())
        {
            var legs = new List<RouteLeg>();
            var geometry = new List<GeoCoordinate>();

            foreach (var leg in (route["legs"] as JsonArray ?? []).OfType<JsonObject>())
            {
                var steps = (leg["steps"] as JsonArray ?? []).OfType<JsonObject>().ToList();

                foreach (var step in steps)
                {
                    AppendGeometry(geometry, step["polyline"]?["encodedPolyline"]?.GetValue<string>());
                }

                legs.AddRange(mode == TravelMode.Transit
                    ? TransitLegs(steps)
                    : [SingleLeg(leg, steps, mode)]);
            }

            // Steps carry the dense line. Fall back to the route's overview only when no step had one.
            if (geometry.Count == 0)
            {
                AppendGeometry(geometry, route["polyline"]?["encodedPolyline"]?.GetValue<string>());
            }

            planned.Add(new PlannedRoute
            {
                Summary = Text(route["description"]),
                DistanceMetres = Number(route["distanceMeters"]),
                DurationSeconds = Seconds(route["duration"]),
                DurationInTrafficSeconds = null,
                Legs = legs,
                Geometry = geometry,
                Warnings = [.. (route["warnings"] as JsonArray ?? []).Select(Text).OfType<string>()]
            });
        }

        return planned;
    }

    private static RouteLeg SingleLeg(JsonObject leg, IReadOnlyList<JsonObject> steps, TravelMode mode) =>
        new()
        {
            Mode = mode,
            DistanceMetres = Number(leg["distanceMeters"]),
            DurationSeconds = Seconds(leg["duration"]),
            Steps = [.. steps.Select(Step)]
        };

    /// <summary>
    /// A transit journey is one Google leg whose steps alternate between walking and riding. The
    /// platform's legs are one per mode segment, so consecutive walking steps become one walking leg
    /// and each ride becomes its own leg carrying its line, stops and times.
    /// </summary>
    private static IEnumerable<RouteLeg> TransitLegs(IReadOnlyList<JsonObject> steps)
    {
        var walking = new List<JsonObject>();

        foreach (var step in steps)
        {
            if (step["transitDetails"] is JsonObject transit)
            {
                if (walking.Count > 0)
                {
                    yield return WalkingLeg(walking);
                    walking = [];
                }

                yield return RideLeg(step, transit);
            }
            else
            {
                walking.Add(step);
            }
        }

        if (walking.Count > 0) yield return WalkingLeg(walking);
    }

    private static RouteLeg WalkingLeg(IReadOnlyList<JsonObject> steps) => new()
    {
        Mode = TravelMode.Walking,
        DistanceMetres = steps.Sum(step => Number(step["distanceMeters"])),
        DurationSeconds = steps.Sum(step => Seconds(step["staticDuration"])),
        Steps = [.. steps.Select(Step)]
    };

    private static RouteLeg RideLeg(JsonObject step, JsonObject transit)
    {
        var line = transit["transitLine"] as JsonObject;
        var stops = transit["stopDetails"] as JsonObject;

        return new RouteLeg
        {
            Mode = TravelMode.Transit,
            DistanceMetres = Number(step["distanceMeters"]),
            DurationSeconds = Seconds(step["staticDuration"]),
            Steps = [Step(step) with { Manoeuvre = "board" }],
            Line = Text(line?["nameShort"]) ?? Text(line?["name"]),
            Agency = Text((line?["agencies"] as JsonArray)?.FirstOrDefault()?["name"]),
            Headsign = Text(transit["headsign"]),
            DepartureStop = Text(stops?["departureStop"]?["name"]),
            ArrivalStop = Text(stops?["arrivalStop"]?["name"]),
            DepartureAt = Time(stops?["departureTime"]),
            ArrivalAt = Time(stops?["arrivalTime"]),
            StopCount = transit["stopCount"] is JsonValue count && count.TryGetValue<int>(out var stopCount)
                ? stopCount
                : null
        };
    }

    private static RouteStep Step(JsonObject step)
    {
        var instruction = step["navigationInstruction"] as JsonObject;
        var at = step["startLocation"]?["latLng"];

        return new RouteStep
        {
            Manoeuvre = Manoeuvre(Text(instruction?["maneuver"])),
            Instruction = Text(instruction?["instructions"]) ?? string.Empty,
            DistanceMetres = Number(step["distanceMeters"]),
            DurationSeconds = Seconds(step["staticDuration"]),
            At = at is null
                ? null
                : GeoCoordinate.TryCreate(Number(at["latitude"], double.NaN), Number(at["longitude"], double.NaN)) is
                    { IsSuccess: true } point
                    ? point.Value
                    : null
        };
    }

    /// <summary>Google's manoeuvres in the client's vocabulary (<c>ManoeuvreKind</c> in the app).</summary>
    internal static string Manoeuvre(string? google) => google switch
    {
        "DEPART" => "depart",
        "TURN_LEFT" => "turn-left",
        "TURN_RIGHT" => "turn-right",
        "TURN_SLIGHT_LEFT" or "RAMP_LEFT" => "slight-left",
        "TURN_SLIGHT_RIGHT" or "RAMP_RIGHT" => "slight-right",
        "TURN_SHARP_LEFT" => "sharp-left",
        "TURN_SHARP_RIGHT" => "sharp-right",
        "UTURN_LEFT" or "UTURN_RIGHT" => "uturn",
        "ROUNDABOUT_LEFT" or "ROUNDABOUT_RIGHT" => "roundabout",
        "MERGE" => "merge",
        "FORK_LEFT" or "FORK_RIGHT" => "fork",
        "FERRY" or "FERRY_TRAIN" => "board",
        _ => "straight"
    };

    private static void AppendGeometry(List<GeoCoordinate> geometry, string? encoded)
    {
        foreach (var (latitude, longitude) in GooglePolyline.Decode(encoded))
        {
            // Consecutive steps share their joining point; keeping both would put a zero-length
            // segment in the line the navigation machine matches against.
            if (geometry.Count > 0
                && geometry[^1].Latitude == latitude
                && geometry[^1].Longitude == longitude)
            {
                continue;
            }

            if (GeoCoordinate.TryCreate(latitude, longitude) is { IsSuccess: true } point)
            {
                geometry.Add(point.Value);
            }
        }
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    private static double Number(JsonNode? node, double fallback = 0d) =>
        node is JsonValue value && value.TryGetValue<double>(out var number) ? number : fallback;

    /// <summary>Google writes durations as <c>"123s"</c> or <c>"3.5s"</c>.</summary>
    internal static double Seconds(JsonNode? node)
    {
        var text = Text(node);
        if (text is null || !text.EndsWith('s')) return 0d;

        return double.TryParse(text[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            ? seconds
            : 0d;
    }

    private static DateTimeOffset? Time(JsonNode? node) =>
        Text(node) is { } text
        && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time)
            ? time
            : null;
}
