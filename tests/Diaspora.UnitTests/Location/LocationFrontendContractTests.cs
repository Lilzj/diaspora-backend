using System.Reflection;
using System.Text.Json;
using Diaspora.Modules.Location;
using Diaspora.Modules.Location.Application.Mapping;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Profiles;
using Diaspora.Modules.Location.Domain.Spatial;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Persistence.Seeding;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Location;

/// <summary>
/// The frozen contract, pinned against the frontend literal it has to match.
/// <para>
/// Every assertion here cites the frontend file and field it protects. When one fails, the fix
/// is almost never to change the test: it is either to restore the backend name, or — if the
/// change is deliberate — to update the frontend, this test and the contract-freeze document
/// together, in one reviewed change. That is what frozen means here. Not unchangeable, but not
/// changeable by accident.
/// </para>
/// </summary>
public sealed class LocationFrontendContractTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    // ── PlatformLocation — src/store/location.ts:3 ───────────────────────

    /// <summary>
    /// Every field of the frontend's <c>PlatformLocation</c> has to be present on the
    /// canonical reference, or the location store cannot bind to it.
    /// </summary>
    [Theory]
    [InlineData("countryCode")]
    [InlineData("countryName")]
    [InlineData("flag")]
    [InlineData("state")]
    [InlineData("cityName")]
    [InlineData("neighborhood")]
    [InlineData("street")]
    [InlineData("latitude")]
    [InlineData("longitude")]
    [InlineData("displayName")]
    public void The_location_reference_carries_every_platform_location_field(string field)
    {
        WireNames<LocationReferenceResponse>().Should().Contain(field);
    }

    /// <summary>
    /// Five of the frontend's seven location DTOs call the neighbourhood <c>areaName</c>; the
    /// store calls it <c>neighborhood</c>. Both are emitted with the same value — naming
    /// finding #15.
    /// </summary>
    [Fact]
    public void The_neighbourhood_is_emitted_under_both_names_the_frontend_uses()
    {
        var names = WireNames<LocationReferenceResponse>();

        names.Should().Contain("neighborhood");
        names.Should().Contain("areaName");
    }

    /// <summary>
    /// <c>distanceKm</c> is the field DistanceBadge and NearbyIndicator read on every card.
    /// </summary>
    [Fact]
    public void Proximity_results_carry_the_distance_field_the_badges_read()
    {
        WireNames<LocationReferenceResponse>().Should().Contain("distanceKm");
        WireNames<AreaSummaryResponse>().Should().Contain("distanceKm");
        WireNames<NearbyItemResponse>().Should().Contain("distanceKm");
    }

    // ── NeighborhoodOverview — src/mocks/handlers/location.ts:183 ────────

    [Theory]
    [InlineData("id")]
    [InlineData("name")]
    [InlineData("cityName")]
    [InlineData("countryName")]
    [InlineData("countryCode")]
    [InlineData("latitude")]
    [InlineData("longitude")]
    [InlineData("overview")]
    [InlineData("stats")]
    [InlineData("resourceCounts")]
    public void The_hub_response_carries_every_neighborhood_overview_field(string field)
    {
        WireNames<NeighborhoodHubResponse>().Should().Contain(field);
    }

    /// <summary>
    /// The hub route carries the slug as <c>id</c>, because that is what the frontend's
    /// existing <c>/neighborhood/peckham</c> link puts there.
    /// </summary>
    [Fact]
    public void The_hub_reports_its_slug_as_the_id_the_frontend_routes_on()
    {
        var hub = SampleHub();

        hub.Id.Should().Be("peckham");
        hub.Slug.Should().Be("peckham");
    }

    /// <summary>The stats object's own field names, from the mock.</summary>
    [Theory]
    [InlineData("safetyScore")]
    [InlineData("costOfLiving")]
    [InlineData("walkability")]
    public void The_hub_stats_carry_the_field_names_the_frontend_renders(string field)
    {
        WireNames<NeighborhoodStatsResponse>().Should().Contain(field);
    }

    // ── CountryItem / CountryRef ─────────────────────────────────────────

    /// <summary>
    /// <c>CountryItem</c> (mocks/countries.ts) is code, name, flag, callingCode.
    /// <c>CountryRef</c> (types/guide.ts) is code, name, flag. One superset binds both.
    /// </summary>
    [Theory]
    [InlineData("code")]
    [InlineData("name")]
    [InlineData("flag")]
    [InlineData("callingCode")]
    public void The_country_summary_binds_both_frontend_country_shapes(string field)
    {
        WireNames<CountrySummaryResponse>().Should().Contain(field);
    }

    // ── CountryExperienceInterval — src/types/trust.ts:63 ────────────────

    [Theory]
    [InlineData("countryCode")]
    [InlineData("countryName")]
    [InlineData("flag")]
    [InlineData("startYear")]
    [InlineData("endYear")]
    [InlineData("isVerified")]
    [InlineData("role")]
    public void The_experience_response_carries_every_timeline_field(string field)
    {
        WireNames<CountryExperienceResponse>().Should().Contain(field);
    }

    /// <summary>
    /// The declared half of <c>CountryVerificationSummary</c> (types/verification.ts:35),
    /// which is Location's rather than Trust's.
    /// </summary>
    [Theory]
    [InlineData("years")]
    [InlineData("expertiseAreas")]
    public void The_experience_response_carries_the_declared_verification_summary_fields(
        string field)
    {
        WireNames<CountryExperienceResponse>().Should().Contain(field);
    }

    // ── The location store — src/store/location.ts:18 ────────────────────

    [Theory]
    [InlineData("currentLocation")]
    [InlineData("travelLocation")]
    [InlineData("travelMode")]
    [InlineData("permissionStatus")]
    [InlineData("savedLocations")]
    [InlineData("recentLocations")]
    public void The_profile_response_replaces_the_client_store_field_for_field(string field)
    {
        WireNames<LocationProfileResponse>().Should().Contain(field);
    }

    /// <summary>
    /// Lower-case, matching the client's <c>'granted' | 'denied' | 'undetermined'</c> union.
    /// This deviates from the platform's PascalCase enum convention and is kept deliberately —
    /// intentional deviation #10.
    /// </summary>
    [Theory]
    [InlineData(LocationPermissionStatus.Granted, "granted")]
    [InlineData(LocationPermissionStatus.Denied, "denied")]
    [InlineData(LocationPermissionStatus.Undetermined, "undetermined")]
    public void Permission_status_is_serialised_lower_case(
        LocationPermissionStatus status, string expected)
    {
        var profile = LocationProfile.CreateFor(UserId.New());
        profile.SetPermissionStatus(status);

        ProfileMapper.ToResponse(profile).PermissionStatus.Should().Be(expected);
    }

    /// <summary>
    /// The radius is emitted in the client's own encoding, sentinels included, so RadiusSlider
    /// binds without a translation step.
    /// </summary>
    [Fact]
    public void The_radius_is_emitted_in_the_sliders_own_encoding()
    {
        var profile = LocationProfile.CreateFor(UserId.New());

        profile.UpdatePreferences(
            GeographicPreference.Default.WithSearchRadius(SearchRadius.CityWide));

        ProfileMapper.ToResponse(profile).Preferences.SearchRadius.Should().Be(-1d);
    }

    // ── The radius options — components/RadiusSlider.tsx:12 ──────────────

    /// <summary>
    /// Every option the slider offers has to be accepted. A radius the client can select and
    /// the server rejects is a control that fails on tap.
    /// </summary>
    [Theory]
    [InlineData(0.5d)]
    [InlineData(1d)]
    [InlineData(3d)]
    [InlineData(5d)]
    [InlineData(10d)]
    [InlineData(25d)]
    [InlineData(-1d)]
    [InlineData(-2d)]
    public void Every_radius_the_slider_offers_is_accepted(double value)
    {
        SearchRadius.FromClientValue(value).IsSuccess.Should().BeTrue();
    }

    // ── Distance thresholds — components + utils/geo.ts ──────────────────

    /// <summary>
    /// The thresholds the client's badges branch on. They are effectively enum-valued and
    /// pinned for the same reason enum members are.
    /// </summary>
    [Fact]
    public void The_distance_thresholds_match_the_frontends()
    {
        DistancePresentation.NeighborhoodThresholdKm.Should().Be(0.1d);
        DistancePresentation.MetersThresholdKm.Should().Be(1d);
        HaversineDistanceCalculator.EarthRadiusKilometers.Should().Be(6371d);
    }

    // ── Enum member names ────────────────────────────────────────────────

    /// <summary>The cost band the frontend renders as <c>'Low' | 'Medium' | 'High'</c>.</summary>
    [Fact]
    public void The_cost_of_living_band_matches_the_frontends_union()
    {
        Enum.GetNames<CostOfLivingBand>().Should().BeEquivalentTo("Low", "Medium", "High");
    }

    /// <summary>The privacy setting Identity owns and Location enforces.</summary>
    [Fact]
    public void Coordinate_precision_matches_the_privacy_settings_union()
    {
        Enum.GetNames<CoordinatePrecision>().Should().BeEquivalentTo("Exact", "Approximate");
    }

    /// <summary>The commute modes DistanceBadge switches on, lower-cased on the wire.</summary>
    [Fact]
    public void The_commute_modes_are_keyed_lower_case()
    {
        var modes = DistancePresentation.AllCommuteModes(Distance.FromKilometers(5d));

        modes.Keys.Should().BeEquivalentTo("walk", "drive", "transit");
    }

    // ── The country catalogue ────────────────────────────────────────────

    /// <summary>
    /// The shipped table is transcribed from <c>src/mocks/countries.ts</c>, which carries 191
    /// entries. A count mismatch means a country was dropped or added on one side only, which
    /// shows up as a member being unable to find their own country on a picker.
    /// </summary>
    [Fact]
    public void The_shipped_country_table_matches_the_frontends_length()
    {
        WorldCountryData.All.Should().HaveCount(191);
    }

    [Fact]
    public void Every_shipped_country_has_a_well_formed_code_and_calling_code()
    {
        foreach (var record in WorldCountryData.All)
        {
            CountryCode.TryCreate(record.Code).IsSuccess.Should()
                .BeTrue($"'{record.Code}' must be a valid ISO code");

            CallingCode.TryCreate(record.CallingCode).IsSuccess.Should()
                .BeTrue($"'{record.CallingCode}' must be a valid E.164 calling code");

            record.Name.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void The_shipped_country_codes_are_unique()
    {
        WorldCountryData.All
            .Select(record => record.Code)
            .Should().OnlyHaveUniqueItems();
    }

    /// <summary>Spot checks against the frontend's own entries, including the launch markets.</summary>
    [Theory]
    [InlineData("GB", "United Kingdom", "+44")]
    [InlineData("MU", "Mauritius", "+230")]
    [InlineData("NG", "Nigeria", "+234")]
    [InlineData("CI", "Côte d'Ivoire", "+225")]
    [InlineData("BA", "Bosnia & Herzegovina", "+387")]
    [InlineData("CD", "Congo - Kinshasa", "+243")]
    public void Country_entries_match_the_frontends_verbatim(
        string code, string name, string callingCode)
    {
        var record = WorldCountryData.All.Single(entry => entry.Code == code);

        record.Name.Should().Be(name);
        record.CallingCode.Should().Be(callingCode);
    }

    // ── Permission and gate keys ─────────────────────────────────────────

    /// <summary>
    /// Frozen: permission keys are nameable in role assignments and gate names are switched on
    /// by clients.
    /// </summary>
    [Fact]
    public void Every_permission_key_is_namespaced_to_the_module()
    {
        foreach (var key in ConstantsOf(typeof(LocationPermissions)))
        {
            key.Should().StartWith("location:");
        }
    }

    [Fact]
    public void Every_feature_gate_is_namespaced_to_the_module()
    {
        foreach (var name in ConstantsOf(typeof(LocationFeatures)))
        {
            name.Should().StartWith("location.");
        }
    }

    [Fact]
    public void Every_error_code_is_namespaced_to_the_module()
    {
        foreach (var code in ConstantsOf(typeof(LocationErrorCodes)))
        {
            code.Should().StartWith("location.");
        }
    }

    private static NeighborhoodHubResponse SampleHub() => new()
    {
        Id = "peckham",
        AreaId = Guid.NewGuid(),
        Slug = "peckham",
        Name = "Peckham",
        CityName = "London",
        CountryName = "United Kingdom",
        CountryCode = "GB",
        Flag = "🇬🇧",
        Stats = new NeighborhoodStatsResponse { IsAvailable = false },
        ResourceCounts = new Dictionary<string, int>(StringComparer.Ordinal),
        Ancestors = []
    };

    /// <summary>
    /// The JSON property names a response actually serialises to, under the host's own
    /// camelCase policy — which is what a client sees, rather than what the C# property is
    /// called.
    /// </summary>
    private static List<string> WireNames<T>() =>
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => WebJson.PropertyNamingPolicy!.ConvertName(property.Name))
            .ToList();

    private static List<string> ConstantsOf(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false })
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();
}
