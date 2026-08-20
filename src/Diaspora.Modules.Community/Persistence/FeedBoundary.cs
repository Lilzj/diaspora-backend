using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Persistence.Paging;

namespace Diaspora.Modules.Community.Persistence;

/// <summary>
/// Turns a <see cref="FeedCursor"/> into a boundary a query can compare against, and back again.
/// <para>
/// <b>Why a window rather than an instant.</b> <see cref="FeedCursor"/> carries its sort key as a
/// double, and a chronological cursor stores Unix <i>milliseconds</i> — a tick count would not
/// survive the round trip through a double. The stored column keeps full precision, so
/// <c>PublishedAt == cursor.AsTimestamp()</c> is almost never true and
/// <c>PublishedAt &lt; cursor.AsTimestamp()</c> quietly skips everything between the truncated
/// millisecond and the row the page actually ended on.
/// </para>
/// <para>
/// So the boundary is the millisecond itself: <c>[Start, End)</c>. Rows outside it are ordered
/// unambiguously; rows inside it are the tie group, and are separated by identity through
/// <see cref="CursorBoundary"/>. Both halves agree because both truncate to the same unit.
/// </para>
/// </summary>
internal static class FeedBoundary
{
    /// <summary>The millisecond the previous page ended in.</summary>
    public static (DateTimeOffset Start, DateTimeOffset End) Window(FeedCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);

        var start = cursor.AsTimestamp();

        return (start, start.AddMilliseconds(1));
    }

    /// <summary>
    /// The identifiers the next page must skip, truncated to the same millisecond the query
    /// compares on.
    /// </summary>
    public static string Key<T>(
        IReadOnlyList<T> rows,
        Func<T, DateTimeOffset> at,
        Func<T, Guid> id,
        FeedCursor? previous)
    {
        ArgumentNullException.ThrowIfNull(at);

        return CursorBoundary.Key(
            rows,
            row => at(row).ToUnixTimeMilliseconds(),
            id,
            previous?.BoundaryIds,
            previous?.AsTimestamp().ToUnixTimeMilliseconds());
    }
}
