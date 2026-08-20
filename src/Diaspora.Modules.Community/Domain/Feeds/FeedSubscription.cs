using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Feeds;

/// <summary>
/// A member's saved view of a feed.
/// <para>
/// Not a follow. A follow says whose content to include; a subscription says which feed, with
/// which filters, ranked which way, and whether to be told when something arrives in it. The
/// client's home screen already keeps a selected category and a location in local state — this
/// is what makes that survive a reinstall and follow a member across devices.
/// </para>
/// <para>
/// Filters are stored as an opaque serialised blob rather than as columns, and that is a
/// deliberate limit on how much this aggregate knows. A feed provider owns its own parameters;
/// adding a filter to the nearby feed must not mean a migration here.
/// </para>
/// </summary>
public sealed class FeedSubscription : AggregateRoot<FeedSubscriptionId>, IAuditableEntity
{
    public const int MaxLabelLength = 100;

    public const int MaxFilterLength = 2_000;

    private FeedSubscription(
        FeedSubscriptionId id,
        UserId userId,
        string feedKey,
        string? label,
        string? rankingKey,
        string? filters,
        DateTimeOffset createdAt) : base(id)
    {
        UserId = userId;
        FeedKey = feedKey;
        Label = label;
        RankingKey = rankingKey;
        Filters = filters;
        CreatedAt = createdAt;
    }

    private FeedSubscription() { }

    public UserId UserId { get; private set; } = default!;

    /// <summary>Which feed. A registered provider's key, validated when the subscription is made.</summary>
    public string FeedKey { get; private set; } = string.Empty;

    public string? Label { get; private set; }

    /// <summary>Which ranking to apply. Null means the feed's own default.</summary>
    public string? RankingKey { get; private set; }

    /// <summary>Opaque to this aggregate. Owned by the feed provider that reads it.</summary>
    public string? Filters { get; private set; }

    public bool NotificationsEnabled { get; private set; }

    public int Position { get; private set; }

    public DateTimeOffset? LastReadAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    public static FeedSubscription Create(
        UserId userId,
        string feedKey,
        DateTimeOffset now,
        string? label = null,
        string? rankingKey = null,
        string? filters = null)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return new FeedSubscription(
            FeedSubscriptionId.New(),
            userId,
            (feedKey ?? string.Empty).Trim().ToLowerInvariant(),
            Truncate(label, MaxLabelLength),
            rankingKey?.Trim().ToLowerInvariant(),
            Truncate(filters, MaxFilterLength),
            now);
    }

    public void Update(string? label, string? rankingKey, string? filters)
    {
        Label = Truncate(label, MaxLabelLength);
        RankingKey = rankingKey?.Trim().ToLowerInvariant();
        Filters = Truncate(filters, MaxFilterLength);
    }

    public void SetNotifications(bool enabled) => NotificationsEnabled = enabled;

    public void MarkRead(DateTimeOffset now) => LastReadAt = now;

    public void MoveTo(int position) => Position = position;

    private static string? Truncate(string? value, int max)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }

    /// <summary>Stamped by the shared persistence interceptor. Never called by a handler.</summary>
    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    /// <inheritdoc cref="SetCreated" />
    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
