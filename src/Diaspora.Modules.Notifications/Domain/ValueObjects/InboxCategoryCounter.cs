using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.ValueObjects;

/// <summary>
/// One category's unread count for one member.
/// <para>
/// <b>Maintained rather than computed, and that is the single most important performance decision in
/// the module.</b> The unread badge is read on every app foreground by every member; computing it as
/// <c>COUNT(*) WHERE recipient = ? AND status IN (...)</c> against a table holding millions of rows
/// is a full index scan per member per launch. A counter costs one indexed row read, and the write
/// it costs is already inside the transaction that created the notification.
/// </para>
/// <para>
/// The price is that a counter can drift if a write path forgets to settle it. That is why every
/// mutation goes through <c>UserInbox</c> rather than touching the notification alone, and why the
/// counters clamp at zero rather than going negative — a visible zero is recoverable, a negative
/// badge is a bug report.
/// </para>
/// </summary>
public sealed class InboxCategoryCounter : ValueObject
{
    private InboxCategoryCounter(Guid counterId, string categoryKey, int unreadCount, int totalCount)
    {
        CounterId = counterId;
        CategoryKey = categoryKey;
        UnreadCount = unreadCount;
        TotalCount = totalCount;
    }

    private InboxCategoryCounter() { }

    /// <summary>Caller-supplied key. The configuration declares <c>ValueGeneratedNever</c>.</summary>
    public Guid CounterId { get; private set; }

    public string CategoryKey { get; private set; } = string.Empty;

    public int UnreadCount { get; private set; }

    /// <summary>Everything ever received in this category, read or not. The denominator.</summary>
    public int TotalCount { get; private set; }

    public static InboxCategoryCounter Create(Guid counterId, string categoryKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryKey);

        return new InboxCategoryCounter(counterId, categoryKey.Trim().ToLowerInvariant(), 0, 0);
    }

    /// <summary>Moves both counters. Clamped at zero — see the class summary.</summary>
    public void Adjust(int unreadDelta, int totalDelta)
    {
        UnreadCount = Math.Max(0, UnreadCount + unreadDelta);
        TotalCount = Math.Max(0, TotalCount + totalDelta);
    }

    /// <summary>Sets the unread count outright. The repair path the reconciliation sweep uses.</summary>
    public void SetUnread(int value) => UnreadCount = Math.Max(0, value);

    /// <summary>Sets the total outright. The repair path the reconciliation sweep uses.</summary>
    public void SetTotal(int value) => TotalCount = Math.Max(0, value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CounterId;
        yield return CategoryKey;
        yield return UnreadCount;
        yield return TotalCount;
    }
}
