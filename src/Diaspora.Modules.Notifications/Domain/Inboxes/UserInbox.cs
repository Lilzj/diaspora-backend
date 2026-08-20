using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Inboxes;

/// <summary>
/// One member's counters, and nothing else.
/// <para>
/// <b>This aggregate exists for one query, and that query is the busiest in the platform.</b> Every
/// app launch by every member asks "how many unread". Answering it with
/// <c>COUNT(*) WHERE recipient = ? AND status IN (...)</c> is an index scan whose cost grows with a
/// member's history — so the members who have been here longest get the slowest launch, which is
/// exactly backwards. One row, read by primary key, is constant.
/// </para>
/// <para>
/// <b>It holds no messages.</b> The name is a concession to the domain language the client already
/// speaks; the aggregate is a counter set. Putting the notifications inside it would mean loading
/// two years of a member's history to mark one message read, and would serialise every write for
/// that member behind a single row.
/// </para>
/// <para>
/// <b>The counters can drift, and the design accepts that.</b> A maintained counter is only correct
/// if every write path settles it, which is why every mutation the module makes goes through this
/// aggregate in the same transaction as the notification. The reconciliation sweep repairs whatever
/// still slipped, and the counters clamp at zero so a drift shows as a stale badge rather than a
/// negative one.
/// </para>
/// </summary>
public sealed class UserInbox : AggregateRoot<UserInboxId>, IAuditableEntity
{
    private readonly List<InboxCategoryCounter> _categories = [];

    private UserInbox(UserInboxId id, UserId userId, DateTimeOffset createdAt) : base(id)
    {
        UserId = userId;
        CreatedAt = createdAt;
    }

    private UserInbox() { }

    public UserId UserId { get; private set; } = default!;

    /// <summary>Everything not yet read, across every category. The badge.</summary>
    public int UnreadCount { get; private set; }

    /// <summary>Everything in the inbox, read or not. Excludes archived and deleted.</summary>
    public int TotalCount { get; private set; }

    public int ArchivedCount { get; private set; }

    public int PinnedCount { get; private set; }

    public int StarredCount { get; private set; }

    /// <summary>
    /// When the member last opened the inbox screen.
    /// <para>
    /// Distinct from a per-message read stamp: it answers "is there anything since they last
    /// looked", which is what a subtle dot on a tab bar needs and an unread count cannot give.
    /// </para>
    /// </summary>
    public DateTimeOffset? LastViewedAt { get; private set; }

