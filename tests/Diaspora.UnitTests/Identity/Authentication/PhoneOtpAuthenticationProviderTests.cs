using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Authentication;
using Diaspora.Application.Identity.Authentication.Providers;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Users;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.Domain.Identity.Verification;
using Diaspora.SharedKernel.Results;
using Diaspora.SharedKernel.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Identity.Authentication;

/// <summary>
/// What a member is told when a one-time code does not work.
/// <para>
/// <b>This provider used to answer "The phone number or password is incorrect."</b> —
/// <see cref="UserErrors.InvalidCredentials"/> — on a flow that has no password. The one
/// screen a member reaches by typing six digits told them their password was wrong, while the
/// sub-line underneath it said "Check the code", so the screen contradicted itself.
/// </para>
/// <para>
/// The reason it did was sound, and these tests exist to keep it sound: an unknown number and
/// a wrong code have to be indistinguishable, or the route becomes an oracle for which numbers
/// are registered. The fix moved both onto the same OTP-shaped error rather than leaking the
/// difference — so the wording is right for the flow <em>and</em> the two cases still read
/// identically.
/// </para>
/// <para>
/// Found on a hosted deployment, because the app's OTP sign-in cannot be exercised without
/// one: Production refuses to log codes and no SMS provider is configured.
/// </para>
/// </summary>
public sealed class PhoneOtpAuthenticationProviderTests
{
    private const string KnownNumber = "+2348100200001";
    private const string UnknownNumber = "+2348100999999";

    [Fact]
    public async Task An_unknown_number_is_told_the_code_is_wrong_not_the_password()
    {
        var result = await Build(user: null)
            .AuthenticateAsync(Credentials(UnknownNumber, "123456"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(OtpErrors.IncorrectCode);
        // The regression itself: no password exists anywhere in this flow.
        result.Error.Description.Should().NotContain("password");
    }

    [Fact]
    public async Task A_malformed_number_is_told_the_same_thing()
    {
        var result = await Build(user: null)
            .AuthenticateAsync(Credentials("not-a-number", "123456"));

        result.Error.Should().Be(OtpErrors.IncorrectCode);
    }

    [Fact]
    public async Task An_unknown_number_and_a_wrong_code_are_indistinguishable()
    {
        // The enumeration property, as an assertion. If these two ever diverge,
        // this route starts answering "is that number registered?".
        var unknown = await Build(user: null)
            .AuthenticateAsync(Credentials(UnknownNumber, "123456"));

        var wrongCode = await Build(Member(), Result.Failure<OtpChallenge>(OtpErrors.IncorrectCode))
            .AuthenticateAsync(Credentials(KnownNumber, "000000"));

        unknown.Error.Should().Be(wrongCode.Error);
    }

    [Fact]
    public async Task An_expired_code_says_so()
    {
        // Past the unknown-number check the challenge is the caller's own — they
        // asked for it — so saying it expired reveals nothing they did not already
        // know. It is also the one thing that makes the resend button make sense
        // rather than look like a second guess at the same wrong code.
        var result = await Build(Member(), Result.Failure<OtpChallenge>(OtpErrors.Expired))
            .AuthenticateAsync(Credentials(KnownNumber, "123456"));

        result.Error.Should().Be(OtpErrors.Expired);
        result.Error.Description.Should().NotContain("password");
    }

    [Fact]
    public async Task An_exhausted_challenge_says_so_too()
    {
        // Flattening this into "incorrect" sends somebody on retrying a challenge
        // that cannot accept another attempt.
        var result = await Build(Member(), Result.Failure<OtpChallenge>(OtpErrors.AttemptLimitReached))
            .AuthenticateAsync(Credentials(KnownNumber, "123456"));

        result.Error.Should().Be(OtpErrors.AttemptLimitReached);
    }

    [Fact]
    public async Task No_failure_on_this_route_mentions_a_password()
    {
        // The whole class of defect, in one assertion.
        var failures = new[]
        {
            (await Build(user: null).AuthenticateAsync(Credentials(UnknownNumber, "1"))).Error,
            (await Build(Member(), Result.Failure<OtpChallenge>(OtpErrors.IncorrectCode))
                .AuthenticateAsync(Credentials(KnownNumber, "1"))).Error,
            (await Build(Member(), Result.Failure<OtpChallenge>(OtpErrors.Expired))
                .AuthenticateAsync(Credentials(KnownNumber, "1"))).Error,
        };

        failures.Should().OnlyContain(error => !error.Description.Contains("password"));
    }

    // ── Fixtures ────────────────────────────────────────────────────────────

    private static AuthenticationCredentials Credentials(string phone, string code) =>
        new(phone) { Code = code };

    private static User Member() =>
        User.Register(
            FullName.Create("Amara Okafor"),
            PhoneNumber.Create(KnownNumber),
            PasswordHash.Create(new string('a', 60)),
            CountryCode.Create("NG"),
            CountryCode.Create("GB"),
            LanguageCode.Create("en"),
            TimeZoneIdentifier.Create("Europe/London"),
            DateTimeOffset.UtcNow);

    private static PhoneOtpAuthenticationProvider Build(
        User? user,
        Result<OtpChallenge>? verification = null)
    {
        var users = Substitute.For<IUserRepository>();
        users
            .GetByPhoneNumberAsync(Arg.Any<PhoneNumber>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(user));

        var challenges = Substitute.For<IOtpChallengeService>();
        challenges
            .VerifyAsync(
                Arg.Any<User>(),
                Arg.Any<OtpPurpose>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(verification ?? Result.Failure<OtpChallenge>(OtpErrors.IncorrectCode)));

        return new PhoneOtpAuthenticationProvider(users, challenges);
    }
}
