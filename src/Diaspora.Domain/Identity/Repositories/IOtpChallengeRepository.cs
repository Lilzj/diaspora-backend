using Diaspora.Domain.Identity.Verification;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Domain.Identity.Repositories;

/// <summary>
/// Persistence contract for the <see cref="OtpChallenge"/> aggregate.
/// </summary>
public interface IOtpChallengeRepository
{
    Task<OtpChallenge?> GetByIdAsync(OtpChallengeId challengeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The newest challenge for a member and purpose, whatever its status. Callers
    /// inspect the status themselves so an exhausted or superseded challenge produces
    /// a precise error rather than looking like "no code was ever sent".
    /// </summary>
    Task<OtpChallenge?> GetLatestAsync(
        UserId userId,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every still-pending challenge for a member and purpose, so a newly issued code
    /// can supersede the ones it replaces.
    /// </summary>
    Task<IReadOnlyList<OtpChallenge>> ListPendingAsync(
        UserId userId,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How many challenges were issued to a member for a purpose since a cut-off.
    /// Backs the abuse throttle on the recovery endpoints.
    /// </summary>
    Task<int> CountIssuedSinceAsync(
        UserId userId,
        OtpPurpose purpose,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);

    Task AddAsync(OtpChallenge challenge, CancellationToken cancellationToken = default);

    void Update(OtpChallenge challenge);
}
