using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Notifications.Domain.Digests;

/// <summary>The digest aggregate's persistence port.</summary>
public interface INotificationDigestRepository
{
    Task<NotificationDigest?> GetAsync(
        NotificationDigestId id, CancellationToken cancellationToken = default);

    Task<CursorPagedResult<NotificationDigest>> ListForUserAsync(
        UserId userId, string? cursor, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a digest already covers this window for this member.
    /// <para>
    /// The sweep runs hourly and a member's schedule fires on an hour; without this check a restart
    /// inside that hour would generate the same digest twice.
    /// </para>
    /// </summary>
    Task<bool> ExistsForWindowAsync(
        UserId userId,
        DigestFrequency frequency,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        string? categoryKey,
        CancellationToken cancellationToken = default);

    Task AddAsync(NotificationDigest digest, CancellationToken cancellationToken = default);

    void Update(NotificationDigest digest);
}
