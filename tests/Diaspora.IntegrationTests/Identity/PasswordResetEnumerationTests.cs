using System.Net;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.Domain.Identity.Verification;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Identity;

/// <summary>
/// Whether the recovery routes will tell you an address has an account.
/// <para>
/// <b>The reset route answered the question four different ways.</b> It is anonymous — a caller
/// needs no session and no code — and it replied <c>NotFound</c> for an address with no account,
/// while an address that had one got the challenge's own state back: "no code outstanding",
/// "already used", "too many attempts". Each is a different answer, and each of them means
/// <em>this address is registered</em>. Nobody had to request a code, and no message ever reached
/// the member.
/// </para>
/// <para>
/// The same defect was found on the OTP sign-in route on 17 Sep 2026 and fixed with
/// <c>VerifyForAnonymousCallerAsync</c>, which tells a challenge's state only to whoever typed
/// the code it sent. The reset route kept calling the plain <c>VerifyAsync</c>, so the fix went
/// past it.
/// </para>
/// <para>
/// Each test below is one state the route used to distinguish. They are separate because a single
/// test would go green the moment any one of them was collapsed.
/// </para>
/// </summary>
public sealed class PasswordResetEnumerationTests : IdentityTestBase
{
    private const string NewPassword = "Another-Correct-Horse-9";

    public PasswordResetEnumerationTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// A registered address with a code outstanding, against one with no account. The plainest
    /// form of the question.
    /// </summary>
    [Fact]
    public async Task A_wrong_code_and_an_unknown_address_answer_identically()
    {
        var (phone, _) = await RegisterAndVerifyAsync();
        await Api.ForgotPasswordAsync(EmailFor(phone));

        var registered = await ResetAsync(EmailFor(phone), "000000");
        var unknown = await ResetAsync(EmailFor(NextPhoneNumber()), "000000");

        await ShouldBeIndistinguishable(registered, unknown);
    }

    /// <summary>
    /// A registered address that never asked for a code. This answered "no verification code is
    /// outstanding" — which is a fact about an account, and therefore proof of one.
    /// </summary>
    [Fact]
    public async Task An_address_with_no_code_outstanding_is_indistinguishable_from_an_unknown_one()
    {
        var (phone, _) = await RegisterAndVerifyAsync();

        // Deliberately no ForgotPasswordAsync: nothing was ever issued for this account.
        var registered = await ResetAsync(EmailFor(phone), "000000");
        var unknown = await ResetAsync(EmailFor(NextPhoneNumber()), "000000");

        await ShouldBeIndistinguishable(registered, unknown);
    }

    /// <summary>
    /// A wrong guess against an account whose code has already been spent. This answered
    /// "already used", which says both that the address is registered and that somebody recently
    /// recovered it — to a caller holding no code at all.
    /// </summary>
    [Fact]
    public async Task A_wrong_guess_after_the_code_was_spent_is_indistinguishable()
    {
        var (phone, _) = await RegisterAndVerifyAsync();
        await Api.ForgotPasswordAsync(EmailFor(phone));

        var code = Factory.OtpCodes.LatestFor(phone, OtpPurpose.PasswordReset);

        (await ResetAsync(EmailFor(phone), code)).IsSuccessStatusCode.Should().BeTrue(
            "the first redemption is the member's own and must work");

        // A guess, not the code. This caller holds nothing, so they learn nothing.
        var registered = await ResetAsync(EmailFor(phone), "000000");
        var unknown = await ResetAsync(EmailFor(NextPhoneNumber()), "000000");

        await ShouldBeIndistinguishable(registered, unknown);
    }

    /// <summary>
    /// The line the design draws, pinned so it is not collapsed by accident.
    /// <para>
    /// Somebody who types the real code is told the truth — here, that it has already been used
    /// — because they demonstrably hold it, and holding an account's code is not something they
    /// could have learned from this route. Answering them "incorrect" would send a member who
    /// followed their recovery link twice hunting for a typo that is not there.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_holder_of_a_spent_code_is_told_it_was_spent()
    {
        var (phone, _) = await RegisterAndVerifyAsync();
        await Api.ForgotPasswordAsync(EmailFor(phone));

        var code = Factory.OtpCodes.LatestFor(phone, OtpPurpose.PasswordReset);

        (await ResetAsync(EmailFor(phone), code)).EnsureSuccessStatusCode();

        var replay = await ResetAsync(EmailFor(phone), code);
        var guess = await ResetAsync(EmailFor(phone), "000000");

        replay.StatusCode.Should().NotBe(
            guess.StatusCode,
            "the member holding the code is told what happened to it, and a guesser is not");
    }

