using Diaspora.Modules.Recommendations.Domain.Profiles;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Extensibility.Personalization;

/// <summary>
/// Turns signals into a profile.
/// <para>
/// <b>Two methods because incremental and full builds are genuinely different operations, not one
/// with a flag.</b> An incremental build folds the unprocessed window into what is already there and
/// runs on every signal batch; a rebuild discards every inference and recomputes from the whole
/// retained history, and is expensive enough to be rate-limited. Merging them would mean either a
/// rebuild that skipped the reset — so a stale inference could never be cleared — or an incremental
/// build that recomputed everything, which would put a full history scan on the write path.
/// </para>
/// <para>
/// <b>Replaceable, and this is where a machine-learned profile would land.</b> Interests, affinities
/// and activity summaries are a deterministic derivation today; an adapter could derive an embedding
/// alongside them without the strategies above noticing, because they read the profile rather than
/// the derivation.
/// </para>
/// </summary>
public interface IPersonalizationProfileBuilder
{
    /// <summary>
    /// Folds a member's unprocessed signals into their profile.
    /// <para>
    /// Idempotent by construction: a signal is marked processed as it is folded, so a repeated call
    /// finds nothing to do. Returns the profile whether or not anything changed.
    /// </para>
    /// </summary>
    Task<PersonalizationProfile> BuildIncrementalAsync(
        UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recomputes a profile from the member's whole retained signal history.
    /// <para>
    /// Discards every inference and keeps everything the member stated. See
    /// <c>PersonalizationProfile.BeginRebuild</c> for why that distinction is enforced by the
    /// aggregate rather than by this method.
    /// </para>
    /// </summary>
    Task<PersonalizationProfile> RebuildAsync(
        UserId userId, CancellationToken cancellationToken = default);
}
