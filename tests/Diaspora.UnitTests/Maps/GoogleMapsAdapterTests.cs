using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Diaspora.Maps.Google;
using Diaspora.Modules.GeographicIntelligence.Domain.Routing;
using Diaspora.Modules.Location.Domain.Places;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Platform.Usage;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diaspora.UnitTests.Maps;

/// <summary>
/// The Google adapters against a fake transport — 27 Sep 2026.
/// <para>
/// The response bodies are shaped as Google's reference documents them, which on this platform is
/// the weakest kind of evidence: provider documentation has been wrong here before, and a fixture
/// written from the same page agrees with the bug. So these tests pin the rules the adapter adds —
/// which SKU a request can reach, that a spent allowance never calls out, what a refusal becomes —
/// and the shapes are confirmed against the live API before anything relies on them.
/// </para>
/// </summary>
public sealed class GoogleMapsAdapterTests
{
    private static readonly GoogleMapsOptions Configured = new() { ApiKey = "test-server-key" };

    // ── Polyline ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_polyline_decoder_reproduces_Googles_worked_example()
    {
        // The example in Google's own description of the algorithm.
        var points = GooglePolyline.Decode("_p~iF~ps|U_ulLnnqC_mqNvxq`@");

        points.Should().Equal((38.5, -120.2), (40.7, -120.95), (43.252, -126.453));
    }

    [Fact]
    public void A_truncated_polyline_keeps_the_points_before_the_damage()
    {
        GooglePolyline.Decode("_p~iF~ps|U_ulL").Should().Equal((38.5, -120.2));
        GooglePolyline.Decode(null).Should().BeEmpty();
    }

    // ── Routing: what reaches Google ─────────────────────────────────────────────────────────

    [Fact]
    public void Driving_is_always_traffic_unaware_which_keeps_it_in_the_Essentials_SKU()
    {
        var body = GoogleRoutingProvider.BuildBody(Request(TravelMode.Driving));

        body["travelMode"]!.GetValue<string>().Should().Be("DRIVE");
        body["routingPreference"]!.GetValue<string>().Should().Be("TRAFFIC_UNAWARE");
    }

    [Theory]
    [InlineData(TravelMode.Walking, "WALK")]
    [InlineData(TravelMode.Cycling, "BICYCLE")]
    [InlineData(TravelMode.Transit, "TRANSIT")]
    public void Other_modes_send_no_routing_preference_because_Google_refuses_one(TravelMode mode, string wire)
    {
        var body = GoogleRoutingProvider.BuildBody(Request(mode));

        body["travelMode"]!.GetValue<string>().Should().Be(wire);
        body.ContainsKey("routingPreference").Should().BeFalse();
    }

    [Fact]
    public void A_transit_departure_is_sent_in_UTC()
    {
        var body = GoogleRoutingProvider.BuildBody(Request(TravelMode.Transit) with
        {
            DepartAt = new DateTimeOffset(2026, 9, 28, 9, 30, 0, TimeSpan.FromHours(1))
        });

        body["departureTime"]!.GetValue<string>().Should().Be("2026-09-28T08:30:00Z");
    }

    [Fact]
    public async Task The_key_and_field_mask_travel_as_headers_and_never_in_the_url()
    {
        var transport = new FakeTransport(_ => (HttpStatusCode.OK, RouteBody));
        var provider = Routing(transport, new FakeMeter());

        await provider.PlanAsync(Request(TravelMode.Driving));

        var sent = transport.Requests.Single();
        sent.Uri.Should().Be("https://routes.googleapis.com/directions/v2:computeRoutes");
        sent.Headers["X-Goog-Api-Key"].Should().Be("test-server-key");
        sent.Headers["X-Goog-FieldMask"].Should().Be(GoogleRoutingProvider.FieldMask);
        sent.Uri.Should().NotContain("key");
    }

