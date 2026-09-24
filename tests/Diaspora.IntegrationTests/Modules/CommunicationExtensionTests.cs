using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Notifications.Application.Preferences;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The three HTTP surfaces B13.3 added, driven end to end.
/// <para>
/// <b>All three close a gap the frontend registers recorded as "the client is guessing".</b> The
/// device list decided which row was the current one from a string the client generated; there was
/// no way to sign every other device out; and the email subscription state a footer's unsubscribe
/// produces did not exist at all. Each is now something the server asserts, and each is asserted here
/// against a real host rather than against a handler.
/// </para>
/// </summary>
[Collection(SignalsCollection.Name)]
public sealed class CommunicationExtensionTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/notifications";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CommunicationExtensionTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Devices ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The flag is derived from the presented token, which is what makes it an assertion.</b>
    /// The client used to match on an identifier it generated itself — a guess the server could not
    /// check, and one a reinstall silently invalidated. Registering on a session that names a device
    /// records that device, and the list reports it.
    /// </summary>
    [Fact]
    public async Task The_device_registered_by_this_session_is_reported_as_the_current_one()
    {
        var (_, auth) = await SignInWithDeviceAsync("device-alpha");
        Api.Authenticate(auth.Tokens.AccessToken);

        await RegisterDeviceAsync("push-token-alpha-0123456789");

        var devices = await ReadAsync<List<DeviceShape>>(
            await HttpClient.GetAsync(new Uri($"{Root}/devices", UriKind.Relative)));

        devices.Should().ContainSingle().Which.IsCurrentDevice.Should().BeTrue();
    }

    /// <summary>
    /// <b>Two sessions, two devices, and each sees only its own as current.</b> The failure this
    /// guards against is the one that matters: "sign my other devices out" spares whatever the list
    /// says is current, and a flag that answered true for everything would spare everything while a
    /// flag that answered true for the wrong row would silence the phone in the member's hand.
    /// </summary>
    [Fact]
    public async Task A_device_registered_by_another_session_is_not_the_current_one()
    {
        var (phone, first) = await SignInWithDeviceAsync("device-alpha");

        Api.Authenticate(first.Tokens.AccessToken);
        await RegisterDeviceAsync("push-token-alpha-0123456789");

        var second = await SignInAgainWithDeviceAsync(phone, "device-beta");

        Api.Authenticate(second.Tokens.AccessToken);
        await RegisterDeviceAsync("push-token-beta-0123456789");

        var devices = await ReadAsync<List<DeviceShape>>(
            await HttpClient.GetAsync(new Uri($"{Root}/devices", UriKind.Relative)));

        devices.Should().HaveCount(2);
        devices.Count(device => device.IsCurrentDevice).Should().Be(
            1, "exactly one endpoint belongs to the session making the request");
    }

    /// <summary>
    /// <b>Everything except this one, and this one is the server's answer rather than the client's.</b>
    /// </summary>
    [Fact]
    public async Task Deactivating_other_devices_spares_the_one_making_the_request()
    {
        var (phone, first) = await SignInWithDeviceAsync("device-alpha");

        Api.Authenticate(first.Tokens.AccessToken);
        await RegisterDeviceAsync("push-token-alpha-0123456789");

        var second = await SignInAgainWithDeviceAsync(phone, "device-beta");

        Api.Authenticate(second.Tokens.AccessToken);
        await RegisterDeviceAsync("push-token-beta-0123456789");

        var response = await HttpClient.PostAsync(
            new Uri($"{Root}/devices/deactivate-others", UriKind.Relative), null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<int>(response)).Should().Be(1);

        var devices = await ReadAsync<List<DeviceShape>>(
            await HttpClient.GetAsync(new Uri($"{Root}/devices", UriKind.Relative)));

        devices.Should().HaveCount(2, "a retired endpoint is kept so a member can be told why");

        devices.Single(device => device.IsCurrentDevice).IsActive.Should().BeTrue();
        devices.Single(device => !device.IsCurrentDevice).IsActive.Should().BeFalse();
    }

    /// <summary>
    /// <b>Refused rather than interpreted when the token names no device.</b> There is no safe
    /// reading of "all except this one" when the server cannot tell which one "this" is: retiring
    /// everything would silence the member's own phone with no way back except a re-registration they
    /// have no reason to know they need.
    /// </summary>
    [Fact]
    public async Task Deactivating_other_devices_is_refused_when_the_session_names_no_device()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await RegisterDeviceAsync("push-token-unattributed-0123456789");

        var response = await HttpClient.PostAsync(
            new Uri($"{Root}/devices/deactivate-others", UriKind.Relative), null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Subscriptions ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Driven by the catalogue, not by stored rows.</b> A member who has never touched a setting
    /// has no stored row for a category, and a list built from stored rows would show them nothing at
    /// all — which reads as "you are subscribed to nothing" when the truth is the opposite.
    /// </summary>
    [Fact]
    public async Task The_subscription_list_covers_every_category_including_untouched_ones()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var subscriptions = await ReadAsync<List<SubscriptionShape>>(
            await HttpClient.GetAsync(
                new Uri($"{Root}/preferences/subscriptions", UriKind.Relative)));

        subscriptions.Should().NotBeEmpty();
        subscriptions.Should().Contain(row => row.CategoryKey == "security");

        subscriptions.Single(row => row.CategoryKey == "security")
            .IsMandatory.Should().BeTrue("security messages cannot be switched off");

        subscriptions.Should().OnlyContain(
            row => row.UnsubscribedAt == null,
            "nobody has unsubscribed from anything yet");
    }

    // ── Unsubscribe ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The one write on this surface that works with no session, because it has to.</b> A member
    /// who has stopped opening the app is exactly the member the footer link exists for.
    /// </summary>
    [Fact]
    public async Task A_signed_link_switches_one_category_off_without_a_session()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        var token = MintUnsubscribeToken(auth.User.Id, "marketplace");

        using var anonymous = Factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync(
            new Uri($"{Root}/unsubscribe", UriKind.Relative), new { token });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await ReadAsync<UnsubscribeResultShape>(response);
        result.CategoryKey.Should().Be("marketplace");
        result.ManageRoute.Should().NotBeNullOrWhiteSpace();

        // And the member's own record says so, with the route it came in on.
        Api.Authenticate(auth.Tokens.AccessToken);

        var subscriptions = await ReadAsync<List<SubscriptionShape>>(
            await HttpClient.GetAsync(
                new Uri($"{Root}/preferences/subscriptions", UriKind.Relative)));

        var marketplace = subscriptions.Single(row => row.CategoryKey == "marketplace");

        marketplace.EmailEnabled.Should().BeFalse();
        marketplace.UnsubscribedAt.Should().NotBeNull();
        marketplace.UnsubscribeSource.Should().Be("emailLink");
    }

    /// <summary>
    /// A mail client that prefetches links, a corporate scanner following every URL, and a member who
    /// clicks twice all arrive more than once. The second visit must confirm rather than fail.
    /// </summary>
    [Fact]
    public async Task Following_the_same_link_twice_confirms_twice()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        var token = MintUnsubscribeToken(auth.User.Id, "marketplace");

        using var anonymous = Factory.CreateClient();
        var uri = new Uri($"{Root}/unsubscribe", UriKind.Relative);

        (await anonymous.PostAsJsonAsync(uri, new { token })).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await anonymous.PostAsJsonAsync(uri, new { token })).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// <b>A member must not be able to switch off the messages that tell them somebody signed in as
    /// them.</b> Refused with an error rather than a silent success: a confirmation page shown over
    /// a setting that did not change teaches a member that unsubscribes are ignored.
    /// </summary>
    [Fact]
    public async Task A_link_naming_a_mandatory_category_is_refused()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        var token = MintUnsubscribeToken(auth.User.Id, "security");

        using var anonymous = Factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync(
            new Uri($"{Root}/unsubscribe", UriKind.Relative), new { token });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// The endpoint is unauthenticated, so the signature is the only thing standing between it and
    /// anybody who can guess a member identifier.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("YWJjZGVm")]
    public async Task An_unsigned_or_forged_link_is_refused(string token)
    {
        using var anonymous = Factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync(
            new Uri($"{Root}/unsubscribe", UriKind.Relative), new { token });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Mints a token the way the email channel does — through the host's own factory, so the test
    /// signs with the deployment's key rather than reimplementing the construction it is verifying.
    /// </summary>
    private string MintUnsubscribeToken(Guid userId, string categoryKey)
    {
        using var scope = Factory.Services.CreateScope();

        return scope.ServiceProvider
            .GetRequiredService<UnsubscribeTokenFactory>()
            .Create(UserId.From(userId), categoryKey);
    }

    /// <summary>
    /// Registers a push endpoint and asserts it took, so a later assertion about the list is about
    /// the list rather than about a registration that quietly failed.
    /// </summary>
    private async Task RegisterDeviceAsync(string token)
    {
        var response = await HttpClient.PutAsJsonAsync(
            new Uri($"{Root}/devices", UriKind.Relative),
            new { token, platform = "Android" });

        response.StatusCode.Should().Be(
            HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private Task<(string PhoneNumber, Contracts.Identity.V1.Responses.AuthenticationResponse Auth)>
        SignInWithDeviceAsync(string deviceIdentifier) =>
        RegisterAndVerifyAsync(deviceIdentifier: deviceIdentifier, deviceName: deviceIdentifier);

    private async Task<Contracts.Identity.V1.Responses.AuthenticationResponse>
        SignInAgainWithDeviceAsync(string phoneNumber, string deviceIdentifier)
    {
        Api.ClearAuthentication();

        var response = await Api.LoginAsync(new LoginRequest
        {
            Email = EmailFor(phoneNumber),
            Password = ValidPassword,
            DeviceIdentifier = deviceIdentifier,
            DeviceName = deviceIdentifier
        });

        return await IdentityApiClient.ReadDataAsync<
            Contracts.Identity.V1.Responses.AuthenticationResponse>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    private sealed record SubscriptionShape
    {
        public string CategoryKey { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public bool EmailEnabled { get; init; }

        public bool IsMandatory { get; init; }

        public DateTimeOffset? UnsubscribedAt { get; init; }

        public string? UnsubscribeSource { get; init; }
    }

    private sealed record UnsubscribeResultShape
    {
        public string CategoryKey { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public string ManageRoute { get; init; } = string.Empty;
    }
}
