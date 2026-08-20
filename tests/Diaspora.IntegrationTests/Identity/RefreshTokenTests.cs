using System.Net;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Identity;

/// <summary>
/// Refresh-token rotation and replay detection over the real endpoint.
/// </summary>
public class RefreshTokenTests : IdentityTestBase
{
    public RefreshTokenTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_refresh_token_can_be_exchanged_for_a_new_pair()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        var response = await Api.RefreshAsync(auth.Tokens.RefreshToken);
        var refreshed = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(response);

        refreshed.Tokens.RefreshToken.Should().NotBe(auth.Tokens.RefreshToken);
        refreshed.SessionId.Should().Be(auth.SessionId, "rotation stays within the same session");
    }

    [Fact]
    public async Task The_rotated_token_stops_working()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        await Api.RefreshAsync(auth.Tokens.RefreshToken);

        var replay = await Api.RefreshAsync(auth.Tokens.RefreshToken);

        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Replaying_a_rotated_token_revokes_the_whole_session()
    {
        // The theft signature: a legitimate client never presents a token twice, so the
        // safe reading is that the chain leaked. Both parties get signed out.
        var (_, auth) = await RegisterAndVerifyAsync();

        var firstRotation = await Api.RefreshAsync(auth.Tokens.RefreshToken);
        var current = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(firstRotation);

        // The attacker replays the token the genuine client already spent.
        var replay = await Api.RefreshAsync(auth.Tokens.RefreshToken);
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // The genuine client's freshly issued token is now dead too.
        var afterBreach = await Api.RefreshAsync(current.Tokens.RefreshToken);
        afterBreach.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Replay_detection_also_kills_the_access_token()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        var rotation = await Api.RefreshAsync(auth.Tokens.RefreshToken);
        var current = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(rotation);

        await Api.RefreshAsync(auth.Tokens.RefreshToken);

        Api.Authenticate(current.Tokens.AccessToken);
        var response = await Api.GetCurrentUserAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_unknown_refresh_token_is_refused()
    {
        var response = await Api.RefreshAsync("not-a-token-that-was-ever-issued");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refreshing_after_sign_out_is_refused()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        await Api.LogoutAsync();
        Api.ClearAuthentication();

        var response = await Api.RefreshAsync(auth.Tokens.RefreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rotation_can_be_repeated_indefinitely_within_the_session()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        var token = auth.Tokens.RefreshToken;

        for (var i = 0; i < 5; i++)
        {
            var response = await Api.RefreshAsync(token);
            var refreshed = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(response);
            token = refreshed.Tokens.RefreshToken;
        }

        token.Should().NotBe(auth.Tokens.RefreshToken);
    }
}