    // ── Routing: the allowance ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_spent_allowance_refuses_without_calling_Google()
    {
        var transport = new FakeTransport(_ => (HttpStatusCode.OK, RouteBody));
        var provider = Routing(transport, new FakeMeter { Remaining = 0 });

        var result = await provider.PlanAsync(Request(TravelMode.Driving));

        result.Outcome.Should().Be(RouteOutcome.AllowanceExhausted);
        transport.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task A_request_Google_would_refuse_costs_no_allowance()
    {
        var meter = new FakeMeter();
        var provider = Routing(new FakeTransport(_ => (HttpStatusCode.OK, RouteBody)), meter);

        var result = await provider.PlanAsync(Request(TravelMode.Transit) with
        {
            Waypoints = [GeoCoordinate.Create(-20.25, 57.48)]
        });

        result.Outcome.Should().Be(RouteOutcome.Unsupported);
        meter.Taken.Should().BeEmpty();
    }

    [Fact]
    public async Task Each_call_takes_one_unit_of_the_routes_essentials_allowance_in_Pacific_time()
    {
        var meter = new FakeMeter();
        var provider = Routing(new FakeTransport(_ => (HttpStatusCode.OK, RouteBody)), meter);

        await provider.PlanAsync(Request(TravelMode.Walking));

        meter.Taken.Should().ContainSingle().Which.Should().Be(
            ("google", "routes-compute-essentials", 9_000, "America/Los_Angeles"));
    }

    // ── Routing: what comes back ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_route_is_parsed_with_dense_geometry_and_no_traffic_figure()
    {
        var provider = Routing(new FakeTransport(_ => (HttpStatusCode.OK, RouteBody)), new FakeMeter());

        var result = await provider.PlanAsync(Request(TravelMode.Walking));

        result.Outcome.Should().Be(RouteOutcome.Success);
        var route = result.Routes.Should().ContainSingle().Subject;
        route.DistanceMetres.Should().Be(1234);
        route.DurationSeconds.Should().Be(905.5);
        route.DurationInTrafficSeconds.Should().BeNull();
        route.Summary.Should().Be("Rue Royale");
        route.Warnings.Should().ContainSingle().Which.Should().StartWith("Walking directions are in beta");

        // Two points then three, sharing the joining point: four distinct points.
        route.Geometry.Should().HaveCount(4);

        var steps = route.Legs.Should().ContainSingle().Subject.Steps;
        steps.Select(step => step.Manoeuvre).Should().Equal("depart", "turn-left");
        steps[1].Instruction.Should().Be("Turn left onto Rue Royale");
        steps[1].At!.Latitude.Should().Be(40.7);
    }

    [Fact]
    public async Task An_empty_answer_is_no_route()
    {
        var provider = Routing(new FakeTransport(_ => (HttpStatusCode.OK, "{}")), new FakeMeter());

        (await provider.PlanAsync(Request(TravelMode.Driving))).Outcome.Should().Be(RouteOutcome.NoRoute);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, RouteOutcome.RateLimited)]
    [InlineData(HttpStatusCode.Forbidden, RouteOutcome.Unavailable)]
    [InlineData(HttpStatusCode.BadRequest, RouteOutcome.InvalidRequest)]
    [InlineData(HttpStatusCode.InternalServerError, RouteOutcome.Unavailable)]
    public async Task A_refusal_becomes_a_status_and_never_carries_Googles_body(
        HttpStatusCode status, RouteOutcome expected)
    {
        var provider = Routing(
            new FakeTransport(_ => (status, """{"error":{"message":"origin -20.24,57.47 ..."}}""")),
            new FakeMeter());

        var result = await provider.PlanAsync(Request(TravelMode.Driving));

        result.Outcome.Should().Be(expected);
        (result.Detail ?? string.Empty).Should().NotContain("-20.24");
    }

    [Fact]
    public void A_transit_journey_becomes_walking_and_riding_legs()
    {
        var body = JsonNode.Parse(TransitBody)!.AsObject();

        var route = GoogleRoutingProvider.ParseRoutes(body, TravelMode.Transit).Single();

        route.Legs.Select(leg => leg.Mode).Should().Equal(TravelMode.Walking, TravelMode.Transit, TravelMode.Walking);
        var ride = route.Legs[1];
        ride.Line.Should().Be("198");
        ride.Agency.Should().Be("Rose Hill Transport");
        ride.Headsign.Should().Be("Port Louis");
        ride.DepartureStop.Should().Be("Rose Hill Bus Station");
        ride.StopCount.Should().Be(12);
        ride.DepartureAt.Should().Be(new DateTimeOffset(2026, 9, 28, 5, 30, 0, TimeSpan.Zero));
        ride.Steps.Single().Manoeuvre.Should().Be("board");
    }

    [Fact]
    public void Routing_is_withheld_until_a_key_exists()
    {
        var provider = Routing(new FakeTransport(_ => (HttpStatusCode.OK, "{}")), new FakeMeter(), new GoogleMapsOptions());

        provider.SupportedModes.Should().BeEmpty();
    }

    // ── Places ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Autocomplete_sends_the_session_a_bias_and_the_regions()
    {
        var body = GooglePlaceSearchProvider.BuildAutocompleteBody(new PlaceAutocompleteRequest
        {
            Input = "12 Royal Road",
            SessionToken = "0f8fad5b-d9cb-469f-a165-70867728950e",
            Near = GeoCoordinate.Create(-20.24, 57.47),
            CountryCodes = ["MU", "GB"],
            Language = "en"
        });

        body["sessionToken"]!.GetValue<string>().Should().Be("0f8fad5b-d9cb-469f-a165-70867728950e");
        body["locationBias"]!["circle"]!["radius"]!.GetValue<double>().Should().Be(20_000);
        body["includedRegionCodes"]!.AsArray().Select(code => code!.GetValue<string>())
            .Should().Equal("mu", "gb");
    }

