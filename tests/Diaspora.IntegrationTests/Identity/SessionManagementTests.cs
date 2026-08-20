using System.Net;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Identity;

public class SessionManagementTests : IdentityTestBase
{
    public SessionManagementTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_member_can_see_their_own_sessions()
    {
        var (_, auth) = await RegisterAndVerifyAsync(deviceName: "Pixel 8");

        Api.Authenticate(auth.Tokens.AccessToken);
        var response = await Api.GetSessionsAsync();

        var sessions = await IdentityApiClient.ReadDataAsync<IReadOnlyList<SessionResponse>>(response);

        sessions.Should().ContainSingle();
        sessions[0].IsCurrent.Should().BeTrue();
        sessions[0].DeviceName.Should().Be("Pixel 8");
    }

    [Fact]
    public async Task Signing_in_on_a_second_device_creates_a_second_session()
    {
        var (phone, first) = await RegisterAndVerifyAsync(deviceName: "Pixel 8");

        var secondLogin = await Api.LoginAsync(new Contracts.Identity.V1.Requests.LoginRequest
        {
            PhoneNumber = phone,
            Password = ValidPassword,
            DeviceName = "MacBook Pro",
            Platform = "Web"
        });

        var second = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(secondLogin);
        second.SessionId.Should().NotBe(first.SessionId);

        Api.Authenticate(second.Tokens.AccessToken);
        var sessions = await IdentityApiClient.ReadDataAsync<IReadOnlyList<SessionResponse>>(
            await Api.GetSessionsAsync());

        sessions.Should().HaveCount(2);
        sessions.Single(s => s.IsCurrent).DeviceName.Should().Be("MacBook Pro");
    }

    [Fact]
    public async Task A_member_can_revoke_another_device()
    {
        var (phone, first) = await RegisterAndVerifyAsync(deviceName: "Pixel 8");

        var secondLogin = await Api.LoginAsync(new Contracts.Identity.V1.Requests.LoginRequest
        {
            PhoneNumber = phone,
            Password = ValidPassword,
            DeviceName = "MacBook Pro",
            Platform = "Web"
        });

        var second = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(secondLogin);

        Api.Authenticate(second.Tokens.AccessToken);
        var revoke = await Api.RevokeSessionAsync(first.SessionId);
        revoke.IsSuccessStatusCode.Should().BeTrue();

        // The revoked device's token stops working immediately.
        Api.Authenticate(first.Tokens.AccessToken);
        (await Api.GetCurrentUserAsync()).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Revoking_the_current_session_is_refused_in_favour_of_signing_out()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        var response = await Api.RevokeSessionAsync(auth.SessionId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Another_members_session_reports_as_not_found()
    {
        // Reporting "forbidden" would confirm the session exists, which is more than the
        // caller is entitled to know.
        var (_, mine) = await RegisterAndVerifyAsync();
        var (_, theirs) = await RegisterAndVerifyAsync();

        Api.Authenticate(mine.Tokens.AccessToken);
        var response = await Api.RevokeSessionAsync(theirs.SessionId);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Revoke_all_other_sessions_leaves_the_current_one_alive()
    {
        var (phone, _) = await RegisterAndVerifyAsync(deviceName: "Device A");

        await Api.LoginAsync(new Contracts.Identity.V1.Requests.LoginRequest
        {
            PhoneNumber = phone, Password = ValidPassword, DeviceName = "Device B", Platform = "Web"
        });

        var thirdLogin = await Api.LoginAsync(new Contracts.Identity.V1.Requests.LoginRequest
        {
            PhoneNumber = phone, Password = ValidPassword, DeviceName = "Device C", Platform = "IOS"
        });

        var current = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(thirdLogin);

        Api.Authenticate(current.Tokens.AccessToken);
        (await Api.RevokeAllOtherSessionsAsync()).IsSuccessStatusCode.Should().BeTrue();

        var sessions = await IdentityApiClient.ReadDataAsync<IReadOnlyList<SessionResponse>>(
            await Api.GetSessionsAsync());

        sessions.Should().ContainSingle();
        sessions[0].DeviceName.Should().Be("Device C");
    }

    [Fact]
    public async Task Signing_out_everywhere_ends_every_session()
    {
        var (phone, first) = await RegisterAndVerifyAsync(deviceName: "Device A");

        var secondLogin = await Api.LoginAsync(new Contracts.Identity.V1.Requests.LoginRequest
        {
            PhoneNumber = phone, Password = ValidPassword, DeviceName = "Device B", Platform = "Web"
        });

        var second = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(secondLogin);

        Api.Authenticate(second.Tokens.AccessToken);
        await Api.LogoutAsync(revokeAllSessions: true);

        Api.Authenticate(first.Tokens.AccessToken);
        (await Api.GetCurrentUserAsync()).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_session_list_never_exposes_refresh_tokens()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        var payload = await (await Api.GetSessionsAsync()).Content.ReadAsStringAsync();

        payload.Should().NotContain(auth.Tokens.RefreshToken);
        payload.Should().NotContain("tokenHash");
    }
}
