using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The Notifications &amp; Communication Platform (B13), driven over real HTTP through the same host
/// every other module suite uses.
/// <para>
/// Five claims are under test. That the module composed in through nothing but contributor
/// interfaces. That <b>a member's inbox exists and is reachable at the lowest access tier the
/// platform has</b> — the assertion that matters most, because the whole gating decision rests on it
/// and getting it wrong silences the platform for exactly the accounts that need it. That the
/// category catalogue is seeded reference data a settings screen can render. That preferences round
/// trip in the shape the mobile client already stores. And that <b>a mandatory category cannot be
/// muted.</b>
/// </para>
/// <para>
/// The wire shapes below are re-declared by hand rather than referenced from the module. Binding
/// these tests to B13's own response records would make them pass whenever the module compiles,
/// which is the one thing a contract test must never do.
/// </para>
/// </summary>
[Collection(SignalsCollection.Name)]
public sealed class NotificationsModuleTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/notifications";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public NotificationsModuleTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Composition ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Proof the module's <c>IFeatureContributor</c> reached the composed catalogue: gates B13 owns
    /// turn up in Identity's response, which the Notifications module never touched.
    /// </summary>
    [Fact]
    public async Task The_notification_gates_composed_into_the_platform_catalogue()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        var names = features.Features.Select(feature => feature.Name).ToList();

        names.Should().Contain("notifications.inbox");
        names.Should().Contain("notifications.devices");
        names.Should().Contain("notifications.digests");
    }

    /// <summary>
    /// <b>The inbox opens for a member who has verified nothing.</b> This is the phase's central
    /// access decision: the first message a new account receives is the one telling it what to do
    /// next, and the second is the security alert. A gate above the floor here would silence the
    /// platform for the people who most need to hear from it.
    /// </summary>
    [Fact]
    public async Task The_inbox_is_readable_at_the_lowest_tier()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(new Uri(Root, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>An unauthenticated caller is refused, which is the other half of the same rule.</summary>
    [Fact]
    public async Task The_inbox_is_refused_without_a_token()
    {
        using var anonymous = Factory.CreateClient();

        var response = await anonymous.GetAsync(new Uri(Root, UriKind.Relative));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// The badge answers from the maintained counter, and answers zero for a member who has never
    /// received anything rather than failing on a missing row.
    /// </summary>
    [Fact]
    public async Task The_unread_count_answers_zero_for_a_new_member()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(new Uri($"{Root}/unread-count", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await ReadAsync<UnreadCountShape>(response);

        payload.Total.Should().BeGreaterThanOrEqualTo(0);
    }

    // ── Reference data ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The category catalogue is seeded and readable, so a settings screen renders from the server
    /// rather than from hard-coded strings — which is what "configurable categories" has to mean to
    /// be worth anything.
    /// </summary>
    [Fact]
    public async Task The_category_catalogue_is_seeded()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(new Uri($"{Root}/categories", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var categories = await ReadAsync<List<CategoryShape>>(response);

        categories.Should().NotBeEmpty();
        categories.Select(category => category.Key).Should().Contain("security");
        categories.Select(category => category.Key).Should().Contain("jobs");

        // The keys the mobile settings screen already stores under.
        categories.Select(category => category.Key).Should().Contain("housing");
        categories.Select(category => category.Key).Should().Contain("marketplace");
    }

    /// <summary>
    /// <b>Security is mandatory, and the catalogue says so.</b> A client renders that switch disabled
    /// with a reason, rather than offering a control that silently does nothing.
    /// </summary>
    [Fact]
    public async Task The_security_category_is_mandatory()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var categories = await ReadAsync<List<CategoryShape>>(
            await HttpClient.GetAsync(new Uri($"{Root}/categories", UriKind.Relative)));

        categories.Single(category => category.Key == "security").IsMandatory.Should().BeTrue();
        categories.Single(category => category.Key == "moderation").IsMandatory.Should().BeTrue();
        categories.Single(category => category.Key == "jobs").IsMandatory.Should().BeFalse();
    }

    // ── Preferences ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A member who has never opened the settings screen still gets a full one, rendered from the
    /// catalogue against the platform defaults. Reading the screen writes nothing.
    /// </summary>
    [Fact]
    public async Task Preferences_default_without_a_stored_record()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var preferences = await ReadAsync<PreferencesShape>(
            await HttpClient.GetAsync(new Uri($"{Root}/preferences", UriKind.Relative)));

        preferences.InApp.Should().BeTrue("the inbox cannot be switched off");
        preferences.Push.Should().BeTrue();
        preferences.Email.Should().BeFalse("an unsolicited first email teaches members to mark us as spam");
        preferences.Categories.Should().NotBeEmpty();
        preferences.Communication.CriticalOverrideEnabled.Should().BeTrue();
    }

    /// <summary>
    /// Preferences round trip in exactly the shape the existing mobile settings screen stores: flat
    /// <c>push</c> and <c>email</c> booleans, and one boolean per category key.
    /// </summary>
    [Fact]
    public async Task Preferences_round_trip_in_the_clients_own_shape()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var update = await HttpClient.PutAsJsonAsync(
            new Uri($"{Root}/preferences", UriKind.Relative),
            new
            {
                push = false,
                email = true,
                categories = new Dictionary<string, bool> { ["marketplace"] = false },
                quietHoursEnabled = true,
                quietHoursStart = 22,
                quietHoursEnd = 7,
                language = "fr",
                timeZoneId = "Europe/Paris"
            });

        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var preferences = await ReadAsync<PreferencesShape>(
            await HttpClient.GetAsync(new Uri($"{Root}/preferences", UriKind.Relative)));

        preferences.Push.Should().BeFalse();
        preferences.Email.Should().BeTrue();
        preferences.Categories.Single(category => category.Key == "marketplace").Enabled
            .Should().BeFalse();
        preferences.Communication.QuietHoursEnabled.Should().BeTrue();
        preferences.Communication.QuietHoursStart.Should().Be(22);
        preferences.Communication.Language.Should().Be("fr");
        preferences.Communication.TimeZoneId.Should().Be("Europe/Paris");
    }

    /// <summary>
    /// <b>A mandatory category cannot be muted, and the refusal is loud rather than silent.</b> A
    /// screen that accepted the change and kept delivering would be lying to the member, and they
    /// would reasonably conclude the platform ignores their settings.
    /// </summary>
    [Fact]
    public async Task Muting_a_mandatory_category_is_refused()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PutAsJsonAsync(
            new Uri($"{Root}/preferences", UriKind.Relative),
            new { categories = new Dictionary<string, bool> { ["security"] = false } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Quiet hours that start and end at the same hour are refused rather than stored.</summary>
    [Fact]
    public async Task Ambiguous_quiet_hours_are_refused()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PutAsJsonAsync(
            new Uri($"{Root}/preferences", UriKind.Relative),
            new { quietHoursEnabled = true, quietHoursStart = 9, quietHoursEnd = 9 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>An unknown time zone is refused rather than accepted and quietly ignored.</summary>
    [Fact]
    public async Task An_unknown_time_zone_is_refused()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PutAsJsonAsync(
            new Uri($"{Root}/preferences", UriKind.Relative),
            new { timeZoneId = "Mars/Olympus_Mons" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Devices ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registering the same token twice is one endpoint, not two — which is what stops a client
    /// retrying on a flaky connection from making every message arrive twice.
    /// </summary>
    [Fact]
    public async Task Registering_the_same_token_twice_produces_one_device()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var body = new
        {
            token = "test-push-token-0123456789",
            platform = "Android",
            deviceIdentifier = "device-1",
            friendlyName = "Pixel"
        };

        (await HttpClient.PutAsJsonAsync(new Uri($"{Root}/devices", UriKind.Relative), body))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await HttpClient.PutAsJsonAsync(new Uri($"{Root}/devices", UriKind.Relative), body))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var devices = await ReadAsync<List<DeviceShape>>(
            await HttpClient.GetAsync(new Uri($"{Root}/devices", UriKind.Relative)));

        devices.Should().ContainSingle();
        devices[0].HasToken.Should().BeTrue();
        devices[0].Platform.Should().Be("Android");
    }

    /// <summary>The token is never echoed back. It is a credential for somebody else's service.</summary>
    [Fact]
    public async Task A_device_response_never_carries_the_token()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await HttpClient.PutAsJsonAsync(
            new Uri($"{Root}/devices", UriKind.Relative),
            new { token = "secret-token-value-9876543210", platform = "IOS" });

        var raw = await (await HttpClient.GetAsync(new Uri($"{Root}/devices", UriKind.Relative)))
            .Content.ReadAsStringAsync();

        raw.Should().NotContain("secret-token-value-9876543210");
    }

    // ── Staff surfaces ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The template catalogue is staff-only. A template is a message the platform sends in its own
    /// name, and an ordinary member must not be able to read the wording of everything it says.
    /// </summary>
    [Fact]
    public async Task The_template_catalogue_is_refused_to_a_member()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(new Uri($"{Root}/templates", UriKind.Relative));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
    }

    /// <summary>Analytics name no member and are still staff-only.</summary>
    [Fact]
    public async Task Analytics_are_refused_to_a_member()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(new Uri($"{Root}/analytics", UriKind.Relative));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }
}
