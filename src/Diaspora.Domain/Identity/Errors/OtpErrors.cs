using Diaspora.SharedKernel.Results;

namespace Diaspora.Domain.Identity.Errors;

/// <summary>
/// Typed failures raised by the <c>OtpChallenge</c> aggregate.
/// </summary>
public static class OtpErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Identity.Otp.NotFound", "No verification code is outstanding. Request a new one.");

    public static readonly Error Expired =
        Error.Validation("Identity.Otp.Expired", "The verification code has expired. Request a new one.");

    public static readonly Error AlreadyUsed =
        Error.Conflict("Identity.Otp.AlreadyUsed", "The verification code has already been used.");

    public static readonly Error Superseded =
        Error.Conflict("Identity.Otp.Superseded", "A newer verification code was issued. Use the most recent one.");

    public static readonly Error IncorrectCode =
        Error.Validation("Identity.Otp.IncorrectCode", "The verification code is incorrect.");

    public static readonly Error AttemptLimitReached =
        Error.RateLimited(
            "Identity.Otp.AttemptLimitReached",
            "Too many incorrect attempts. Request a new verification code.");

    public static readonly Error ResendLimitReached =
        Error.RateLimited(
            "Identity.Otp.ResendLimitReached",
            "The maximum number of verification codes has been requested. Try again later.");

    public static Error ResendTooSoon(TimeSpan retryAfter) =>
        Error.RateLimited(
            "Identity.Otp.ResendTooSoon",
            $"A verification code was sent recently. Try again in {Math.Ceiling(retryAfter.TotalSeconds)} seconds.");
}
