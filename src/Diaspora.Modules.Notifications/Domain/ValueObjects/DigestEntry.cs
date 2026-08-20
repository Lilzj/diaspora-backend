using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.ValueObjects;

/// <summary>
/// One line in an assembled digest.
/// <para>
/// A snapshot, not a reference. The notification it came from can be archived, expired or swept
/// away between the digest being built and being read, and a digest whose lines resolved at read
/// time would develop holes. It carries the identifier too, so a member tapping a line still lands
/// on the message if it is still there.
/// </para>
/// </summary>
public sealed class DigestEntry : ValueObject
{
    private DigestEntry(
        Guid entryId,
        NotificationId notificationId,
        string categoryKey,
        string title,
        string summary,
        string? route,
        DateTimeOffset occurredAt)
    {
        EntryId = entryId;
        NotificationId = notificationId;
        CategoryKey = categoryKey;
        Title = title;
        Summary = summary;
        Route = route;
        OccurredAt = occurredAt;
    }

    private DigestEntry() { }

    /// <summary>Caller-supplied key. The configuration declares <c>ValueGeneratedNever</c>.</summary>
    public Guid EntryId { get; private set; }

    public NotificationId NotificationId { get; private set; } = default!;

    public string CategoryKey { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    /// <summary>A one-line form of the body. Digests summarise; they do not reprint.</summary>
    public string Summary { get; private set; } = string.Empty;

    public string? Route { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static DigestEntry Create(
        Guid entryId,
        NotificationId notificationId,
        string categoryKey,
        string title,
        string summary,
        string? route,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(notificationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        return new DigestEntry(
            entryId,
            notificationId,
            categoryKey.Trim().ToLowerInvariant(),
            Clip(title, NotificationLimits.MaxTitleLength)!,
            Clip(summary, NotificationLimits.MaxPushBodyLength) ?? string.Empty,
            Clip(route, NotificationLimits.MaxDeepLinkLength),
            occurredAt);
    }

    private static string? Clip(string? value, int max) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return EntryId;
        yield return NotificationId;
        yield return CategoryKey;
        yield return Title;
        yield return Summary;
        yield return Route;
        yield return OccurredAt;
    }
}
