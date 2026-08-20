using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Domain.Profiles;

/// <summary>
/// The profile store.
/// <para>
/// <b><see cref="GetOrCreateAsync"/> checks the change tracker before the database, and that is
/// correctness rather than an optimisation.</b> One unit of work routinely folds several signals for
/// one member — a batch of client impressions, an event that produces three observations — and each
/// asks for the profile. Without the local lookup the second call would query, find nothing (the
/// first is added but unsaved), and add a second profile row that the unique index rejects at save
/// time, failing the whole batch. B13's inbox repository records the identical trap.
/// </para>
/// </summary>
public interface IPersonalizationProfileRepository
{
    Task<PersonalizationProfile?> FindAsync(
        UserId userId, CancellationToken cancellationToken = default);

    /// <summary>The profile, created on first sight. See the class summary.</summary>
    Task<PersonalizationProfile> GetOrCreateAsync(
        UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Profiles that have not been built recently, least recently first.
    /// <para>
    /// Ordered so the sweep works through the platform rather than repeatedly re-checking whichever
    /// rows the database happened to return first.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<PersonalizationProfile>> ListStaleAsync(
        DateTimeOffset staleBefore, int limit, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>The mean completeness across the platform. One number, for the analytics surface.</summary>
    Task<decimal> AverageCompletenessAsync(CancellationToken cancellationToken = default);

    void Update(PersonalizationProfile profile);

    /// <summary>Erasure. The profile is derived, so this is a convenience rather than a duty.</summary>
    Task<bool> DeleteForUserAsync(UserId userId, CancellationToken cancellationToken = default);
}
