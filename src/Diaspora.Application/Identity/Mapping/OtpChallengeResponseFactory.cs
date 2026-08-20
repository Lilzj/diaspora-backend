using Diaspora.Application.Identity.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Verification;

namespace Diaspora.Application.Identity.Mapping;

/// <summary>
/// Projects an OTP challenge for the client: enough to drive a countdown and a resend
/// button, and nothing that would help someone guess the code.
/// </summary>
public static class OtpChallengeResponseFactory
{
    /// <summary>
    /// Cooldown used when rendering the resend timer. Kept in step with the OTP policy
    /// through <see cref="IIdentityPolicyProvider"/> at the call sites that have it.
    /// </summary>
    public static OtpChallengeResponse Create(
        OtpChallenge challenge,
        DateTimeOffset now,
        TimeSpan? resendCooldown = null)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        var cooldown = resendCooldown ?? TimeSpan.FromSeconds(60);
        var nextResendAt = challenge.LastSentAt.Add(cooldown);
        var waitSeconds = nextResendAt > now ? (int)Math.Ceiling((nextResendAt - now).TotalSeconds) : 0;

        return new OtpChallengeResponse
        {
            PhoneNumber = challenge.PhoneNumber.Masked,
            Purpose = challenge.Purpose.ToString(),
            ExpiresAt = challenge.ExpiresAt,
            AttemptsRemaining = challenge.AttemptsRemaining,
            ResendsRemaining = challenge.ResendsRemaining,
            ResendAvailableInSeconds = waitSeconds
        };
    }
}