    [Fact]
    public async Task Suggestions_skip_query_predictions_and_keep_the_structured_text()
    {
        var provider = Places(new FakeTransport(_ => (HttpStatusCode.OK, SuggestionBody)), new FakeMeter());

        var result = await provider.AutocompleteAsync(Autocomplete());

        result.IsSuccess.Should().BeTrue();
        var suggestion = result.Value.Should().ContainSingle().Subject;
        suggestion.PlaceId.Should().Be("ChIJ-place-one");
        suggestion.MainText.Should().Be("Royal Road");
        suggestion.SecondaryText.Should().Be("Rose Hill, Mauritius");
    }

    [Fact]
    public async Task Details_ask_only_for_Essentials_fields_and_read_a_UK_postal_town()
    {
        var transport = new FakeTransport(_ => (HttpStatusCode.OK, DetailBody));
        var provider = Places(transport, new FakeMeter());

        var result = await provider.GetDetailsAsync(new PlaceDetailsRequest
        {
            PlaceId = "ChIJ-place-two",
            SessionToken = "0f8fad5b-d9cb-469f-a165-70867728950e"
        });

        transport.Requests.Single().Headers["X-Goog-FieldMask"].Should().NotContain("displayName");
        transport.Requests.Single().Uri.Should().Contain("sessionToken=0f8fad5b");

        var place = result.Value!;
        place.Coordinate.Latitude.Should().Be(51.4741);
        place.CountryCode.Should().Be("GB");
        place.Locality.Should().Be("London");
        place.PostalCode.Should().Be("SE15 4ST");
    }

