using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Services;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Policies;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Users;
using Diaspora.Domain.Identity.Verification;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Diaspora.SharedKernel.ValueObjects;
using Diaspora.UnitTests.Identity.TestData;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Identity.Authentication;

/// <summary>
/// What somebody learns by posting a code against a number that is not theirs: that the code is
/// incorrect, and nothing more.
/// <para>
/// <b>Found 17 Sep 2026, from two integration tests that had been failing for weeks.</b> The
/// anonymous sign-in route passed the member's challenge state to whoever asked. A registered number
/// with no code outstanding answered 404 "No verification code is outstanding", while an
/// unregistered number answered 400 "incorrect". A member who had once signed in with a code
/// answered 409 "already used" to any code at all. Nobody had to request a code, or send the member
/// a text, to find out the number had an account.
/// </para>
/// <para>
/// The member still needs "expired", "already used" and "too many attempts" to know what to do next,
/// so those are kept, and told only to the caller who typed the code that was sent.
/// </para>
/// </summary>
public sealed class OtpChallengeServiceDisclosureTests
{
    private const string SentCode = "482913";
    private const string WrongCode = "000000";

    private static readonly OtpPolicy Policy = OtpPolicy.Create(
        TimeSpan.FromMinutes(10),
        maxVerificationAttempts: 3,
        maxResends: 2,
        resendCooldown: TimeSpan.FromSeconds(60));

    private readonly User _member = IdentityTestData.LimitedAccessUser();
    private readonly IOtpChallengeRepository _challenges = Substitute.For<IOtpChallengeRepository>();
    private DateTimeOffset _now = IdentityTestData.Now;

    [Fact]
    public async Task A_number_with_no_code_outstanding_reads_as_an_incorrect_code()
    {
        Outstanding(null);

        var result = await Service().VerifyForAnonymousCallerAsync(_member, OtpPurpose.SignIn, WrongCode);

        result.Error.Should().Be(OtpErrors.IncorrectCode);
    }

    [Fact]
    public async Task A_flow_that_already_knows_the_member_is_still_told_nothing_is_outstanding()
    {
        // Phone verification and password reset keep the answer they had. Only the anonymous
        // sign-in route changed.
        Outstanding(null);

        var result = await Service().VerifyAsync(_member, OtpPurpose.PhoneVerification, WrongCode);

        result.Error.Should().Be(OtpErrors.NotFound);
    }

    [Fact]
    public async Task A_used_code_is_named_only_to_the_caller_who_typed_it()
    {
        Outstanding(Issue());
        var service = Service();

        (await service.VerifyForAnonymousCallerAsync(_member, OtpPurpose.SignIn, SentCode))
            .IsSuccess.Should().BeTrue();

        var stranger = await service.VerifyForAnonymousCallerAsync(_member, OtpPurpose.SignIn, WrongCode);
        var holder = await service.VerifyForAnonymousCallerAsync(_member, OtpPurpose.SignIn, SentCode);

        stranger.Error.Should().Be(OtpErrors.IncorrectCode);
        holder.Error.Should().Be(OtpErrors.AlreadyUsed);
    }

    [Fact]
    public async Task An_expired_code_is_named_only_to_the_caller_who_typed_it()
    {
        Outstanding(Issue());
        _now = _now.Add(Policy.Lifetime).AddSeconds(1);
        var service = Service();

        var stranger = await service.VerifyForAnonymousCallerAsync(_member, OtpPurpose.SignIn, WrongCode);
        var holder = await service.VerifyForAnonymousCallerAsync(_member, OtpPurpose.SignIn, SentCode);

        stranger.Error.Should().Be(OtpErrors.IncorrectCode);
        holder.Error.Should().Be(OtpErrors.Expired);
    }

    [Fact]
    public async Task Wrong_guesses_still_use_up_the_budget_and_the_one_that_locks_it_reads_like_the_rest()
    {
        var challenge = Issue();
        Outstanding(challenge);
        var service = Service();

        var answers = new List<Error>();
        for (var attempt = 0; attempt < Policy.MaxVerificationAttempts; attempt++)
        {
            answers.Add((await service.VerifyForAnonymousCallerAsync(_member, OtpPurpose.SignIn, WrongCode)).Error);
        }

        answers.Should().AllSatisfy(error => error.Should().Be(OtpErrors.IncorrectCode));
        challenge.Status.Should().Be(OtpChallengeStatus.AttemptLimitReached, "the budget is what makes six digits safe");
        _challenges.Received(Policy.MaxVerificationAttempts).Update(challenge);

        // The member, typing the code at last, is told why it no longer works.
        (await service.VerifyForAnonymousCallerAsync(_member, OtpPurpose.SignIn, SentCode))
            .Error.Should().Be(OtpErrors.AttemptLimitReached);

        // And a stranger still hears only "incorrect".
        (await service.VerifyForAnonymousCallerAsync(_member, OtpPurpose.SignIn, WrongCode))
            .Error.Should().Be(OtpErrors.IncorrectCode);
    }

    [Fact]
    public async Task The_code_that_was_sent_signs_in()
    {
        var challenge = Issue();
        Outstanding(challenge);

        var result = await Service().VerifyForAnonymousCallerAsync(_member, OtpPurpose.SignIn, $" {SentCode} ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(challenge);
        challenge.Status.Should().Be(OtpChallengeStatus.Verified);
    }

    // ── Fixtures ────────────────────────────────────────────────────────────

    private OtpChallenge Issue() =>
        OtpChallenge.Issue(
            _member.Id,
            PhoneNumber.Create(IdentityTestData.ValidPhone),
            IdentityTestData.Hash(SentCode),
            OtpPurpose.SignIn,
            _now,
            Policy);

    private void Outstanding(OtpChallenge? challenge) =>
        _challenges
            .GetLatestAsync(_member.Id, Arg.Any<OtpPurpose>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(challenge));

    private OtpChallengeService Service()
    {
        var codes = Substitute.For<IOtpCodeGenerator>();
        codes.ComputeHash(Arg.Any<string>()).Returns(call => IdentityTestData.Hash(call.Arg<string>()));

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(_ => _now);

        return new OtpChallengeService(
            _challenges,
            codes,
            Substitute.For<IOtpDeliveryService>(),
            Substitute.For<IIdentityPolicyProvider>(),
            clock,
            NullLogger<OtpChallengeService>.Instance);
    }
}