    /// <summary>
    /// The half that must keep working. Closing an oracle by refusing everybody would pass every
    /// test above and leave nobody able to recover their account.
    /// </summary>
    [Fact]
    public async Task The_member_holding_the_code_can_still_reset()
    {
        var (phone, _) = await RegisterAndVerifyAsync();
        await Api.ForgotPasswordAsync(EmailFor(phone));

        var code = Factory.OtpCodes.LatestFor(phone, OtpPurpose.PasswordReset);

        (await ResetAsync(EmailFor(phone), code)).IsSuccessStatusCode.Should().BeTrue();

        var signedIn = await Api.LoginAsync(new LoginRequest
        {
            Email = EmailFor(phone),
            Password = NewPassword
        });

        signedIn.IsSuccessStatusCode.Should().BeTrue("the new password is the one that now works");
    }

    /// <summary>
    /// The same question, asked of phone verification.
    /// <para>
    /// Found by sweeping for the other callers of the plain <c>VerifyAsync</c> after fixing the
    /// reset route. This one carried a comment saying its answer "matched an expired or absent
    /// challenge, so this endpoint cannot be used to discover which numbers are registered" — and
    /// it matched an <em>absent</em> one. A registered number with a live challenge answered
    /// differently to the same guess, so a wrong code told a caller whether that number had just
    /// registered.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_wrong_phone_code_does_not_say_whether_the_number_is_registered()
    {
        // Registered and mid-verification: a challenge is outstanding for this number.
        var registeredPhone = await RegisterAsync();

        var registered = await Api.VerifyPhoneAsync(new VerifyPhoneRequest
        {
            PhoneNumber = registeredPhone,
            Code = "000000",
            Platform = "Android",
            AppVersion = "1.0.0"
        });

        var unknown = await Api.VerifyPhoneAsync(new VerifyPhoneRequest
        {
            PhoneNumber = NextPhoneNumber(),
            Code = "000000",
            Platform = "Android",
            AppVersion = "1.0.0"
        });

        await ShouldBeIndistinguishable(registered, unknown);
    }

    /// <summary>
    /// A number that has already finished verifying, guessed at.
    /// <para>
    /// This is the half a wrong code alone does not reach: a live challenge answers "incorrect"
    /// whichever way it is asked, so only a challenge in some <em>other</em> state shows whether
    /// its state is being handed out. A spent one used to answer "already used", which says the
    /// number is registered and has been through verification.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_number_that_already_verified_is_indistinguishable_from_an_unknown_one()
    {
        // Registers and spends the code, leaving a redeemed challenge behind.
        var (verifiedPhone, _) = await RegisterAndVerifyAsync();

        var registered = await Api.VerifyPhoneAsync(new VerifyPhoneRequest
        {
            PhoneNumber = verifiedPhone,
            Code = "000000",
            Platform = "Android",
            AppVersion = "1.0.0"
        });

        var unknown = await Api.VerifyPhoneAsync(new VerifyPhoneRequest
        {
            PhoneNumber = NextPhoneNumber(),
            Code = "000000",
            Platform = "Android",
            AppVersion = "1.0.0"
        });

        await ShouldBeIndistinguishable(registered, unknown);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private Task<HttpResponseMessage> ResetAsync(string email, string code) =>
        Api.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email = email,
            Code = code,
            NewPassword = NewPassword
        });

    /// <summary>
    /// Status, title and error code all have to match. Any one of them differing is the oracle,
    /// and a test that compared only the status would have passed while the body gave it away.
    /// </summary>
    private static async Task ShouldBeIndistinguishable(
        HttpResponseMessage registered, HttpResponseMessage unknown)
    {
        unknown.StatusCode.Should().Be(registered.StatusCode);

        var registeredBody = await Read(registered);
        var unknownBody = await Read(unknown);

        unknownBody.Title.Should().Be(registeredBody.Title);
        unknownBody.Code.Should().Be(registeredBody.Code);
    }

    private static async Task<(string Title, string Code)> Read(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(body))
        {
            return (string.Empty, string.Empty);
        }

        using var document = JsonDocument.Parse(body);

        return (
            document.RootElement.TryGetProperty("title", out var title)
                ? title.GetString() ?? string.Empty
                : string.Empty,
            document.RootElement.TryGetProperty("code", out var code)
                ? code.GetString() ?? string.Empty
                : string.Empty);
    }
}
