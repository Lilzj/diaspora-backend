using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Platform.Usage;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Street-address search and the vendor allowance meter — 27 Sep 2026.
/// <para>
/// The test host has no Google key, so these pin the deployment without a vendor: the routes are
/// reachable, authorised and validated, and answer <c>NotConfigured</c>. The meter is exercised
/// directly because its guarantee (no machine takes a unit past the allowance) lives in one SQL
/// statement, and that statement is the same one production runs.
/// </para>
/// </summary>
public sealed class PlaceSearchTests : IdentityTestBase
{
    private const string Places = "/api/v1/location/places";
    private const string Token = "0f8fad5b-d9cb-469f-a165-70867728950e";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PlaceSearchTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Theory]
    [InlineData("/autocomplete")]
    [InlineData("/details")]
    public async Task An_anonymous_caller_is_refused(string route)
    {
        Api.ClearAuthentication();

        var response = await HttpClient.PostAsJsonAsync($"{Places}{route}", new { input = "Royal Road" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Without_a_vendor_autocomplete_says_so_and_suggests_nothing()
    {
        await AuthenticateMemberAsync();

        var response = await HttpClient.PostAsJsonAsync($"{Places}/autocomplete", new
        {
            input = "Royal Road",
            sessionToken = Token,
            near = new { latitude = -20.24, longitude = 57.47 },
            countryCodes = new[] { "MU" }
        });

        var body = await DataAsync(response);
        body.GetProperty("status").GetString().Should().Be("NotConfigured");
        body.GetProperty("provider").GetString().Should().Be("none");
        body.GetProperty("suggestions").GetArrayLength().Should().Be(0);
        body.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Without_a_vendor_details_say_so()
    {
        await AuthenticateMemberAsync();

        var response = await HttpClient.PostAsJsonAsync($"{Places}/details", new
        {
            placeId = "ChIJN1t_tDeuEmsRUsoyG83frY4",
            sessionToken = Token
        });

        var body = await DataAsync(response);
        body.GetProperty("status").GetString().Should().Be("NotConfigured");
        body.GetProperty("place").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("R", Token)]
    [InlineData("Royal Road", "short")]
    [InlineData("Royal Road", "has spaces in it and is long enough")]
    public async Task A_malformed_search_is_refused_before_any_vendor_is_asked(string input, string token)
    {
        await AuthenticateMemberAsync();

        var response = await HttpClient.PostAsJsonAsync($"{Places}/autocomplete", new
        {
            input,
            sessionToken = token
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_place_id_that_is_not_url_safe_never_leaves_the_platform()
    {
        await AuthenticateMemberAsync();

        var response = await HttpClient.PostAsJsonAsync($"{Places}/details", new
        {
            placeId = "../../v1/places:searchText",
            sessionToken = Token
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_capability_report_says_street_addresses_are_not_available()
    {
        await AuthenticateMemberAsync();

        var body = await DataAsync(await HttpClient.GetAsync("/api/v1/geo/capabilities"));

        var entry = body.GetProperty("capabilities").EnumerateArray()
            .Single(capability => capability.GetProperty("capability").GetString() == "street-level-geocoding");

        entry.GetProperty("available").GetBoolean().Should().BeFalse();
        entry.GetProperty("provider").GetString().Should().Be("catalog");
    }

    // ── The meter ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_meter_hands_out_exactly_the_allowance_and_then_refuses()
    {
        var provider = $"test-{Guid.NewGuid():N}"[..20];

        using var scope = Factory.Services.CreateScope();
        var meter = scope.ServiceProvider.GetRequiredService<IProviderUsageMeter>();

        var answers = new List<bool>();

        for (var attempt = 0; attempt < 4; attempt++)
        {
            answers.Add(await meter.TryConsumeAsync(provider, "sku", 3, "America/Los_Angeles"));
        }

        answers.Should().Equal(true, true, true, false);
        (await meter.GetUsedAsync(provider, "sku", "America/Los_Angeles")).Should().Be(3);
    }

    [Fact]
    public async Task Each_sku_has_its_own_allowance()
    {
        var provider = $"test-{Guid.NewGuid():N}"[..20];

        using var scope = Factory.Services.CreateScope();
        var meter = scope.ServiceProvider.GetRequiredService<IProviderUsageMeter>();

        (await meter.TryConsumeAsync(provider, "routes", 1, "America/Los_Angeles")).Should().BeTrue();
        (await meter.TryConsumeAsync(provider, "routes", 1, "America/Los_Angeles")).Should().BeFalse();
        (await meter.TryConsumeAsync(provider, "places", 1, "America/Los_Angeles")).Should().BeTrue();
    }

    [Fact]
    public async Task An_allowance_of_zero_switches_a_sku_off_without_writing_a_row()
    {
        var provider = $"test-{Guid.NewGuid():N}"[..20];

        using var scope = Factory.Services.CreateScope();
        var meter = scope.ServiceProvider.GetRequiredService<IProviderUsageMeter>();

        (await meter.TryConsumeAsync(provider, "sku", 0, "America/Los_Angeles")).Should().BeFalse();
        (await meter.GetUsedAsync(provider, "sku", "America/Los_Angeles")).Should().Be(0);
    }

    [Fact]
    public void The_billing_month_turns_over_in_the_vendors_time_zone_not_in_UTC()
    {
        // 03:00 UTC on 1 October is still 30 September in California.
        var moment = new DateTimeOffset(2026, 10, 1, 3, 0, 0, TimeSpan.Zero);

        ProviderUsageCounter.PeriodOf(moment, "America/Los_Angeles").Should().Be("2026-09");
        ProviderUsageCounter.PeriodOf(moment, "UTC").Should().Be("2026-10");
    }

    private async Task AuthenticateMemberAsync()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            $"the request should have succeeded but returned {(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("data").Clone();
    }
}
