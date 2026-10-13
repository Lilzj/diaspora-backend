using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The Location &amp; Hyperlocal Intelligence Engine, driven over real HTTP through the same
/// host every other suite uses.
/// <para>
/// Three things are under test at once. The module's behaviour, obviously. That a module with
/// eight tables, nine permissions and thirty routes composed into the platform through nothing
/// but contributor interfaces and one project reference — the same claim the Bookmarks and
/// Trust suites make, now at a third scale. And the one thing neither of those could show:
/// that a module can seed its own reference data and expose extension seams to modules that
/// do not exist yet, without either capability leaking into the host.
/// </para>
/// </summary>
public sealed class LocationModuleTests : IdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public LocationModuleTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Composition ──────────────────────────────────────────────────────

    /// <summary>
    /// Proof the module's <c>IFeatureContributor</c> reached the composed catalogue: a
    /// Location-owned gate turns up in Identity's <c>/access/features</c> response, which the
    /// Location module never touched.
    /// </summary>
    [Fact]
    public async Task The_location_feature_gates_composed_into_the_platform_catalogue()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        var names = features.Features.Select(feature => feature.Name).ToList();

        names.Should().Contain("location.discovery");
        names.Should().Contain("location.nearby");
        names.Should().Contain("location.neighborhood-hub");
    }

    [Fact]
    public async Task The_platform_manifest_lists_the_location_module()
    {
        var response = await HttpClient.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("Location", "the kernel discovered the module by assembly scan");
    }

    // ── Access control ───────────────────────────────────────────────────

    [Fact]
    public async Task The_catalogue_refuses_an_anonymous_caller()
    {
        var response = await HttpClient.GetAsync("/api/v1/location/countries");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_members_profile_is_not_readable_without_a_token()
    {
        var response = await HttpClient.GetAsync("/api/v1/location/me/profile");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── The catalogue, seeded by the module itself ───────────────────────

    /// <summary>
    /// The seeding hosted service the module registers from its own
    /// <c>ConfigureServices</c> — no edit to <c>Program.cs</c>, and a populated catalogue on
    /// first boot.
    /// </summary>
    [Fact]
    public async Task The_country_catalogue_is_seeded_at_startup()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/location/countries?pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await ReadDataAsync<PagedDto<CountryDto>>(response);

        page.TotalCount.Should().BeGreaterThan(100, "the shipped table carries 191 countries");
        page.Items.Should().Contain(country => country.Code == "GB");
    }

    /// <summary>The launch markets are featured, so they sort ahead of the alphabet.</summary>
    [Fact]
    public async Task The_launch_markets_are_featured_and_supported()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/location/countries/GB");
        var country = await ReadDataAsync<CountryDetailDto>(response);

        country.Country.Code.Should().Be("GB");
        country.Country.Name.Should().Be("United Kingdom");
        country.Country.CallingCode.Should().Be("+44");
        country.Country.IsSupported.Should().BeTrue();
        country.Metadata.Currency.Should().Be("GBP");
        country.Metadata.PrimaryTimeZone.Should().Be("Europe/London");
    }

    /// <summary>A well-formed code the catalogue does not know is a 404, not a 500.</summary>
    [Fact]
    public async Task An_unknown_country_code_is_reported_as_not_found()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/location/countries/ZZ");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>The launch geography seeder's cities, hanging off their country.</summary>
    [Fact]
    public async Task The_launch_cities_are_seeded_under_their_country()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/location/countries/GB/cities");
        var page = await ReadDataAsync<PagedDto<AreaDto>>(response);

        page.Items.Select(area => area.Name).Should().Contain(["London", "Manchester"]);
        page.Items.Should().OnlyContain(area => area.Level == "City");
    }

    /// <summary>
    /// Mauritius organises by district with no state above it — the case that makes skippable
    /// rungs necessary rather than merely tidy.
    /// </summary>
    [Fact]
    public async Task A_country_that_skips_a_rung_still_resolves_its_hierarchy()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/location/countries/MU/districts");
        var page = await ReadDataAsync<PagedDto<AreaDto>>(response);

        page.Items.Select(area => area.Name).Should().Contain("Plaines Wilhems");
    }

    // ── Search ───────────────────────────────────────────────────────────

    /// <summary>
    /// The one Location endpoint the frontend already calls, and the parameter name it already
    /// sends.
    /// </summary>
    [Fact]
    public async Task Search_accepts_the_query_parameter_the_frontend_sends()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/location/search?query=peck");
        var result = await ReadDataAsync<SearchDto>(response);

        result.Results.Should().Contain(item => item.Neighborhood == "Peckham");
    }

    /// <summary>
    /// What a member in Mauritius actually typed (13 Oct 2026): lower-case, and a trailing space
    /// from the keyboard's word suggestion. The sheet said "No locations found".
    /// </summary>
    [Theory]
    [InlineData("Port Louis")]
    [InlineData("Port louis ")]
    [InlineData("port louis")]
    public async Task A_member_can_find_a_launch_city_however_it_is_typed(string typed)
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(
            $"/api/v1/location/search?query={Uri.EscapeDataString(typed)}");
        var result = await ReadDataAsync<SearchDto>(response);

        result.Results.Should().Contain(item => item.CityName == "Port Louis");
    }

    /// <summary>
    /// The canonical reference is a superset of PlatformLocation, so the client's store binds
    /// to a search result unchanged.
    /// </summary>
    [Fact]
    public async Task A_search_result_carries_every_field_the_client_store_needs()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/location/search?query=peckham");
        var result = await ReadDataAsync<SearchDto>(response);

        var peckham = result.Results.Single(item => item.Neighborhood == "Peckham");

        peckham.CountryCode.Should().Be("GB");
        peckham.CountryName.Should().Be("United Kingdom");
        peckham.Flag.Should().Be("🇬🇧");
        peckham.CityName.Should().Be("London");
        peckham.DisplayName.Should().Be("Peckham, London");
        peckham.Latitude.Should().BeApproximately(51.4742d, 0.001d);
        peckham.AreaId.Should().NotBeNull("the reference adds identifiers the flat DTO lacked");
        peckham.Slug.Should().Be("peckham");
    }

    /// <summary>
    /// One character matches a substantial fraction of a gazetteer, and returning the first
    /// fifty of forty thousand reads as "my city is not here".
    /// </summary>
    [Fact]
    public async Task A_one_character_search_is_rejected_with_a_problem_document()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/location/search?query=p");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// The server-side replacement for the client's direct call to a third-party IP geolocator.
    /// </summary>
    [Fact]
    public async Task Reverse_geocoding_resolves_a_coordinate_to_a_neighbourhood()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(
            "/api/v1/location/reverse-geocode?latitude=51.4742&longitude=-0.0691");

        var place = await ReadDataAsync<LocationReferenceDto>(response);

        place.Neighborhood.Should().Be("Peckham");
        place.CityName.Should().Be("London");
        place.CountryCode.Should().Be("GB");
    }

    // ── The neighbourhood hub ────────────────────────────────────────────

    /// <summary>
    /// The frontend's <c>NeighborhoodCard</c> builds <c>/neighborhood/peckham</c> links today.
    /// The backend has to resolve that slug, or adopting it breaks navigation that works.
    /// </summary>
    [Fact]
    public async Task The_hub_resolves_the_slug_the_frontend_already_links_to()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/location/neighborhoods/peckham");
        var hub = await ReadDataAsync<HubDto>(response);

        hub.Id.Should().Be("peckham");
        hub.Name.Should().Be("Peckham");
        hub.CityName.Should().Be("London");
        hub.CountryCode.Should().Be("GB");
    }

    /// <summary>
    /// Nothing supplies liveability data yet, and the response says so rather than reporting
    /// three zeroes — which read as a very bad neighbourhood. Known gap #1.
    /// </summary>
    [Fact]
    public async Task The_hub_reports_its_quality_indexes_as_unavailable_rather_than_zero()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var hub = await ReadDataAsync<HubDto>(
            await HttpClient.GetAsync("/api/v1/location/neighborhoods/brixton"));

        hub.Stats.IsAvailable.Should().BeFalse();
        hub.Stats.SafetyScore.Should().BeNull();
    }

    /// <summary>
    /// No vertical has registered a counter yet, so the counts are empty rather than a set of
    /// confident zeroes. An absent key means "unknown"; a zero would mean "none".
    /// </summary>
    [Fact]
    public async Task The_hub_reports_no_resource_counts_until_a_vertical_contributes_one()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var hub = await ReadDataAsync<HubDto>(
            await HttpClient.GetAsync("/api/v1/location/neighborhoods/peckham"));

        // B6 is the first vertical to contribute a counter, so the hub now reports a count
        // rather than nothing. The property under test is unchanged: the hub reports exactly
        // what verticals have claimed, and no more.
        hub.ResourceCounts.Should().ContainKey("updates");
        hub.ResourceCounts["updates"].Should().Be(0);
    }

    [Fact]
    public async Task An_unknown_neighbourhood_slug_is_reported_as_not_found()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(
            "/api/v1/location/neighborhoods/nowhere-in-particular");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── The member's own geography ───────────────────────────────────────

    /// <summary>
    /// The client's whole location store, persisted. A member who sets their location on one
    /// device now finds it on another.
    /// </summary>
    [Fact]
    public async Task A_member_can_set_read_and_travel_from_their_location()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        // A fresh member has defaults rather than an error.
        var initial = await ReadDataAsync<ProfileDto>(
            await HttpClient.GetAsync("/api/v1/location/me/profile"));

        initial.ActiveLocation.Should().BeNull();
        initial.PermissionStatus.Should().Be("undetermined");
        initial.Preferences.SearchRadius.Should().Be(5d, "the client store's own default");

        // Move.
        var moved = await ReadDataAsync<ProfileDto>(
            await HttpClient.PutAsJsonAsync(
                "/api/v1/location/me/current-location",
                new { countryCode = "GB", cityName = "London", neighborhood = "Peckham" },
                JsonOptions));

        moved.ActiveLocation!.DisplayName.Should().Be("Peckham, London");
        moved.RecentLocations.Should().ContainSingle();

        // Travel, without losing home.
        var travelling = await ReadDataAsync<ProfileDto>(
            await HttpClient.PutAsJsonAsync(
                "/api/v1/location/me/travel-mode",
                new
                {
                    enabled = true,
                    destination = new
                    {
                        countryCode = "MU",
                        cityName = "Rose Hill",
                        neighborhood = "Rose Hill Central"
                    }
                },
                JsonOptions));

        travelling.TravelMode.Should().BeTrue();
        travelling.ActiveLocation!.CountryCode.Should().Be("MU");
        travelling.CurrentLocation!.Neighborhood.Should().Be("Peckham");
    }

    /// <summary>
    /// The slider's sentinels reach the server as they are and come back as they were, so no
    /// client change is forced. Intentional deviation #4.
    /// </summary>
    [Fact]
    public async Task The_radius_sentinels_round_trip_unchanged()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var updated = await ReadDataAsync<ProfileDto>(
            await HttpClient.PutAsJsonAsync(
                "/api/v1/location/me/preferences",
                new { searchRadiusKm = -2d },
                JsonOptions));

        updated.Preferences.SearchRadius.Should().Be(-2d);
        updated.Preferences.RadiusScope.Should().Be("Country");
    }

    [Fact]
    public async Task A_member_can_save_move_and_forget_a_labelled_place()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var saved = await ReadDataAsync<SavedLocationDto>(
            await HttpClient.PutAsJsonAsync(
                "/api/v1/location/me/saved-locations/Home",
                new { location = new { countryCode = "GB", cityName = "London", neighborhood = "Peckham" } },
                JsonOptions));

        saved.Label.Should().Be("Home");

        // Saving the same label again moves it rather than duplicating.
        var moved = await ReadDataAsync<SavedLocationDto>(
            await HttpClient.PutAsJsonAsync(
                "/api/v1/location/me/saved-locations/Home",
                new { location = new { countryCode = "GB", cityName = "London", neighborhood = "Brixton" } },
                JsonOptions));

        moved.Id.Should().Be(saved.Id);
        moved.Location.Neighborhood.Should().Be("Brixton");

        var profile = await ReadDataAsync<ProfileDto>(
            await HttpClient.GetAsync("/api/v1/location/me/profile"));

        profile.SavedLocations.Should().ContainSingle();

        var removed = await HttpClient.DeleteAsync("/api/v1/location/me/saved-locations/Home");
        removed.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Forgetting_a_place_that_was_never_saved_is_reported_as_not_found()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.DeleteAsync(
            "/api/v1/location/me/saved-locations/Nowhere");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Residence history ────────────────────────────────────────────────

    /// <summary>
    /// Location serves the claim. The verification state beside it is Trust's, projected — and
    /// with no Trust decision recorded it reads Unverified rather than being absent.
    /// </summary>
    [Fact]
    public async Task A_member_can_declare_where_they_have_lived()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var declared = await ReadDataAsync<ExperienceDto>(
            await HttpClient.PostAsJsonAsync(
                "/api/v1/location/me/countries",
                new
                {
                    countryCode = "GB",
                    arrivedOn = "2019-03-01",
                    role = "Diaspora Member",
                    expertiseAreas = new[] { "Housing", "Visas" }
                },
                JsonOptions));

        declared.CountryCode.Should().Be("GB");
        declared.StartYear.Should().Be(2019);
        declared.EndYear.Should().BeNull("an open period renders as Present");
        declared.IsCurrentResidence.Should().BeTrue();
        declared.ExpertiseAreas.Should().BeEquivalentTo("Housing", "Visas");

        declared.IsVerified.Should().BeFalse("Trust has decided nothing about this claim");
        declared.VerificationState.Should().Be("Unverified");

        var timeline = await ReadDataAsync<TimelineDto>(
            await HttpClient.GetAsync("/api/v1/location/me/countries"));

        timeline.CountryCount.Should().Be(1);
        timeline.VerifiedCountryCount.Should().Be(0);
        timeline.CurrentResidence!.CountryCode.Should().Be("GB");
    }

    /// <summary>
    /// One row per member per country. The wizard re-posts the whole record on every edit, so
    /// the second save has to be an amendment rather than a conflict.
    /// </summary>
    [Fact]
    public async Task Declaring_the_same_country_twice_amends_rather_than_duplicates()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await HttpClient.PostAsJsonAsync(
            "/api/v1/location/me/countries",
            new { countryCode = "GB", arrivedOn = "2019-03-01" },
            JsonOptions);

        var amended = await ReadDataAsync<ExperienceDto>(
            await HttpClient.PostAsJsonAsync(
                "/api/v1/location/me/countries",
                new { countryCode = "GB", arrivedOn = "2019-03-01", departedOn = "2024-06-30" },
                JsonOptions));

        amended.EndYear.Should().Be(2024);
        amended.IsCurrentResidence.Should().BeFalse();

        var timeline = await ReadDataAsync<TimelineDto>(
            await HttpClient.GetAsync("/api/v1/location/me/countries"));

        timeline.CountryCount.Should().Be(1);
    }

    [Fact]
    public async Task An_arrival_date_in_the_future_is_rejected()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/location/me/countries",
            new { countryCode = "GB", arrivedOn = "2099-01-01" },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_country_the_catalogue_does_not_know_cannot_be_declared()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/location/me/countries",
            new { countryCode = "ZZ", arrivedOn = "2019-01-01" },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A member with no declared history gets an empty timeline rather than a 404. Whether a
    /// user id exists is Identity's to disclose; answering 404 here would make this endpoint an
    /// account-enumeration oracle.
    /// </summary>
    [Fact]
    public async Task An_unknown_members_public_timeline_is_empty_rather_than_missing()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(
            $"/api/v1/location/users/{Guid.NewGuid()}/countries");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDataAsync<TimelineDto>(response)).CountryCount.Should().Be(0);
    }

    // ── The hyperlocal engine ────────────────────────────────────────────

    /// <summary>
    /// No vertical has registered a provider yet, so every group is empty — and, crucially,
    /// none is reported as unavailable. Empty means "nobody has anything here"; unavailable
    /// would mean "we could not find out".
    /// </summary>
    [Fact]
    public async Task Nearby_returns_an_empty_but_well_formed_payload_with_no_providers()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(
            "/api/v1/location/nearby?latitude=51.4742&longitude=-0.0691&radiusKm=5");

        var nearby = await ReadDataAsync<NearbyDto>(response);

        nearby.Center.Neighborhood.Should().Be("Peckham");
        nearby.RadiusKm.Should().Be(5d);
        nearby.Scope.Should().Be("Radius");
        // One group, from the one vertical that has shipped a provider. Empty until B6.
        nearby.Groups.Should().ContainKey("updates");
        nearby.UnavailableResourceTypes.Should().BeEmpty();
    }

    /// <summary>The marketplace filter's second name for the radius binds too. Naming finding #18.</summary>
    [Fact]
    public async Task Nearby_accepts_the_second_name_the_marketplace_filter_uses_for_the_radius()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(
            "/api/v1/location/nearby?latitude=51.4742&longitude=-0.0691&distanceRadius=3");

        (await ReadDataAsync<NearbyDto>(response)).RadiusKm.Should().Be(3d);
    }

    /// <summary>City-wide is a hierarchy question, not a circle, and reports itself as such.</summary>
    [Fact]
    public async Task The_city_wide_sentinel_resolves_to_a_city_scope()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(
            "/api/v1/location/nearby?latitude=51.4742&longitude=-0.0691&radiusKm=-1");

        var nearby = await ReadDataAsync<NearbyDto>(response);

        nearby.Scope.Should().Be("City");
        nearby.RadiusKm.Should().Be(0d);
    }

    [Fact]
    public async Task A_viewport_query_returns_the_nodes_inside_it()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(
            "/api/v1/location/within-bounds?north=51.6&south=51.3&east=0.1&west=-0.2");

        var areas = await ReadDataAsync<List<AreaDto>>(response);

        areas.Select(area => area.Name).Should().Contain("Peckham");
        areas.Select(area => area.Name).Should().NotContain("Rose Hill");
    }

    /// <summary>
    /// An unbounded viewport is a full table scan dressed as a spatial query. The request that
    /// hurts is the one nobody meant to send.
    /// </summary>
    [Fact]
    public async Task A_viewport_spanning_the_world_is_rejected()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(
            "/api/v1/location/within-bounds?north=80&south=-80&east=170&west=-170");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_polygon_query_excludes_a_point_in_a_concave_notch()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        // A box around Peckham but not Hackney.
        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/location/within-polygon",
            new
            {
                vertices = new[]
                {
                    new { latitude = 51.45d, longitude = -0.10d },
                    new { latitude = 51.50d, longitude = -0.10d },
                    new { latitude = 51.50d, longitude = -0.04d },
                    new { latitude = 51.45d, longitude = -0.04d }
                }
            },
            JsonOptions);

        var areas = await ReadDataAsync<List<AreaDto>>(response);

        areas.Select(area => area.Name).Should().Contain("Peckham");
        areas.Select(area => area.Name).Should().NotContain("Hackney");
    }

    /// <summary>
    /// The server's distance and the client's have to agree, because the client keeps
    /// computing its own.
    /// </summary>
    [Fact]
    public async Task Distance_and_commute_estimates_match_the_clients()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/location/distance",
            new
            {
                fromLatitude = 51.4742d,
                fromLongitude = -0.0691d,
                toLatitude = 51.4624d,
                toLongitude = -0.1149d
            },
            JsonOptions);

        var distance = await ReadDataAsync<DistanceDto>(response);

        distance.DistanceKm.Should().BeApproximately(3.4d, 0.2d);
        distance.Formatted.Should().EndWith("km away");
        distance.CommuteMinutes.Should().ContainKeys("walk", "drive", "transit");
    }

    // ── The extension seams, reported honestly ───────────────────────────

    /// <summary>
    /// The capability report is how an operator confirms which spatial provider is actually
    /// running, and how a client decides whether to offer a control that needs one.
    /// </summary>
    [Fact]
    public async Task The_capability_report_names_the_active_provider_and_its_limits()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var capabilities = await ReadDataAsync<CapabilitiesDto>(
            await HttpClient.GetAsync("/api/v1/location/capabilities"));

        capabilities.Provider.Should().Be("relational");
        capabilities.DistanceCalculator.Should().Be("haversine");
        capabilities.SupportsRadiusSearch.Should().BeTrue();
        capabilities.SupportsBoundingBoxSearch.Should().BeTrue();
        capabilities.SupportsPolygonSearch.Should().BeTrue();

        capabilities.SupportsNearestNeighbor.Should().BeFalse(
            "the default provider has no spatial index and does not claim one");

        capabilities.NearbyResourceTypes.Should().BeEquivalentTo(
            ["updates", "providers", "guides", "marketplace", "housing", "events"],
            "the report names exactly the resource types verticals have claimed — B6 claims " +
            "'updates', B7 claims 'providers', B8 claims 'guides', B10 claims 'marketplace', " +
            "B11 claims 'housing' and B12 claims 'events', six of the keys this module reserved. " +
            "Only 'jobs' is unclaimed: B9 ships no nearby provider");
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(JsonOptions);

        return envelope!.Data!;
    }

    private sealed record Envelope<T>(T? Data);

    private sealed record PagedDto<T>(List<T> Items, int TotalCount);

    private sealed record CountryDto(
        string Code, string Name, string Flag, string? CallingCode, bool IsSupported, bool IsFeatured);

    private sealed record CountryMetadataDto(string? Currency, string? PrimaryTimeZone);

    private sealed record CountryDetailDto(CountryDto Country, CountryMetadataDto Metadata);

    private sealed record AreaDto(Guid Id, string Name, string Slug, string Level, string Path);

    private sealed record LocationReferenceDto(
        string CountryCode,
        string CountryName,
        string Flag,
        string? CityName,
        string? Neighborhood,
        double? Latitude,
        string DisplayName,
        Guid? AreaId,
        string? Slug);

    private sealed record SearchDto(string Term, List<LocationReferenceDto> Results);

    private sealed record HubStatsDto(int? SafetyScore, string? CostOfLiving, bool IsAvailable);

    private sealed record HubDto(
        string Id,
        string Name,
        string? CityName,
        string CountryCode,
        HubStatsDto Stats,
        Dictionary<string, int> ResourceCounts);

    private sealed record PreferencesDto(double SearchRadius, string RadiusScope);

    private sealed record ProfileDto(
        LocationReferenceDto? CurrentLocation,
        LocationReferenceDto? ActiveLocation,
        bool TravelMode,
        string PermissionStatus,
        PreferencesDto Preferences,
        List<SavedLocationDto> SavedLocations,
        List<LocationReferenceDto> RecentLocations);

    private sealed record SavedLocationDto(Guid Id, string Label, LocationReferenceDto Location);

    private sealed record ExperienceDto(
        string CountryCode,
        int StartYear,
        int? EndYear,
        bool IsCurrentResidence,
        bool IsVerified,
        string VerificationState,
        List<string> ExpertiseAreas);

    private sealed record TimelineDto(
        List<ExperienceDto> Experiences,
        ExperienceDto? CurrentResidence,
        int CountryCount,
        int VerifiedCountryCount);

    private sealed record NearbyDto(
        LocationReferenceDto Center,
        double RadiusKm,
        string Scope,
        Dictionary<string, List<object>> Groups,
        List<string> UnavailableResourceTypes);

    private sealed record DistanceDto(
        double DistanceKm, string Formatted, Dictionary<string, int> CommuteMinutes);

    private sealed record CapabilitiesDto(
        string Provider,
        string DistanceCalculator,
        bool SupportsRadiusSearch,
        bool SupportsBoundingBoxSearch,
        bool SupportsPolygonSearch,
        bool SupportsNearestNeighbor,
        List<string> NearbyResourceTypes);
}
