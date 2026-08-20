using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Verification;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Identity;

/// <summary>
/// The hybrid authentication strategy: password and phone-OTP are interchangeable ways to
/// prove identity, both ending in the same session and token pair. Password sign-in — the
/// path B2 shipped and the integration suite already covers elsewhere — must keep working
/// unchanged; these tests add the passwordless path the frontend leads with.
/// </summary>
public sealed class HybridAuthenticationTests : IdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public HybridAuthenticationTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_member_can_sign_in_with_a_one_time_code()
    {
        var (phone, _) = await RegisterAndVerifyAsync();
        Api.ClearAuthentication();

        // Start: request a sign-in code.
        var start = await HttpClient.PostAsJsonAsync(
            "/api/v1/auth/otp/start", new RequestSignInOtpRequest { PhoneNumber = phone }, JsonOptions);
        start.StatusCode.Should().Be(HttpStatusCode.OK);

        Factory.OtpCodes.HasCodeFor(phone, OtpPurpose.SignIn)
            .Should().BeTrue("a sign-in code should have been dispatched");
        var code = Factory.OtpCodes.LatestFor(phone, OtpPurpose.SignIn);

        // Complete: redeem it for a token pair — the same response password sign-in returns.
        var signIn = await HttpClient.PostAsJsonAsync(
            "/api/v1/auth/otp/sign-in",
            new OtpSignInRequest { PhoneNumber = phone, Code = code, Platform = "Android" },
            JsonOptions);

        var auth = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(signIn);
        auth.Tokens.AccessToken.Should().NotBeNullOrEmpty();
        auth.Tokens.RefreshToken.Should().NotBeNullOrEmpty();
        auth.SessionId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Password_sign_in_still_works_unchanged()
    {
        var (phone, _) = await RegisterAndVerifyAsync();
        Api.ClearAuthentication();

        var response = await Api.LoginAsync(new LoginRequest { PhoneNumber = phone, Password = ValidPassword });

        var auth = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(response);
        auth.Tokens.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task The_start_endpoint_acknowledges_an_unknown_number_identically()
    {
        // No account enumeration: an unregistered number gets the same acknowledgement as a
        // registered one, and no code is dispatched.
        var unknown = NextPhoneNumber();

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/auth/otp/start", new RequestSignInOtpRequest { PhoneNumber = unknown }, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Factory.OtpCodes.HasCodeFor(unknown, OtpPurpose.SignIn).Should().BeFalse();
    }

    [Fact]
    public async Task A_wrong_code_is_refused_indistinguishably()
    {
        var (phone, _) = await RegisterAndVerifyAsync();
        Api.ClearAuthentication();

        await HttpClient.PostAsJsonAsync(
            "/api/v1/auth/otp/start", new RequestSignInOtpRequest { PhoneNumber = phone }, JsonOptions);

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/auth/otp/sign-in",
            new OtpSignInRequest { PhoneNumber = phone, Code = "000000" },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_registration_code_cannot_be_replayed_as_a_sign_in_code()
    {
        // Purposes are isolated: the code from the registration flow is minted for
        // PhoneVerification and must not authorise a sign-in.
        var phone = await RegisterAsync();
        var registrationCode = Factory.OtpCodes.LatestFor(phone, OtpPurpose.PhoneVerification);
        Api.ClearAuthentication();

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/auth/otp/sign-in",
            new OtpSignInRequest { PhoneNumber = phone, Code = registrationCode },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
