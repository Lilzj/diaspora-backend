using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Domain.Preferences;

/// <summary>
/// The preference aggregate's persistence port.
/// <para>
/// <see cref="FindAsync"/> returns null for a member who has never opened the settings screen, and
/// the resolver treats that as the platform defaults. Creating a row for every account at
/// registration would be a write per member that never changes anything, and a migration for every
/// existing member whenever a default moves.
/// </para>
/// </summary>
public interface INotificationPreferenceRepository
{
    Task<NotificationPreference?> FindAsync(
        UserId userId, CancellationToken cancellationToken = default);

    Task<NotificationPreference> GetOrCreateAsync(
        UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Members whose digest schedule fires in the given UTC hour.
    /// <para>
    /// Selected by frequency and by the member's own local hour, which the query computes from the
    /// stored zone. Doing it the other way — loading every digest-enabled member and filtering in
    /// memory — is the shape that works until the platform has a hundred thousand of them.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<NotificationPreference>> ListDigestDueAsync(
        DateTimeOffset utcNow,
        DigestFrequency frequency,
        int limit,
        CancellationToken cancellationToken = default);

    Task AddAsync(NotificationPreference preference, CancellationToken cancellationToken = default);

    void Update(NotificationPreference preference);
}
