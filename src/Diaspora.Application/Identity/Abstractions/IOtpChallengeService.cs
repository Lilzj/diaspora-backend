using Diaspora.Domain.Identity.Users;
using Diaspora.Domain.Identity.Verification;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// Issues, resends and redeems one-time codes.
/// <para>
/// Registration, resend, forgot-password and reset all need the same careful sequence:
/// supersede outstanding codes, generate, hash, persist, deliver — and unwind if delivery
/// fails. Centralising it means the retry budget and cooldown cannot be enforced in one
/// flow and forgotten in another.
/// </para>
/// </summary>
public interface IOtpChallengeService
{
    /// <summary>
    /// Issues a fresh challenge, superseding any outstanding one for the same purpose.
    /// </summary>
    Task<Result<OtpChallenge>> IssueAsync(
        User user,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resends the outstanding challenge, subject to its cooldown and resend budget.
    /// Issues a new challenge if none is outstanding.
    /// </summary>
    Task<Result<OtpChallenge>> ResendAsync(
        User user,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Redeems a code. Consumes an attempt on failure, which is what makes a short
    /// numeric code safe.
    /// </summary>
    Task<Result<OtpChallenge>> VerifyAsync(
        User user,
        OtpPurpose purpose,
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Redeems a code for a caller nobody has identified yet, such as the anonymous sign-in route.
    /// <para>
    /// Attempts are counted exactly as in <see cref="VerifyAsync"/>. The difference is what the answer
    /// says. Anyone can post any code against any registered number, so a challenge's state (none
    /// outstanding, already used, expired, locked) is disclosed only to a caller whose code matches
    /// it. Everyone else is told the code is incorrect, which is what an unregistered number is told.
    /// </para>
    /// </summary>
    Task<Result<OtpChallenge>> VerifyForAnonymousCallerAsync(
        User user,
        OtpPurpose purpose,
        string code,
        CancellationToken cancellationToken = default);
}
