using Diaspora.Domain.Identity.Events;
using Diaspora.Domain.Identity.Policies;
using Diaspora.Domain.Identity.Verification;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.ValueObjects;
using Diaspora.UnitTests.Identity.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Identity.Domain;

public class OtpChallengeTests
{
    private static readonly OtpPolicy Policy = OtpPolicy.Create(
        TimeSpan.FromMinutes(10),
        maxVerificationAttempts: 3,
        maxResends: 2,
        resendCooldown: TimeSpan.FromSeconds(60));

    private static OtpChallenge Issue(DateTimeOffset? now = null) =>
        OtpChallenge.Issue(
            UserId.New(),
            PhoneNumber.Create(IdentityTestData.ValidPhone),
            IdentityTestData.Hash("123456"),
            OtpPurpose.PhoneVerification,
            now ?? IdentityTestData.Now,
            Policy);

    [Fact]
    public void A_new_challenge_is_pending_and_redeemable()
    {
        var challenge = Issue();

        challenge.Status.Should().Be(OtpChallengeStatus.Pending);
        challenge.IsRedeemableAt(IdentityTestData.Now).Should().BeTrue();
        challenge.AttemptsRemaining.Should().Be(3);
        challenge.ResendsRemaining.Should().Be(2);
        challenge.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<OtpChallengeIssuedDomainEvent>();
    }

    [Fact]
    public void The_correct_code_verifies()
    {
        var challenge = Issue();

        var result = challenge.Verify(IdentityTestData.Hash("123456"), IdentityTestData.Now);

        result.IsSuccess.Should().BeTrue();
        challenge.Status.Should().Be(OtpChallengeStatus.Verified);
        challenge.VerifiedAt.Should().Be(IdentityTestData.Now);
    }

    [Fact]
    public void A_wrong_code_costs_an_attempt()
    {
        var challenge = Issue();

        var result = challenge.Verify(IdentityTestData.Hash("000000"), IdentityTestData.Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.Otp.IncorrectCode");
        challenge.AttemptCount.Should().Be(1);
        challenge.AttemptsRemaining.Should().Be(2);
    }

    [Fact]
    public void Exhausting_the_attempt_budget_retires_the_challenge()
    {
        // What makes a six-digit code safe: the budget runs out long before the search
        // space does.
        var challenge = Issue();

        for (var i = 0; i < 3; i++)
        {
            challenge.Verify(IdentityTestData.Hash("000000"), IdentityTestData.Now);
        }

        challenge.Status.Should().Be(OtpChallengeStatus.AttemptLimitReached);
    }

    [Fact]
    public void The_correct_code_is_refused_once_the_budget_is_spent()
    {
        var challenge = Issue();

        for (var i = 0; i < 3; i++)
        {
            challenge.Verify(IdentityTestData.Hash("000000"), IdentityTestData.Now);
        }

        var result = challenge.Verify(IdentityTestData.Hash("123456"), IdentityTestData.Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.Otp.AttemptLimitReached");
    }

    [Fact]
    public void A_failed_attempt_raises_the_failure_event()
    {
        var challenge = Issue();
        challenge.ClearDomainEvents();

        challenge.Verify(IdentityTestData.Hash("000000"), IdentityTestData.Now);

        challenge.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<OtpVerificationFailedDomainEvent>();
    }

    [Fact]
    public void An_expired_code_is_refused()
    {
        var challenge = Issue();

        var result = challenge.Verify(
            IdentityTestData.Hash("123456"), IdentityTestData.Now.AddMinutes(11));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.Otp.Expired");
    }

    [Fact]
    public void A_code_cannot_be_redeemed_twice()
    {
        var challenge = Issue();
        challenge.Verify(IdentityTestData.Hash("123456"), IdentityTestData.Now);

        var second = challenge.Verify(IdentityTestData.Hash("123456"), IdentityTestData.Now);

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("Identity.Otp.AlreadyUsed");
    }

    [Fact]
    public void Resending_before_the_cooldown_elapses_is_refused()
    {
        var challenge = Issue();

        var result = challenge.Resend(
            IdentityTestData.Hash("654321"), IdentityTestData.Now.AddSeconds(30), Policy);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.Otp.ResendTooSoon");
    }

    [Fact]
    public void Resending_after_the_cooldown_replaces_the_code_and_resets_attempts()
    {
        var challenge = Issue();
        challenge.Verify(IdentityTestData.Hash("000000"), IdentityTestData.Now);

        var later = IdentityTestData.Now.AddSeconds(61);
        var result = challenge.Resend(IdentityTestData.Hash("654321"), later, Policy);

        result.IsSuccess.Should().BeTrue();
        challenge.AttemptCount.Should().Be(0);
        challenge.ResendCount.Should().Be(1);
        challenge.Verify(IdentityTestData.Hash("654321"), later).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void The_superseded_code_stops_working_after_a_resend()
    {
        var challenge = Issue();
        var later = IdentityTestData.Now.AddSeconds(61);

        challenge.Resend(IdentityTestData.Hash("654321"), later, Policy);

        challenge.Verify(IdentityTestData.Hash("123456"), later).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void The_resend_budget_is_finite()
    {
        var challenge = Issue();
        var moment = IdentityTestData.Now;

        for (var i = 1; i <= 2; i++)
        {
            moment = moment.AddSeconds(61);
            challenge.Resend(IdentityTestData.Hash($"code-{i}"), moment, Policy).IsSuccess.Should().BeTrue();
        }

        moment = moment.AddSeconds(61);
        var beyondBudget = challenge.Resend(IdentityTestData.Hash("code-3"), moment, Policy);

        beyondBudget.IsFailure.Should().BeTrue();
        beyondBudget.Error.Code.Should().Be("Identity.Otp.ResendLimitReached");
    }

    [Fact]
    public void A_verified_challenge_cannot_be_resent()
    {
        var challenge = Issue();
        challenge.Verify(IdentityTestData.Hash("123456"), IdentityTestData.Now);

        var result = challenge.Resend(
            IdentityTestData.Hash("654321"), IdentityTestData.Now.AddMinutes(5), Policy);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.Otp.AlreadyUsed");
    }

    [Fact]
    public void Superseding_retires_a_pending_challenge()
    {
        var challenge = Issue();

        challenge.Supersede();

        challenge.Status.Should().Be(OtpChallengeStatus.Superseded);
        challenge.Verify(IdentityTestData.Hash("123456"), IdentityTestData.Now)
            .Error.Code.Should().Be("Identity.Otp.Superseded");
    }

    [Fact]
    public void Superseding_does_not_undo_a_completed_verification()
    {
        var challenge = Issue();
        challenge.Verify(IdentityTestData.Hash("123456"), IdentityTestData.Now);

        challenge.Supersede();

        challenge.Status.Should().Be(OtpChallengeStatus.Verified);
    }

    [Fact]
    public void A_code_minted_for_one_purpose_is_recorded_against_that_purpose()
    {
        var challenge = OtpChallenge.Issue(
            UserId.New(),
            PhoneNumber.Create(IdentityTestData.ValidPhone),
            IdentityTestData.Hash("123456"),
            OtpPurpose.PasswordReset,
            IdentityTestData.Now,
            Policy);

        challenge.Purpose.Should().Be(OtpPurpose.PasswordReset);
    }
}
