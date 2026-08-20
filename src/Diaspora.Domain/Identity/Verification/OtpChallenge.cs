using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Events;
using Diaspora.Domain.Identity.Policies;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;
using Diaspora.SharedKernel.ValueObjects;

namespace Diaspora.Domain.Identity.Verification;

/// <summary>
/// A one-time code sent to a member's phone.
/// <para>
/// One aggregate serves phone verification, password reset and account recovery.
/// They differ only in <see cref="OtpPurpose"/>, and collapsing them means expiry,
/// retry budgets, resend cooldowns and abuse lockout are implemented — and tested —
/// exactly once. This is also the "recovery token" the recovery flows hand out.
/// </para>
/// <para>
/// Only the hash of the code is stored, and comparison is fixed-time.
/// </para>
/// </summary>
public sealed class OtpChallenge : AggregateRoot<OtpChallengeId>, IAuditableEntity
{
    public UserId UserId { get; private set; } = default!;
    public PhoneNumber PhoneNumber { get; private set; } = default!;
    public SecretHash CodeHash { get; private set; } = default!;
    public OtpPurpose Purpose { get; private set; }
    public OtpChallengeStatus Status { get; private set; }

    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public DateTimeOffset LastSentAt { get; private set; }

    public int AttemptCount { get; private set; }
    public int MaxAttempts { get; private set; }
    public int ResendCount { get; private set; }
    public int MaxResends { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    private OtpChallenge() { }

    private OtpChallenge(
        OtpChallengeId id,
        UserId userId,
        PhoneNumber phoneNumber,
        SecretHash codeHash,
        OtpPurpose purpose,
        DateTimeOffset now,
        OtpPolicy policy) : base(id)
    {
        UserId = userId;
        PhoneNumber = phoneNumber;
        CodeHash = codeHash;
        Purpose = purpose;
        Status = OtpChallengeStatus.Pending;
        IssuedAt = now;
        LastSentAt = now;
        ExpiresAt = now.Add(policy.Lifetime);
        MaxAttempts = policy.MaxVerificationAttempts;
        MaxResends = policy.MaxResends;
    }

    public static OtpChallenge Issue(
        UserId userId,
        PhoneNumber phoneNumber,
        SecretHash codeHash,
        OtpPurpose purpose,
        DateTimeOffset now,
        OtpPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(phoneNumber);
        ArgumentNullException.ThrowIfNull(codeHash);
        ArgumentNullException.ThrowIfNull(policy);

        var challenge = new OtpChallenge(
            OtpChallengeId.New(), userId, phoneNumber, codeHash, purpose, now, policy);

        challenge.RaiseDomainEvent(new OtpChallengeIssuedDomainEvent(
            challenge.Id, userId, phoneNumber.Value, purpose, 0, challenge.ExpiresAt));

        return challenge;
    }

    public bool IsExpiredAt(DateTimeOffset now) => ExpiresAt <= now;

    public bool IsRedeemableAt(DateTimeOffset now) =>
        Status == OtpChallengeStatus.Pending && !IsExpiredAt(now);

    public int AttemptsRemaining => Math.Max(0, MaxAttempts - AttemptCount);

    public int ResendsRemaining => Math.Max(0, MaxResends - ResendCount);

    /// <summary>
    /// Redeems the challenge against a candidate code hash. Every wrong answer costs
    /// an attempt; exhausting the budget retires the challenge so a new code must be
    /// requested, which is what stops a six-digit code from being brute-forced.
    /// </summary>
    public Result Verify(SecretHash candidateHash, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(candidateHash);

        switch (Status)
        {
            case OtpChallengeStatus.Verified:
                return Result.Failure(OtpErrors.AlreadyUsed);
            case OtpChallengeStatus.Superseded:
                return Result.Failure(OtpErrors.Superseded);
            case OtpChallengeStatus.AttemptLimitReached:
                return Result.Failure(OtpErrors.AttemptLimitReached);
        }

        if (IsExpiredAt(now))
        {
            return Result.Failure(OtpErrors.Expired);
        }

        if (!CodeHash.Matches(candidateHash))
        {
            AttemptCount++;
            var limitReached = AttemptCount >= MaxAttempts;
            if (limitReached)
            {
                Status = OtpChallengeStatus.AttemptLimitReached;
            }

            RaiseDomainEvent(new OtpVerificationFailedDomainEvent(
                Id, UserId, Purpose, AttemptCount, limitReached, now));

            return Result.Failure(limitReached ? OtpErrors.AttemptLimitReached : OtpErrors.IncorrectCode);
        }

        Status = OtpChallengeStatus.Verified;
        VerifiedAt = now;
        return Result.Success();
    }

    /// <summary>
    /// Replaces the outstanding code with a new one, subject to the cooldown and the
    /// resend budget. The attempt counter resets — the member is proving they hold the
    /// phone, not retrying a guess.
    /// </summary>
    public Result Resend(SecretHash newCodeHash, DateTimeOffset now, OtpPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(newCodeHash);
        ArgumentNullException.ThrowIfNull(policy);

        if (Status == OtpChallengeStatus.Verified)
        {
            return Result.Failure(OtpErrors.AlreadyUsed);
        }

        if (Status == OtpChallengeStatus.Superseded)
        {
            return Result.Failure(OtpErrors.Superseded);
        }

        if (ResendCount >= MaxResends)
        {
            return Result.Failure(OtpErrors.ResendLimitReached);
        }

        var earliestResend = LastSentAt.Add(policy.ResendCooldown);
        if (now < earliestResend)
        {
            return Result.Failure(OtpErrors.ResendTooSoon(earliestResend - now));
        }

        CodeHash = newCodeHash;
        Status = OtpChallengeStatus.Pending;
        ResendCount++;
        AttemptCount = 0;
        LastSentAt = now;
        ExpiresAt = now.Add(policy.Lifetime);

        RaiseDomainEvent(new OtpChallengeIssuedDomainEvent(
            Id, UserId, PhoneNumber.Value, Purpose, ResendCount, ExpiresAt));

        return Result.Success();
    }

    /// <summary>
    /// Retires this challenge because a newer one was issued for the same purpose.
    /// </summary>
    public void Supersede()
    {
        if (Status == OtpChallengeStatus.Pending)
        {
            Status = OtpChallengeStatus.Superseded;
        }
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
