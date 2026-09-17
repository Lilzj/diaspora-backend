using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Users;
using Diaspora.Domain.Identity.Verification;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Application.Identity.Services;

/// <inheritdoc cref="IOtpChallengeService" />
public sealed class OtpChallengeService : IOtpChallengeService
{
    private readonly IOtpChallengeRepository _challenges;
    private readonly IOtpCodeGenerator _codeGenerator;
    private readonly IOtpDeliveryService _delivery;
    private readonly IIdentityPolicyProvider _policies;
    private readonly IClock _clock;
    private readonly ILogger<OtpChallengeService> _logger;

    public OtpChallengeService(
        IOtpChallengeRepository challenges,
        IOtpCodeGenerator codeGenerator,
        IOtpDeliveryService delivery,
        IIdentityPolicyProvider policies,
        IClock clock,
        ILogger<OtpChallengeService> logger)
    {
        _challenges = challenges;
        _codeGenerator = codeGenerator;
        _delivery = delivery;
        _policies = policies;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<OtpChallenge>> IssueAsync(
        User user,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var policies = _policies.Current;
        var now = _clock.UtcNow;

        var throttle = await EnsureWithinIssueBudgetAsync(user, purpose, now, cancellationToken);
        if (throttle.IsFailure)
        {
            return Result.Failure<OtpChallenge>(throttle.Error);
        }

        await SupersedeOutstandingAsync(user, purpose, cancellationToken);

        var code = _codeGenerator.Generate();
        var challenge = OtpChallenge.Issue(
            user.Id,
            user.PhoneNumber,
            _codeGenerator.ComputeHash(code),
            purpose,
            now,
            policies.Otp);

        await _challenges.AddAsync(challenge, cancellationToken);

        var delivered = await DeliverAsync(user, challenge, code, cancellationToken);
        if (!delivered)
        {
            return Result.Failure<OtpChallenge>(Error.ServiceUnavailable(
                "Identity.Otp.DeliveryFailed",
                "We could not send a verification code right now. Please try again shortly."));
        }

        return Result.Success(challenge);
    }

    public async Task<Result<OtpChallenge>> ResendAsync(
        User user,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var policies = _policies.Current;
        var now = _clock.UtcNow;

        var existing = await _challenges.GetLatestAsync(user.Id, purpose, cancellationToken);

        // Nothing outstanding, or the last one is spent — a resend is really a fresh issue.
        if (existing is null ||
            existing.Status is OtpChallengeStatus.Verified or OtpChallengeStatus.Superseded ||
            existing.IsExpiredAt(now))
        {
            return await IssueAsync(user, purpose, cancellationToken);
        }

        var throttle = await EnsureWithinIssueBudgetAsync(user, purpose, now, cancellationToken);
        if (throttle.IsFailure)
        {
            return Result.Failure<OtpChallenge>(throttle.Error);
        }

        var code = _codeGenerator.Generate();
        var resend = existing.Resend(_codeGenerator.ComputeHash(code), now, policies.Otp);
        if (resend.IsFailure)
        {
            return Result.Failure<OtpChallenge>(resend.Error);
        }

        _challenges.Update(existing);

        var delivered = await DeliverAsync(user, existing, code, cancellationToken);
        if (!delivered)
        {
            return Result.Failure<OtpChallenge>(Error.ServiceUnavailable(
                "Identity.Otp.DeliveryFailed",
                "We could not send a verification code right now. Please try again shortly."));
        }

        return Result.Success(existing);
    }

    public async Task<Result<OtpChallenge>> VerifyAsync(
        User user,
        OtpPurpose purpose,
        string code,
        CancellationToken cancellationToken = default)
    {
        var redemption = await RedeemAsync(user, purpose, code, cancellationToken);

        return redemption.Verification.IsFailure
            ? Result.Failure<OtpChallenge>(redemption.Verification.Error)
            : Result.Success(redemption.Challenge!);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Found on 17 Sep 2026. The sign-in route passed a challenge's state to whoever asked, so a
    /// registered number with no sign-in code outstanding answered 404 "No verification code is
    /// outstanding", while an unregistered number answered 400 "incorrect". A member who had once
    /// signed in with a code answered 409 "already used" to any code at all. Nobody had to request a
    /// code, or send the member a text, to learn the number had an account.
    /// </remarks>
    public async Task<Result<OtpChallenge>> VerifyForAnonymousCallerAsync(
        User user,
        OtpPurpose purpose,
        string code,
        CancellationToken cancellationToken = default)
    {
        var redemption = await RedeemAsync(user, purpose, code, cancellationToken);

        if (redemption.Verification.IsSuccess)
        {
            return Result.Success(redemption.Challenge!);
        }

        // Only the holder of the code learns why it did not work. That includes the attempt that
        // locks the challenge: a wrong guess that uses up the budget is still just a wrong guess to
        // the caller who made it, and the holder hears "too many attempts" when they type the code.
        return Result.Failure<OtpChallenge>(
            redemption.CallerHoldsTheCode ? redemption.Verification.Error : OtpErrors.IncorrectCode);
    }

    private async Task<Redemption> RedeemAsync(
        User user,
        OtpPurpose purpose,
        string code,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(code))
        {
            return new Redemption(null, Result.Failure(OtpErrors.IncorrectCode), CallerHoldsTheCode: false);
        }

        var challenge = await _challenges.GetLatestAsync(user.Id, purpose, cancellationToken);
        if (challenge is null)
        {
            return new Redemption(null, Result.Failure(OtpErrors.NotFound), CallerHoldsTheCode: false);
        }

        var candidate = _codeGenerator.ComputeHash(code.Trim());

        // Compared before the challenge is redeemed, although redeeming never changes the hash:
        // the answer depends on the code the caller typed, not on the state the attempt left behind.
        var holdsTheCode = challenge.CodeHash.Matches(candidate);
        var verification = challenge.Verify(candidate, _clock.UtcNow);

        // Persisted either way: a failed attempt must cost the attacker one of their
        // attempts, so the counter increment has to survive the failed request.
        _challenges.Update(challenge);

        return new Redemption(challenge, verification, holdsTheCode);
    }

    /// <summary>
    /// Caps how many codes one member can trigger inside the throttle window, so the
    /// endpoints cannot be turned into a free SMS pump aimed at someone else's phone.
    /// </summary>
    private async Task<Result> EnsureWithinIssueBudgetAsync(
        User user,
        OtpPurpose purpose,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var policies = _policies.Current;

        var issued = await _challenges.CountIssuedSinceAsync(
            user.Id, purpose, now - policies.RecoveryThrottleWindow, cancellationToken);

        if (issued >= policies.MaxRecoveryRequestsPerWindow)
        {
            _logger.LogWarning(
                "OTP issue budget exhausted for user {UserId} and purpose {Purpose}: {Issued} in {Window}",
                user.Id, purpose, issued, policies.RecoveryThrottleWindow);

            return Result.Failure(OtpErrors.ResendLimitReached);
        }

        return Result.Success();
    }

    private async Task SupersedeOutstandingAsync(
        User user,
        OtpPurpose purpose,
        CancellationToken cancellationToken)
    {
        var pending = await _challenges.ListPendingAsync(user.Id, purpose, cancellationToken);

        foreach (var challenge in pending)
        {
            challenge.Supersede();
            _challenges.Update(challenge);
        }
    }

    private async Task<bool> DeliverAsync(
        User user,
        OtpChallenge challenge,
        string code,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _delivery.SendAsync(
                new OtpDeliveryRequest
                {
                    PhoneNumber = user.PhoneNumber.Value,

                    // Both contact points, every time. Which one is used is the transport's
                    // decision, not this service's: it is the layer that knows what the
                    // deployment has configured.
                    EmailAddress = user.Email?.Value,
                    Code = code,
                    Purpose = challenge.Purpose,
                    Language = user.PreferredLanguage.Value,
                    ExpiresAt = challenge.ExpiresAt
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Never let a gateway fault surface as a stack trace to the caller, and never
            // log the code itself.
            _logger.LogError(ex,
                "Failed to deliver a {Purpose} code to user {UserId}", challenge.Purpose, user.Id);
            return false;
        }
    }

    /// <summary>What one attempt at a code did, and whether the code typed was the one sent.</summary>
    private readonly record struct Redemption(
        OtpChallenge? Challenge,
        Result Verification,
        bool CallerHoldsTheCode);
}
