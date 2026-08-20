using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Domain.Preferences;

/// <summary>
/// The stated-preference store.
/// <para>
/// <b><see cref="FindAsync"/> returning null is the common case and must stay cheap.</b> Most
/// members never open the personalization settings screen, so most generations find nothing here —
/// and the policy layer treats absence as "every default", which is why there is no
/// <c>GetOrCreate</c> on the read path. Creating a row to answer a read would put one write per
/// member per first feed on the busiest path in the module.
/// </para>
/// </summary>
public interface IPersonalizationPreferenceRepository
{
    Task<PersonalizationPreference?> FindAsync(
        UserId userId, CancellationToken cancellationToken = default);

    /// <summary>The write path only. Creates the row the first time a member states anything.</summary>
    Task<PersonalizationPreference> GetOrCreateAsync(
        UserId userId, CancellationToken cancellationToken = default);

    void Update(PersonalizationPreference preference);

    /// <summary>Erasure. Unlike the profile, this is not derivable and deleting it loses a decision.</summary>
    Task<bool> DeleteForUserAsync(UserId userId, CancellationToken cancellationToken = default);
}
