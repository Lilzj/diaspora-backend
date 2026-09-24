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
[Collection(IdentityCollection.Name)]
public sealed class HybridAuthenticationTests : PooledIdentityTestBase
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

        var response = await Api.LoginAsync(new LoginRequest { Email = EmailFor(phone), Password = ValidPassword });

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

    /// <summary>
    /// <b>These tests used to expect 401 for every refusal</b>, and had failed since the provider moved
    /// from "the phone number or password is incorrect" to an OTP-shaped 400. The status was never the
    /// property that matters. What matters is that a refused code for a registered number reads exactly
    /// like one for a number nobody registered, so they now compare the two.
    /// </summary>
    [Fact]
    public async Task A_wrong_code_is_refused_indistinguishably()
    {
        var (phone, _) = await RegisterAndVerifyAsync();
        Api.ClearAuthentication();

        await StartAsync(phone);
        var sent = Factory.OtpCodes.LatestFor(phone, OtpPurpose.SignIn);

        var wrongCode = await SignInAsync(phone, WrongCodeFor(sent));
        var unknownNumber = await SignInAsync(NextPhoneNumber(), WrongCodeFor(sent));

        wrongCode.IsSuccessStatusCode.Should().BeFalse();
        await ShouldReadAlikeAsync(wrongCode, unknownNumber);
        (await ProblemAsync(wrongCode)).Code.Should().Be("Identity.Otp.IncorrectCode");
    }

    [Fact]
    public async Task A_registration_code_cannot_be_replayed_as_a_sign_in_code()
    {
        // Purposes are isolated: the code from the registration flow is minted for
        // PhoneVerification and must not authorise a sign-in.
        //
        // No sign-in code was ever requested for this number either. That case answered 404
        // "No verification code is outstanding" until 17 Sep 2026, where an unregistered number
        // answered 400, so anyone could ask whether a number had an account without texting it.
        var phone = await RegisterAsync();
        var registrationCode = Factory.OtpCodes.LatestFor(phone, OtpPurpose.PhoneVerification);
        Api.ClearAuthentication();

        var replayed = await SignInAsync(phone, registrationCode);
        var unknownNumber = await SignInAsync(NextPhoneNumber(), registrationCode);

        replayed.IsSuccessStatusCode.Should().BeFalse();
        await ShouldReadAlikeAsync(replayed, unknownNumber);
    }

    [Fact]
    public async Task A_member_who_signed_in_with_a_code_is_not_given_away_by_it_afterwards()
    {
        // Their newest sign-in challenge is spent, and it answered 409 "already used" to any code
        // anybody posted against the number.
        var (phone, _) = await RegisterAndVerifyAsync();
        Api.ClearAuthentication();

        await StartAsync(phone);
        var sent = Factory.OtpCodes.LatestFor(phone, OtpPurpose.SignIn);
        (await SignInAsync(phone, sent)).StatusCode.Should().Be(HttpStatusCode.OK);

        var stranger = await SignInAsync(phone, WrongCodeFor(sent));
        var unknownNumber = await SignInAsync(NextPhoneNumber(), WrongCodeFor(sent));

        await ShouldReadAlikeAsync(stranger, unknownNumber);
    }

    [Fact]
    public async Task Only_the_holder_of_a_locked_code_is_told_to_request_a_new_one()
    {
        var (phone, _) = await RegisterAndVerifyAsync();
        Api.ClearAuthentication();

        await StartAsync(phone);
        var sent = Factory.OtpCodes.LatestFor(phone, OtpPurpose.SignIn);

        // The test host allows three attempts (Identity:Otp:MaxVerificationAttempts). The guess
        // that uses up the budget reads like every other wrong guess, or it would tell a stranger
        // that a code was outstanding.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var guess = await SignInAsync(phone, WrongCodeFor(sent));
            var unknownNumber = await SignInAsync(NextPhoneNumber(), WrongCodeFor(sent));

            await ShouldReadAlikeAsync(guess, unknownNumber);
        }

        var holder = await SignInAsync(phone, sent);

        holder.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await ProblemAsync(holder)).Code.Should().Be("Identity.Otp.AttemptLimitReached");
    }

    // -- Helpers ---------------------------------------------------------------

    private async Task StartAsync(string phone)
    {
        var start = await HttpClient.PostAsJsonAsync(
            "/api/v1/auth/otp/start", new RequestSignInOtpRequest { PhoneNumber = phone }, JsonOptions);

        start.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private Task<HttpResponseMessage> SignInAsync(string phone, string code) =>
        HttpClient.PostAsJsonAsync(
            "/api/v1/auth/otp/sign-in",
            new OtpSignInRequest { PhoneNumber = phone, Code = code },
            JsonOptions);

    /// <summary>A code that is certainly not the one sent, whatever the generator drew.</summary>
    private static string WrongCodeFor(string sent) => sent == "000000" ? "111111" : "000000";

    /// <summary>
    /// Status, error code, title and wording must all match. Any one of them differing turns the
    /// route into an answer to "is this number registered?".
    /// </summary>
    private static async Task ShouldReadAlikeAsync(HttpResponseMessage registered, HttpResponseMessage unknown)
    {
        registered.StatusCode.Should().Be(unknown.StatusCode);

        var registeredProblem = await ProblemAsync(registered);
        var unknownProblem = await ProblemAsync(unknown);

        registeredProblem.Should().Be(unknownProblem);
    }

    private static async Task<ProblemShape> ProblemAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await IdentityApiClient.ReadProblemDetailAsync(response));
        var root = document.RootElement;

        string Read(string name) =>
            root.TryGetProperty(name, out var value) ? value.GetString() ?? string.Empty : string.Empty;

        return new ProblemShape(Read("code"), Read("title"), Read("detail"));
    }

    private sealed record ProblemShape(string Code, string Title, string Detail);
}
