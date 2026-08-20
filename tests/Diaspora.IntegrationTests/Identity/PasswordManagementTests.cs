using System.Net;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Verification;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Identity;

public class PasswordManagementTests : IdentityTestBase
{
    private const string NewPassword = "Different-Mule-Cabinet-3";

    public PasswordManagementTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_member_can_change_their_password()
    {
        var (phone, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        var response = await Api.ChangePasswordAsync(new ChangePasswordRequest
        {
            CurrentPassword = ValidPassword,
            NewPassword = NewPassword
        });

        response.IsSuccessStatusCode.Should().BeTrue();

        Api.ClearAuthentication();
        var signIn = await Api.LoginAsync(new LoginRequest { PhoneNumber = phone, Password = NewPassword });
        signIn.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task The_old_password_stops_working_after_a_change()
    {
        var (phone, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        await Api.ChangePasswordAsync(new ChangePasswordRequest
        {
            CurrentPassword = ValidPassword,
            NewPassword = NewPassword
        });

        Api.ClearAuthentication();
        var signIn = await Api.LoginAsync(new LoginRequest { PhoneNumber = phone, Password = ValidPassword });

        signIn.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Changing_the_password_requires_the_current_one()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        var response = await Api.ChangePasswordAsync(new ChangePasswordRequest
        {
            CurrentPassword = "Not-The-Current-Password-1",
            NewPassword = NewPassword
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reusing_a_previous_password_is_refused()
    {
        var (phone, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        await Api.ChangePasswordAsync(new ChangePasswordRequest
        {
            CurrentPassword = ValidPassword,
            NewPassword = NewPassword
        });

        // The change rotated the security stamp, so the old access token is dead. Sign in
        // again with the new password before attempting the second change.
        Api.ClearAuthentication();
        var reAuth = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(
            await Api.LoginAsync(new LoginRequest { PhoneNumber = phone, Password = NewPassword }));

        Api.Authenticate(reAuth.Tokens.AccessToken);
        var revert = await Api.ChangePasswordAsync(new ChangePasswordRequest
        {
            CurrentPassword = NewPassword,
            NewPassword = ValidPassword
        });

        revert.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "the original password is still in the retained history");
    }

    [Fact]
    public async Task Changing_the_password_ends_every_other_session()
    {
        var (phone, first) = await RegisterAndVerifyAsync(deviceName: "Device A");

        var secondLogin = await Api.LoginAsync(new LoginRequest
        {
            PhoneNumber = phone, Password = ValidPassword, DeviceName = "Device B", Platform = "Web"
        });

        var second = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(secondLogin);

        Api.Authenticate(second.Tokens.AccessToken);
        await Api.ChangePasswordAsync(new ChangePasswordRequest
        {
            CurrentPassword = ValidPassword,
            NewPassword = NewPassword
        });

        Api.Authenticate(first.Tokens.AccessToken);
        (await Api.GetCurrentUserAsync()).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Account_recovery_always_acknowledges_the_same_way()
    {
        var (phone, _) = await RegisterAndVerifyAsync();

        var known = await Api.ForgotPasswordAsync(phone);
        var unknown = await Api.ForgotPasswordAsync(NextPhoneNumber());

        known.StatusCode.Should().Be(HttpStatusCode.OK);
        unknown.StatusCode.Should().Be(HttpStatusCode.OK);

        var knownBody = await known.Content.ReadAsStringAsync();
        var unknownBody = await unknown.Content.ReadAsStringAsync();

        // Identical wording; only the request id in the envelope differs.
        knownBody.Should().Contain("If that phone number belongs to a Diaspora account");
        unknownBody.Should().Contain("If that phone number belongs to a Diaspora account");
    }

    [Fact]
    public async Task A_member_can_recover_their_account_with_the_code()
    {
        var (phone, _) = await RegisterAndVerifyAsync();

        await Api.ForgotPasswordAsync(phone);
        var code = Factory.OtpCodes.LatestFor(phone, OtpPurpose.PasswordReset);

        var reset = await Api.ResetPasswordAsync(new ResetPasswordRequest
        {
            PhoneNumber = phone,
            Code = code,
            NewPassword = NewPassword
        });

        reset.IsSuccessStatusCode.Should().BeTrue();

        var signIn = await Api.LoginAsync(new LoginRequest { PhoneNumber = phone, Password = NewPassword });
        signIn.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task A_wrong_recovery_code_is_refused()
    {
        var (phone, _) = await RegisterAndVerifyAsync();
        await Api.ForgotPasswordAsync(phone);

        var reset = await Api.ResetPasswordAsync(new ResetPasswordRequest
        {
            PhoneNumber = phone,
            Code = "000000",
            NewPassword = NewPassword
        });

        reset.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task Recovery_revokes_every_session_including_the_callers()
    {
        // Recovery exists precisely because the previous credential may be in someone
        // else's hands; no session earns an exemption.
        var (phone, auth) = await RegisterAndVerifyAsync();

        await Api.ForgotPasswordAsync(phone);
        var code = Factory.OtpCodes.LatestFor(phone, OtpPurpose.PasswordReset);

        await Api.ResetPasswordAsync(new ResetPasswordRequest
        {
            PhoneNumber = phone,
            Code = code,
            NewPassword = NewPassword
        });

        Api.Authenticate(auth.Tokens.AccessToken);
        (await Api.GetCurrentUserAsync()).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_recovery_code_cannot_be_redeemed_twice()
    {
        var (phone, _) = await RegisterAndVerifyAsync();

        await Api.ForgotPasswordAsync(phone);
        var code = Factory.OtpCodes.LatestFor(phone, OtpPurpose.PasswordReset);

        await Api.ResetPasswordAsync(new ResetPasswordRequest
        {
            PhoneNumber = phone, Code = code, NewPassword = NewPassword
        });

        var replay = await Api.ResetPasswordAsync(new ResetPasswordRequest
        {
            PhoneNumber = phone, Code = code, NewPassword = "Yet-Another-Password-5"
        });

        replay.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task A_phone_verification_code_cannot_be_used_to_reset_a_password()
    {
        // Purposes are a security boundary: a code obtained for one flow must not
        // authorise another.
        var phone = await RegisterAsync();
        var verificationCode = Factory.OtpCodes.LatestFor(phone, OtpPurpose.PhoneVerification);

        var reset = await Api.ResetPasswordAsync(new ResetPasswordRequest
        {
            PhoneNumber = phone,
            Code = verificationCode,
            NewPassword = NewPassword
        });

        reset.IsSuccessStatusCode.Should().BeFalse();
    }
}
