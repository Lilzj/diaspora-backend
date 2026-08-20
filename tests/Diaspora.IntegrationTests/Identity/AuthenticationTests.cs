using System.Net;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Identity;

public class AuthenticationTests : IdentityTestBase
{
    public AuthenticationTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// The platform's composed feature catalogue, read from the running host — so the assertions
    /// below are about what this deployment actually declares, not a fixture that could drift
    /// from it.
    /// </summary>
    private IReadOnlyList<FeatureDefinition> AllFeatures =>
        Factory.Services.GetRequiredService<IFeatureCatalog>().All;

    [Fact]
    public async Task A_verified_member_can_sign_in()
    {
        var (phone, _) = await RegisterAndVerifyAsync();

        var response = await Api.LoginAsync(new LoginRequest
        {
            PhoneNumber = phone,
            Password = ValidPassword,
            DeviceName = "Pixel 8",
            Platform = "Android"
        });

        var auth = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(response);

        auth.User.AccessState.Should().Be("LimitedAccess");
        auth.SessionId.Should().NotBeEmpty();
        auth.Tokens.TokenType.Should().Be("Bearer");
    }

    [Fact]
    public async Task A_wrong_password_is_refused()
    {
        var (phone, _) = await RegisterAndVerifyAsync();

        var response = await Api.LoginAsync(new LoginRequest
        {
            PhoneNumber = phone,
            Password = "Wrong-Horse-Battery-9"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_unknown_number_and_a_wrong_password_are_indistinguishable()
    {
        // Any difference here — status, body, or error code — turns sign-in into an
        // account-enumeration oracle.
        var (phone, _) = await RegisterAndVerifyAsync();

        var wrongPassword = await Api.LoginAsync(new LoginRequest
        {
            PhoneNumber = phone,
            Password = "Wrong-Horse-Battery-9"
        });

        var unknownNumber = await Api.LoginAsync(new LoginRequest
        {
            PhoneNumber = NextPhoneNumber(),
            Password = "Wrong-Horse-Battery-9"
        });

        unknownNumber.StatusCode.Should().Be(wrongPassword.StatusCode);

        var wrongBody = await IdentityApiClient.ReadProblemDetailAsync(wrongPassword);
        var unknownBody = await IdentityApiClient.ReadProblemDetailAsync(unknownNumber);

        ExtractTitle(unknownBody).Should().Be(ExtractTitle(wrongBody));
    }

    [Fact]
    public async Task An_unverified_account_cannot_sign_in_to_full_access()
    {
        var phone = await RegisterAsync();

        var response = await Api.LoginAsync(new LoginRequest
        {
            PhoneNumber = phone,
            Password = ValidPassword
        });

        // Sign-in succeeds — the member needs a token to complete verification — but the
        // gateway routes them straight back to the phone verification screen.
        var auth = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(response);
        auth.Access.Destination.Should().Be("PhoneVerification");

        // The canonical reading of AccessTier.Onboarding, per B2.0.1: this subject holds
        // onboarding capabilities and nothing else.
        //
        // This assertion was originally "no gates at all", which encoded the reading that
        // Onboarding grants nothing. That was never quite true — Location has opened its country
        // catalogue at Onboarding since B4 so registration can ask for a country of origin — and
        // it became observably false in B5, when Media opened onboarding-tier *gates* and the
        // catalogue started reporting them. B2.0.1 settled the question in Identity's favour on
        // substance and Media's on outcome: Onboarding is real, and it admits exactly one
        // category of capability.
        var granted = auth.Access.GrantedFeatures;

        granted.Should().NotBeEmpty(
            "a subject mid-onboarding can still add a profile photograph and upload verification "
            + "evidence — gating those behind verification is a loop with no entrance");

        granted.Should().OnlyContain(
            name => AllFeatures.Single(feature => feature.Name == name).MinimumTier
                    == AccessTier.Onboarding,
            "nothing above the onboarding rung may be reachable before the phone is verified");

        granted.Should().OnlyContain(
            name => CapabilityClassifier.Classify(
                AllFeatures.Single(feature => feature.Name == name))
                    == CapabilityCategory.Onboarding);

        granted.Should().NotContain(
            name => CapabilityClassifier.ReadsAsMembershipPrivilege(name),
            "reading the community, commenting, reacting, following, messaging and publishing "
            + "are membership privileges and never open before onboarding is complete");
    }

    [Fact]
    public async Task Repeated_failures_trigger_a_temporary_lockout()
    {
        var (phone, _) = await RegisterAndVerifyAsync();

        for (var i = 0; i < 3; i++)
        {
            await Api.LoginAsync(new LoginRequest { PhoneNumber = phone, Password = "Wrong-Password-1" });
        }

        var afterLockout = await Api.LoginAsync(new LoginRequest
        {
            PhoneNumber = phone,
            Password = ValidPassword
        });

        afterLockout.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "the lockout must hold even against the correct password");
    }

    [Fact]
    public async Task The_access_token_opens_authenticated_endpoints()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        var response = await Api.GetCurrentUserAsync();

        var profile = await IdentityApiClient.ReadDataAsync<UserProfileResponse>(response);
        profile.Id.Should().Be(auth.User.Id);
    }

    [Fact]
    public async Task An_unauthenticated_request_is_refused()
    {
        var response = await Api.GetCurrentUserAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_tampered_token_is_refused()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        // Flip the last character of the signature.
        var token = auth.Tokens.AccessToken;
        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');

        Api.Authenticate(tampered);
        var response = await Api.GetCurrentUserAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_profile_response_masks_the_phone_number()
    {
        var (phone, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        var response = await Api.GetCurrentUserAsync();
        var payload = await response.Content.ReadAsStringAsync();

        payload.Should().NotContain(phone);
    }

    [Fact]
    public async Task Signing_out_immediately_invalidates_the_access_token()
    {
        // The whole point of validating the session on every request: revocation cannot
        // wait for the access token to expire.
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        (await Api.GetCurrentUserAsync()).IsSuccessStatusCode.Should().BeTrue();

        await Api.LogoutAsync();

        (await Api.GetCurrentUserAsync()).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string ExtractTitle(string problemJson)
    {
        using var document = System.Text.Json.JsonDocument.Parse(problemJson);
        return document.RootElement.TryGetProperty("title", out var title)
            ? title.GetString() ?? string.Empty
            : string.Empty;
    }
}
