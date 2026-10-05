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
/// Email and password is the only way to sign in (decided 5 Oct 2026).
/// <para>
/// Sign-in by one-time code (<c>POST /auth/otp/start</c>, <c>POST /auth/otp/sign-in</c>) and the
/// external-identity links (<c>/integrations/identities</c>) were removed rather than hidden, so
/// these tests pin that the routes are gone. Codes still verify a phone number.
/// </para>
/// </summary>
[Collection(IdentityCollection.Name)]
public sealed class SignInMethodTests : PooledIdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public SignInMethodTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_member_signs_in_with_email_and_password()
    {
        var (phone, _) = await RegisterAndVerifyAsync();
        Api.ClearAuthentication();

        var response = await Api.LoginAsync(new LoginRequest { Email = EmailFor(phone), Password = ValidPassword });

        var auth = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(response);
        auth.Tokens.AccessToken.Should().NotBeNullOrEmpty();
        auth.Tokens.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("/api/v1/auth/otp/start")]
    [InlineData("/api/v1/auth/otp/sign-in")]
    public async Task There_is_no_way_to_sign_in_with_a_code(string route)
    {
        var (phone, _) = await RegisterAndVerifyAsync();
        Api.ClearAuthentication();

        var response = await HttpClient.PostAsJsonAsync(
            route, new { phoneNumber = phone, code = "123456", platform = "Android" }, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_sign_in_code_cannot_be_requested_through_resend()
    {
        // With no challenge to replace, a resend issues a new one, so this route would otherwise
        // still text sign-in codes to any registered number.
        var (phone, _) = await RegisterAndVerifyAsync();
        Api.ClearAuthentication();

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/auth/resend-otp", new { phoneNumber = phone, purpose = "SignIn" }, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Factory.OtpCodes.HasCodeFor(phone, OtpPurpose.SignIn).Should().BeFalse();
    }

    [Fact]
    public async Task There_are_no_external_identities_to_list_or_unlink()
    {
        await RegisterAndVerifyAsync();

        (await HttpClient.GetAsync("/api/v1/integrations/identities"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await HttpClient.DeleteAsync("/api/v1/integrations/identities/identity.google?reauthToken=x"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