    /// <summary>When anything last arrived. Drives "nothing new" without a query.</summary>
    public DateTimeOffset? LastNotificationAt { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public IReadOnlyList<InboxCategoryCounter> Categories => _categories.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static UserInbox Open(UserInboxId id, UserId userId, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(userId);

        return new UserInbox(id, userId, createdAt);
    }

    public int UnreadFor(string categoryKey) =>
        FindCounter(categoryKey)?.UnreadCount ?? 0;

    /// <summary>A message arrived. Called inside the transaction that created it.</summary>
    public void RecordArrival(string categoryKey, Guid counterId, DateTimeOffset arrivedAt)
    {
        UnreadCount++;
        TotalCount++;
        LastNotificationAt = arrivedAt;

        EnsureCounter(categoryKey, counterId).Adjust(unreadDelta: 1, totalDelta: 1);

        Touch();
    }

    /// <summary>A message was read. Idempotent at the aggregate's edge — the caller must not call it twice.</summary>
    public void RecordRead(string categoryKey, Guid counterId)
    {
        UnreadCount = Math.Max(0, UnreadCount - 1);

        EnsureCounter(categoryKey, counterId).Adjust(unreadDelta: -1, totalDelta: 0);

        Touch();
    }

    /// <summary>A message was put back to unread.</summary>
    public void RecordUnread(string categoryKey, Guid counterId)
    {
        UnreadCount++;

        EnsureCounter(categoryKey, counterId).Adjust(unreadDelta: 1, totalDelta: 0);

        Touch();
    }

    /// <summary>
    /// A message left the inbox.
    /// <para>
    /// <paramref name="wasUnread"/> comes from the domain event rather than being read back, because
    /// by the time this runs the notification has already been changed and the fact is gone.
    /// </para>
    /// </summary>
    public void RecordArchived(string categoryKey, Guid counterId, bool wasUnread)
    {
        TotalCount = Math.Max(0, TotalCount - 1);
        ArchivedCount++;

        if (wasUnread)
        {
            UnreadCount = Math.Max(0, UnreadCount - 1);
        }

        EnsureCounter(categoryKey, counterId)
            .Adjust(unreadDelta: wasUnread ? -1 : 0, totalDelta: -1);

        Touch();
    }

    /// <summary>A filed message came back.</summary>
    public void RecordUnarchived(string categoryKey, Guid counterId, bool isUnread)
    {
        TotalCount++;
        ArchivedCount = Math.Max(0, ArchivedCount - 1);

        if (isUnread)
        {
            UnreadCount++;
        }

        EnsureCounter(categoryKey, counterId)
            .Adjust(unreadDelta: isUnread ? 1 : 0, totalDelta: 1);

        Touch();
    }

    /// <summary>A message was soft-deleted. Leaves the archived count alone: a deleted message is not filed.</summary>
    public void RecordDeleted(string categoryKey, Guid counterId, bool wasUnread)
    {
        TotalCount = Math.Max(0, TotalCount - 1);

        if (wasUnread)
        {
            UnreadCount = Math.Max(0, UnreadCount - 1);
        }

        EnsureCounter(categoryKey, counterId)
            .Adjust(unreadDelta: wasUnread ? -1 : 0, totalDelta: -1);

        Touch();
    }

    public void RecordPinned(int delta)
    {
        PinnedCount = Math.Max(0, PinnedCount + delta);
        Touch();
    }

    public void RecordStarred(int delta)
    {
        StarredCount = Math.Max(0, StarredCount + delta);
        Touch();
    }

    /// <summary>The member opened the inbox screen. Does not clear anything.</summary>
    public void RecordViewed(DateTimeOffset viewedAt)
    {
        LastViewedAt = viewedAt;
        Touch();
    }

    /// <summary>
    /// Everything read at once.
    /// <para>
    /// Sets rather than decrements. "Mark all as read" is the one operation where the counter and
    /// the rows are updated by separate statements, so deriving the new value from the old one would
    /// carry any existing drift forward forever.
    /// </para>
    /// </summary>
    public void RecordAllRead()
    {
        UnreadCount = 0;

        foreach (var counter in _categories)
        {
            counter.SetUnread(0);
        }

        Touch();
    }

    /// <summary>
    /// Replaces every counter with a measured truth.
    /// <para>
    /// The reconciliation sweep's entry point. Maintained counters drift — a crashed process between
    /// two saves, a write path added without a settlement — and the honest answer to that is a
    /// periodic recount rather than a promise that it cannot happen.
    /// </para>
    /// </summary>
    public void Reconcile(
        int unread,
        int total,
        int archived,
        int pinned,
        int starred,
        IReadOnlyDictionary<string, (int Unread, int Total)> byCategory,
        Func<string, Guid> counterIdFactory)
    {
        ArgumentNullException.ThrowIfNull(byCategory);
        ArgumentNullException.ThrowIfNull(counterIdFactory);

        UnreadCount = Math.Max(0, unread);
        TotalCount = Math.Max(0, total);
        ArchivedCount = Math.Max(0, archived);
        PinnedCount = Math.Max(0, pinned);
        StarredCount = Math.Max(0, starred);

        foreach (var counter in _categories)
        {
            counter.SetUnread(0);
            counter.SetTotal(0);
        }

        foreach (var (categoryKey, counts) in byCategory)
        {
            var counter = EnsureCounter(categoryKey, counterIdFactory(categoryKey));
            counter.SetUnread(counts.Unread);
            counter.SetTotal(counts.Total);
        }

        Touch();
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    private InboxCategoryCounter? FindCounter(string categoryKey)
    {
        var key = categoryKey.Trim().ToLowerInvariant();

        return _categories.Find(counter =>
            string.Equals(counter.CategoryKey, key, StringComparison.Ordinal));
    }

    /// <summary>
    /// The counter for a category, created on first use.
    /// <para>
    /// Created lazily rather than seeded for every catalogue entry, because a member who never
    /// receives a housing notification should not carry a housing row — and a new category shipping
    /// would otherwise mean a migration touching every inbox on the platform.
    /// </para>
    /// </summary>
    private InboxCategoryCounter EnsureCounter(string categoryKey, Guid counterId)
    {
        if (FindCounter(categoryKey) is { } existing)
        {
            return existing;
        }

        var counter = InboxCategoryCounter.Create(counterId, categoryKey);

        _categories.Add(counter);

        return counter;
    }

    private void Touch() => Revision++;
}
