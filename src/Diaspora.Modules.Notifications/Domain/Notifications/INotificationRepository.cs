using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Notifications.Domain.Notifications;

/// <summary>
/// The notification aggregate's persistence port.
/// <para>
/// Declared in the domain and implemented in <c>Persistence</c>, so the aggregate's collaborators
/// are expressed in the domain's own language and no handler ever holds a <c>DbContext</c>.
/// </para>
/// </summary>
public interface INotificationRepository
{
    Task<Notification?> GetAsync(NotificationId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a soft-deleted message.
    /// <para>
    /// Separate from <see cref="GetAsync"/> because the global query filter hides exactly the row an
    /// undelete needs — the trap B1.0.1 recorded and every module since has re-learned.
    /// </para>
    /// </summary>
    Task<Notification?> GetIncludingDeletedAsync(
        NotificationId id, CancellationToken cancellationToken = default);

    Task<CursorPagedResult<Notification>> SearchAsync(
        NotificationQueryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether an equivalent message already reached this member inside the window.
    /// <para>
    /// The de-duplication check. At-least-once delivery guarantees a handler sees the same event
    /// twice; without this, so does the member.
    /// </para>
    /// </summary>
    Task<bool> ExistsRecentlyAsync(
        UserId recipientUserId,
        string templateKey,
        string deduplicationKey,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);

    /// <summary>Everything a digest window holds, oldest first, already limited.</summary>
    Task<IReadOnlyList<Notification>> ListForDigestAsync(
        UserId recipientUserId,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        string? categoryKey,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Deliveries the dispatcher should act on now, across every member.</summary>
    Task<IReadOnlyList<Notification>> ListDueForDeliveryAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default);

    /// <summary>Messages the retention sweep should age out.</summary>
    Task<IReadOnlyList<Notification>> ListExpiredAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default);

    /// <summary>Everything still unread, for "mark all as read". Loaded in batches by the caller.</summary>
    Task<IReadOnlyList<Notification>> ListUnreadAsync(
        UserId recipientUserId, int limit, CancellationToken cancellationToken = default);

    /// <summary>The measured counts the reconciliation sweep compares the counters against.</summary>
    Task<InboxCountSnapshot> CountForReconciliationAsync(
        UserId recipientUserId, CancellationToken cancellationToken = default);

    Task<int> CountPinnedAsync(UserId recipientUserId, CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(UserId recipientUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Notification>> ListByChannelStatusAsync(
        UserId recipientUserId,
        NotificationChannelKind channel,
        DeliveryStatus status,
        int limit,
        CancellationToken cancellationToken = default);

    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);

    void Update(Notification notification);
}
