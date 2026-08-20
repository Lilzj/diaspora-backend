using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.ValueObjects;

/// <summary>
/// What the member did with the message and when.
/// <para>
/// <b>Four instants, each recorded once.</b> A member who reads a message three times has opened it
/// once; overwriting <see cref="OpenedAt"/> on every read would destroy the only number that says
/// how long the platform took to reach somebody, which is the number every channel is judged on.
/// </para>
/// <para>
/// An owned value object rather than four columns on the aggregate, so that "has this been engaged
/// with at all" is one question with one answer instead of four nullable comparisons repeated at
/// every call site.
/// </para>
/// </summary>
public sealed class NotificationEngagement : ValueObject
{
    private NotificationEngagement(
        DateTimeOffset? openedAt,
        DateTimeOffset? clickedAt,
        DateTimeOffset? dismissedAt,
        DateTimeOffset? archivedAt,
        string? openedOnChannel)
    {
        OpenedAt = openedAt;
        ClickedAt = clickedAt;
        DismissedAt = dismissedAt;
        ArchivedAt = archivedAt;
        OpenedOnChannel = openedOnChannel;
    }

    private NotificationEngagement() { }

    public DateTimeOffset? OpenedAt { get; private set; }

    public DateTimeOffset? ClickedAt { get; private set; }

    public DateTimeOffset? DismissedAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    /// <summary>Which channel the member was on when they first read it.</summary>
    public string? OpenedOnChannel { get; private set; }

    public static NotificationEngagement None() => new(null, null, null, null, null);

    public bool IsOpened => OpenedAt.HasValue;

    public bool IsClicked => ClickedAt.HasValue;

    public bool IsDismissed => DismissedAt.HasValue;

    public bool IsArchived => ArchivedAt.HasValue;

    /// <summary>Records a first read. A second call returns the same instance unchanged.</summary>
    public NotificationEngagement WithOpened(DateTimeOffset at, string channel) =>
        OpenedAt.HasValue
            ? this
            : new NotificationEngagement(at, ClickedAt, DismissedAt, ArchivedAt, channel);

    /// <summary>
    /// Records a click, and a read if there was not one.
    /// <para>
    /// A member cannot click something they have not seen, so a click that arrives without a prior
    /// open backfills it. Without this, every open-rate would under-count by exactly the members who
    /// tapped straight through from a push banner.
    /// </para>
    /// </summary>
    public NotificationEngagement WithClicked(DateTimeOffset at, string channel) =>
        ClickedAt.HasValue
            ? this
            : new NotificationEngagement(
                OpenedAt ?? at, at, DismissedAt, ArchivedAt, OpenedOnChannel ?? channel);

    public NotificationEngagement WithDismissed(DateTimeOffset at) =>
        DismissedAt.HasValue
            ? this
            : new NotificationEngagement(OpenedAt, ClickedAt, at, ArchivedAt, OpenedOnChannel);

    public NotificationEngagement WithArchived(DateTimeOffset at) =>
        new(OpenedAt, ClickedAt, DismissedAt, at, OpenedOnChannel);

    /// <summary>Clears the archive stamp. The restore path; every other instant survives.</summary>
    public NotificationEngagement WithoutArchived() =>
        new(OpenedAt, ClickedAt, DismissedAt, null, OpenedOnChannel);

    /// <summary>Marks the message unread again, which is the one engagement fact a member may undo.</summary>
    public NotificationEngagement WithoutOpened() =>
        new(null, ClickedAt, DismissedAt, ArchivedAt, null);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return OpenedAt;
        yield return ClickedAt;
        yield return DismissedAt;
        yield return ArchivedAt;
        yield return OpenedOnChannel;
    }
}