    [Fact]
    public async Task A_spent_places_allowance_refuses_without_calling_Google()
    {
        var transport = new FakeTransport(_ => (HttpStatusCode.OK, SuggestionBody));
        var provider = Places(transport, new FakeMeter { Remaining = 0 });

        var result = await provider.AutocompleteAsync(Autocomplete());

        result.Outcome.Should().Be(PlaceSearchOutcome.AllowanceExhausted);
        transport.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task An_unknown_place_is_not_found()
    {
        var provider = Places(new FakeTransport(_ => (HttpStatusCode.NotFound, "{}")), new FakeMeter());

        var result = await provider.GetDetailsAsync(new PlaceDetailsRequest
        {
            PlaceId = "ChIJ-gone-away",
            SessionToken = "0f8fad5b-d9cb-469f-a165-70867728950e"
        });

        result.Outcome.Should().Be(PlaceSearchOutcome.NotFound);
    }

    // ── Options ──────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(nameof(GoogleMapsOptions.RoutesMonthlyAllowance))]
    [InlineData(nameof(GoogleMapsOptions.AutocompleteMonthlyAllowance))]
    [InlineData(nameof(GoogleMapsOptions.PlaceDetailsMonthlyAllowance))]
    public void No_allowance_may_exceed_Googles_free_monthly_cap(string property)
    {
        var options = new GoogleMapsOptions { ApiKey = "k" };
        typeof(GoogleMapsOptions).GetProperty(property)!.SetValue(options, 10_001);

        var valid = Validator.TryValidateObject(
            options, new ValidationContext(options), [], validateAllProperties: true);

        valid.Should().BeFalse();
    }

    // ── Fixtures ─────────────────────────────────────────────────────────────────────────────

    private static RoutePlanRequest Request(TravelMode mode) => new()
    {
        Origin = GeoCoordinate.Create(-20.2410, 57.4769),
        Destination = GeoCoordinate.Create(-20.1609, 57.4989),
        Mode = mode
    };

    private static PlaceAutocompleteRequest Autocomplete() => new()
    {
        Input = "Royal Road",
        SessionToken = "0f8fad5b-d9cb-469f-a165-70867728950e"
    };

    private static GoogleRoutingProvider Routing(
        FakeTransport transport, FakeMeter meter, GoogleMapsOptions? options = null) =>
        new(
            new HttpClient(transport, disposeHandler: false) { BaseAddress = new Uri("https://routes.googleapis.com/") },
            meter,
            new StaticOptionsMonitor<GoogleMapsOptions>(options ?? Configured),
            NullLogger<GoogleRoutingProvider>.Instance);

    private static GooglePlaceSearchProvider Places(FakeTransport transport, FakeMeter meter) =>
        new(
            new HttpClient(transport, disposeHandler: false) { BaseAddress = new Uri("https://places.googleapis.com/") },
            meter,
            new StaticOptionsMonitor<GoogleMapsOptions>(Configured),
            NullLogger<GooglePlaceSearchProvider>.Instance);

    // Two steps: (38.5,-120.2)→(40.7,-120.95) and (40.7,-120.95)→(43.252,-126.453)→(44,-127).
    private const string RouteBody = """
        {"routes":[{"distanceMeters":1234,"duration":"905.5s","description":"Rue Royale",
          "warnings":["Walking directions are in beta. Use caution."],
          "polyline":{"encodedPolyline":"_p~iF~ps|U"},
          "legs":[{"distanceMeters":1234,"duration":"905.5s","steps":[
            {"distanceMeters":400,"staticDuration":"300s","travelMode":"WALK",
             "polyline":{"encodedPolyline":"_p~iF~ps|U_ulLnnqC"},
             "startLocation":{"latLng":{"latitude":38.5,"longitude":-120.2}},
             "navigationInstruction":{"maneuver":"DEPART","instructions":"Head north"}},
            {"distanceMeters":834,"staticDuration":"605.5s","travelMode":"WALK",
             "polyline":{"encodedPolyline":"_flwFn`faV_mqNvxq`@_bqCvyiB"},
             "startLocation":{"latLng":{"latitude":40.7,"longitude":-120.95}},
             "navigationInstruction":{"maneuver":"TURN_LEFT","instructions":"Turn left onto Rue Royale"}}
          ]}]}]}
        """;

    private const string TransitBody = """
        {"routes":[{"distanceMeters":9000,"duration":"2400s","legs":[{"distanceMeters":9000,"duration":"2400s","steps":[
          {"distanceMeters":200,"staticDuration":"180s","travelMode":"WALK","navigationInstruction":{"maneuver":"DEPART","instructions":"Walk to Rose Hill Bus Station"}},
          {"distanceMeters":150,"staticDuration":"120s","travelMode":"WALK","navigationInstruction":{"instructions":"Continue"}},
          {"distanceMeters":8500,"staticDuration":"1920s","travelMode":"TRANSIT","navigationInstruction":{"instructions":"Bus towards Port Louis"},
           "transitDetails":{"headsign":"Port Louis","stopCount":12,
             "stopDetails":{"departureStop":{"name":"Rose Hill Bus Station"},"arrivalStop":{"name":"Victoria Square"},
               "departureTime":"2026-09-28T05:30:00Z","arrivalTime":"2026-09-28T06:02:00Z"},
             "transitLine":{"name":"Rose Hill - Port Louis","nameShort":"198","agencies":[{"name":"Rose Hill Transport"}]}}},
          {"distanceMeters":150,"staticDuration":"180s","travelMode":"WALK","navigationInstruction":{"instructions":"Walk to destination"}}
        ]}]}]}
        """;

    private const string SuggestionBody = """
        {"suggestions":[
          {"placePrediction":{"placeId":"ChIJ-place-one","text":{"text":"Royal Road, Rose Hill, Mauritius"},
            "structuredFormat":{"mainText":{"text":"Royal Road"},"secondaryText":{"text":"Rose Hill, Mauritius"}},
            "types":["route","geocode"]}},
          {"queryPrediction":{"text":{"text":"royal road shops"}}}
        ]}
        """;

    private const string DetailBody = """
        {"id":"ChIJ-place-two","formattedAddress":"133 Rye Ln, London SE15 4ST, UK",
         "location":{"latitude":51.4741,"longitude":-0.0681},
         "addressComponents":[
           {"longText":"133","shortText":"133","types":["street_number"]},
           {"longText":"London","shortText":"London","types":["postal_town"]},
           {"longText":"United Kingdom","shortText":"GB","types":["country","political"]},
           {"longText":"SE15 4ST","shortText":"SE15 4ST","types":["postal_code"]}]}
        """;

    private sealed class FakeMeter : IProviderUsageMeter
    {
        public int Remaining { get; set; } = int.MaxValue;

        public List<(string Provider, string Sku, int Allowance, string Zone)> Taken { get; } = [];

        public Task<bool> TryConsumeAsync(
            string provider, string sku, int monthlyAllowance, string billingTimeZoneId,
            CancellationToken cancellationToken = default)
        {
            if (Remaining <= 0) return Task.FromResult(false);

            Remaining--;
            Taken.Add((provider, sku, monthlyAllowance, billingTimeZoneId));
            return Task.FromResult(true);
        }

        public Task<int> GetUsedAsync(
            string provider, string sku, string billingTimeZoneId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Taken.Count);
    }

    private sealed record SentRequest(string Uri, IReadOnlyDictionary<string, string> Headers);

    private sealed class FakeTransport(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> answer)
        : HttpMessageHandler
    {
        public List<SentRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new SentRequest(
                request.RequestUri!.ToString(),
                request.Headers.ToDictionary(header => header.Key, header => string.Join(",", header.Value))));

            var (status, body) = answer(request);

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
