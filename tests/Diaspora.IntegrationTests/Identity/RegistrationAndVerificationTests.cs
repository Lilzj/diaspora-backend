using System.Net;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Verification;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Identity;

/// <summary>
/// The onboarding path, end to end over HTTP.
/// </summary>
[Collection(IdentityCollection.Name)]
public class RegistrationAndVerificationTests : PooledIdentityTestBase
{
    public RegistrationAndVerificationTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Registration_creates_an_account_awaiting_phone_verification()
    {
        var phone = NextPhoneNumber();

        var response = await Api.RegisterAsync(RegistrationFor(phone));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await IdentityApiClient.ReadDataAsync<RegistrationResponse>(response);
        body.AccessState.Should().Be("PhoneVerificationPending");
        body.UserId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Registration_masks_the_phone_number_in_its_response()
    {
        var phone = NextPhoneNumber();

        var response = await Api.RegisterAsync(RegistrationFor(phone));
        var body = await IdentityApiClient.ReadDataAsync<RegistrationResponse>(response);

        body.PhoneNumber.Should().NotBe(phone);
        body.PhoneNumber.Should().Contain("*");
    }

    [Fact]
    public async Task Registration_issues_no_tokens()
    {
        // The account must be unusable until the phone is confirmed; issuing tokens here
        // would make verification a dismissible prompt rather than a gate.
        var phone = NextPhoneNumber();

        var response = await Api.RegisterAsync(RegistrationFor(phone));
        var payload = await response.Content.ReadAsStringAsync();

        payload.Should().NotContain("accessToken");
        payload.Should().NotContain("refreshToken");
    }

    [Fact]
    public async Task Registration_dispatches_a_verification_code()
    {
        var phone = NextPhoneNumber();

        await Api.RegisterAsync(RegistrationFor(phone));

        Factory.OtpCodes.HasCodeFor(phone, OtpPurpose.PhoneVerification).Should().BeTrue();
    }

    [Fact]
    public async Task A_duplicate_phone_number_is_refused()
    {
        var phone = NextPhoneNumber();
        await Api.RegisterAsync(RegistrationFor(phone));

        var second = await Api.RegisterAsync(RegistrationFor(phone));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_weak_password_is_refused()
    {
        var response = await Api.RegisterAsync(RegistrationFor(NextPhoneNumber(), "short"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_malformed_phone_number_is_refused()
    {
        var response = await Api.RegisterAsync(new RegisterRequest
        {
            FullName = "Amara Okafor",
            PhoneNumber = "08012345678",
            // Valid, so the malformed phone number is the only thing under test.
            Email = "malformed.phone@diaspora.test",
            Password = ValidPassword,
            Nationality = "NG",
            CountryOfResidence = "CA"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Verifying_the_phone_promotes_the_member_and_issues_tokens()
    {
        var phone = await RegisterAsync();
        var code = Factory.OtpCodes.LatestFor(phone, OtpPurpose.PhoneVerification);

        var response = await Api.VerifyPhoneAsync(new VerifyPhoneRequest
        {
            PhoneNumber = phone,
            Code = code,
            Platform = "Android"
        });

        var auth = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(response);

        auth.User.AccessState.Should().Be("LimitedAccess");
        auth.Tokens.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.Tokens.RefreshToken.Should().NotBeNullOrWhiteSpace();
        auth.Access.Destination.Should().Be("ExploreLimited");
    }

    [Fact]
    public async Task A_wrong_verification_code_is_refused()
    {
        var phone = await RegisterAsync();

        var response = await Api.VerifyPhoneAsync(new VerifyPhoneRequest
        {
            PhoneNumber = phone,
            Code = "000000"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Repeated_wrong_codes_exhaust_the_attempt_budget()
    {
        var phone = await RegisterAsync();
        var realCode = Factory.OtpCodes.LatestFor(phone, OtpPurpose.PhoneVerification);

        // Three attempts is the configured budget for the test host.
        for (var i = 0; i < 3; i++)
        {
            await Api.VerifyPhoneAsync(new VerifyPhoneRequest { PhoneNumber = phone, Code = "000000" });
        }

        var withCorrectCode = await Api.VerifyPhoneAsync(
            new VerifyPhoneRequest { PhoneNumber = phone, Code = realCode });

        withCorrectCode.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "the budget must hold even against the right code, or it is not a budget");
    }

    [Fact]
    public async Task A_verification_code_cannot_be_redeemed_twice()
    {
        var phone = await RegisterAsync();
        var code = Factory.OtpCodes.LatestFor(phone, OtpPurpose.PhoneVerification);

        await Api.VerifyPhoneAsync(new VerifyPhoneRequest { PhoneNumber = phone, Code = code });
        var replay = await Api.VerifyPhoneAsync(new VerifyPhoneRequest { PhoneNumber = phone, Code = code });

        replay.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task Resending_supersedes_the_previous_code()
    {
        var phone = await RegisterAsync();
        var original = Factory.OtpCodes.LatestFor(phone, OtpPurpose.PhoneVerification);

        await Api.ResendOtpAsync(new ResendOtpRequest { PhoneNumber = phone });
        var replacement = Factory.OtpCodes.LatestFor(phone, OtpPurpose.PhoneVerification);

        replacement.Should().NotBe(original);

        var withOldCode = await Api.VerifyPhoneAsync(
            new VerifyPhoneRequest { PhoneNumber = phone, Code = original });

        withOldCode.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task Resending_to_an_unregistered_number_looks_identical_to_a_registered_one()
    {
        // The endpoint is unauthenticated; any observable difference would make it an
        // account-enumeration oracle.
        var registered = await RegisterAsync();

        var known = await Api.ResendOtpAsync(new ResendOtpRequest { PhoneNumber = registered });
        var unknown = await Api.ResendOtpAsync(new ResendOtpRequest { PhoneNumber = NextPhoneNumber() });

        unknown.StatusCode.Should().Be(known.StatusCode);
    }

    [Fact]
    public async Task A_verification_code_is_never_returned_over_the_wire()
    {
        var phone = NextPhoneNumber();

        var response = await Api.RegisterAsync(RegistrationFor(phone));
        var payload = await response.Content.ReadAsStringAsync();
        var code = Factory.OtpCodes.LatestFor(phone, OtpPurpose.PhoneVerification);

        payload.Should().NotContain(code);
    }
}
