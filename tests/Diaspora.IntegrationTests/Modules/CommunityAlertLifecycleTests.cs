using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Alerts, over HTTP: withdrawn, cancelled, amended, sized, and who may publish them — 16 Sep 2026.
/// <para>
/// Before this an alert could be published and could expire, and nothing in between. The map drew
/// every report as a pin whatever it covered, an author who got it wrong could only make it vanish,
/// and a client deciding whether to offer an Emergency option had nothing to ask.
/// </para>
/// </summary>
public sealed class CommunityAlertLifecycleTests : IdentityTestBase
{
    private const double PeckhamLatitude = 51.4742d;
    private const double PeckhamLongitude = -0.0691d;

    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web);

    public CommunityAlertLifecycleTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_withdrawn_alert_stays_readable_marked_and_leaves_the_map()
    {
        var run = Run();
        await SignInVerifiedAsync();

        var alert = await PublishAlertAsync(run, PeckhamLatitude, PeckhamLongitude, radius: 600);

        alert.AffectedRadiusMetres.Should().Be(600);
        alert.CanWithdraw.Should().BeTrue();
        alert.CanCancel.Should().BeTrue();
        (await NearbyTitlesAsync()).Should().Contain(Title(run), "a published alert is on the map");

        var withdrawn = await ReadPostAsync(
            await HttpClient.PostAsync($"/api/v1/community/posts/{alert.PostId}/withdraw", null));

        withdrawn.Status.Should().Be("withdrawn");
        withdrawn.ClosedAt.Should().NotBeNull();
        withdrawn.CanWithdraw.Should().BeFalse();
        withdrawn.CanCancel.Should().BeFalse();

        // Somebody who acted on it can still open it and read that it was withdrawn.
        await SignInVerifiedAsync();

        var read = await ReadPostAsync(await HttpClient.GetAsync($"/api/v1/community/posts/{alert.PostId}"));

        read.Status.Should().Be("withdrawn");
        read.CanWithdraw.Should().BeFalse("it is not theirs");
        (await NearbyTitlesAsync()).Should().NotContain(Title(run));
    }

    [Fact]
    public async Task A_cancelled_alert_says_cancelled_and_nobody_else_can_close_it()
    {
        var run = Run();
        await SignInVerifiedAsync();

        var alert = await PublishAlertAsync(run, PeckhamLatitude, PeckhamLongitude);

        await SignInVerifiedAsync();

        var refused = await HttpClient.PostAsync($"/api/v1/community/posts/{alert.PostId}/cancel", null);

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("community.post.not-owned");
    }

    [Fact]
    public async Task An_author_can_raise_an_alert_and_widen_it_then_remove_the_area()
    {
        var run = Run();
        await SignInVerifiedAsync();

        var alert = await PublishAlertAsync(run, PeckhamLatitude, PeckhamLongitude, radius: 500);

        var amended = await ReadPostAsync(await HttpClient.PutAsJsonAsync(
            $"/api/v1/community/posts/{alert.PostId}",
            new { body = Body(run), title = Title(run), urgency = "emergency", affectedRadiusMetres = 3_000 },
            WireJson));

        amended.Urgency.Should().Be("emergency");
        amended.AffectedRadiusMetres.Should().Be(3_000);

        var cleared = await ReadPostAsync(await HttpClient.PutAsJsonAsync(
            $"/api/v1/community/posts/{alert.PostId}",
            new { body = Body(run), title = Title(run), affectedRadiusMetres = 0 },
            WireJson));

        cleared.AffectedRadiusMetres.Should().BeNull();
        cleared.Urgency.Should().Be("emergency", "leaving urgency out keeps it");
    }

    /// <summary>
    /// The point of an affected area. This alert is pinned eight kilometres north of Peckham and
    /// reaches ten; a member searching five kilometres around Peckham is inside it.
    /// </summary>
    [Fact]
    public async Task An_alert_whose_area_reaches_the_member_is_near_them_even_when_its_pin_is_not()
    {
        var run = Run();
        await SignInVerifiedAsync();

        const double eightKilometresNorth = PeckhamLatitude + 0.0720d;

        await PublishAlertAsync($"{run}-wide", eightKilometresNorth, PeckhamLongitude, radius: 10_000);
        await PublishAlertAsync($"{run}-point", eightKilometresNorth, PeckhamLongitude);

        var titles = await NearbyTitlesAsync();

        titles.Should().Contain(Title($"{run}-wide"));
        titles.Should().NotContain(Title($"{run}-point"), "a pin eight kilometres away is outside five");
    }

    [Fact]
    public async Task An_area_with_no_pin_is_refused_with_a_reason_the_composer_can_show()
    {
        await SignInVerifiedAsync();

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/community/posts",
            new
            {
                body = "Flooding somewhere in town",
                category = "traffic",
                urgency = "urgent",
                countryCode = "GB",
                cityName = "London",
                affectedRadiusMetres = 1_000
            },
            WireJson);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("community.post.affected-area.needs-point");
    }

    [Fact]
    public async Task A_verified_member_is_told_they_may_publish_any_urgency_including_emergency()
    {
        await SignInVerifiedAsync();

        var capabilities = await IdentityApiClient.ReadDataAsync<CapabilitiesShape>(
            await HttpClient.GetAsync("/api/v1/community/alerts/capabilities"));

        capabilities.CanPublish.Should().BeTrue();
        capabilities.CanPublishEmergency.Should().BeTrue();
        capabilities.CannotPublishEmergencyReason.Should().BeNull();
        capabilities.UrgencyLevels.Should().Equal("low", "normal", "urgent", "emergency");
        capabilities.MinAffectedRadiusMetres.Should().Be(25);
        capabilities.MaxAffectedRadiusMetres.Should().Be(50_000);
    }

    [Fact]
    public async Task A_member_who_is_not_verified_is_told_why_they_cannot_publish()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var capabilities = await IdentityApiClient.ReadDataAsync<CapabilitiesShape>(
            await HttpClient.GetAsync("/api/v1/community/alerts/capabilities"));

        capabilities.CanPublish.Should().BeFalse();
        capabilities.CannotPublishReason.Should().NotBeNullOrWhiteSpace();
        capabilities.CanPublishEmergency.Should().BeFalse();
        capabilities.UrgencyLevels.Should().BeEmpty();
    }

    [Fact]
    public async Task Somebody_not_signed_in_is_asked_to_sign_in_rather_than_refused()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        (await HttpClient.GetAsync("/api/v1/community/alerts/capabilities"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    private static string Run() => Guid.NewGuid().ToString("N")[..8];

    private static string Title(string run) => $"Burst water main {run}";

    private static string Body(string run) => $"The high street is flooded near the station. {run}";

    private async Task SignInVerifiedAsync()
    {
        Factory.EnsureDatabaseCreated();

        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
    }

    private async Task<PostShape> PublishAlertAsync(
        string run, double latitude, double longitude, int? radius = null)
    {
        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/community/posts",
            new
            {
                body = Body(run),
                title = Title(run),
                category = "traffic",
                urgency = "urgent",
                countryCode = "GB",
                cityName = "London",
                latitude,
                longitude,
                affectedRadiusMetres = radius,
                publish = true
            },
            WireJson);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        return await IdentityApiClient.ReadDataAsync<PostShape>(response);
    }

    private static async Task<PostShape> ReadPostAsync(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());

        return await IdentityApiClient.ReadDataAsync<PostShape>(response);
    }

    private async Task<List<string>> NearbyTitlesAsync()
    {
        var nearby = await IdentityApiClient.ReadDataAsync<NearbyShape>(
            await HttpClient.GetAsync(
                $"/api/v1/location/nearby?latitude={PeckhamLatitude}&longitude={PeckhamLongitude}&radiusKm=5"));

        return nearby.Groups.TryGetValue("updates", out var updates)
            ? updates.Select(item => item.Title).ToList()
            : [];
    }

    private sealed record PostShape
    {
        public Guid PostId { get; init; }

        public string Status { get; init; } = string.Empty;

        public string Urgency { get; init; } = string.Empty;

        public int? AffectedRadiusMetres { get; init; }

        public DateTimeOffset? ClosedAt { get; init; }

        public bool CanWithdraw { get; init; }

        public bool CanCancel { get; init; }
    }

    private sealed record CapabilitiesShape
    {
        public bool CanPublish { get; init; }

        public string? CannotPublishReason { get; init; }

        public bool CanPublishEmergency { get; init; }

        public string? CannotPublishEmergencyReason { get; init; }

        public List<string> UrgencyLevels { get; init; } = [];

        public int MinAffectedRadiusMetres { get; init; }

        public int MaxAffectedRadiusMetres { get; init; }
    }

    private sealed record NearbyShape
    {
        public Dictionary<string, List<NearbyItemShape>> Groups { get; init; } = [];
    }

    private sealed record NearbyItemShape
    {
        public string Title { get; init; } = string.Empty;
    }
}
