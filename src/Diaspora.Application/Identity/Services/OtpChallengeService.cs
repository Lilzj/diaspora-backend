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
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Failure<OtpChallenge>(OtpErrors.IncorrectCode);
        }

        var challenge = await _challenges.GetLatestAsync(user.Id, purpose, cancellationToken);
        if (challenge is null)
        {
            return Result.Failure<OtpChallenge>(OtpErrors.NotFound);
        }

        var verification = challenge.Verify(_codeGenerator.ComputeHash(code.Trim()), _clock.UtcNow);

        // Persisted either way: a failed attempt must cost the attacker one of their
        // attempts, so the counter increment has to survive the failed request.
        _challenges.Update(challenge);

        return verification.IsFailure
            ? Result.Failure<OtpChallenge>(verification.Error)
            : Result.Success(challenge);
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
}
