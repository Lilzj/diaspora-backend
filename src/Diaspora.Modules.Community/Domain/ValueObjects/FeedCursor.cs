using System.Buffers.Text;
using System.Globalization;
using System.Text;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.ValueObjects;

/// <summary>
/// An opaque position in a feed.
/// <para>
/// <b>Offset pagination is wrong for a feed and this type is the reason it is not used.</b> A
/// feed changes while a member scrolls; page two by offset re-shows whatever page one already
/// showed once anything was inserted above it, and skips whatever fell through. A cursor names
/// a row rather than a distance, so the second page is correct no matter what happened in
/// between.
/// </para>
/// <para>
/// The payload is a sort key and a tie-break identifier. The key is a double rather than a
/// timestamp because a ranked feed's key is a score, and a chronological feed's score is just
/// its timestamp expressed as one — one cursor shape for every feed, so a client that switches
/// from newest to trending does not need a different pagination model.
/// </para>
/// <para>
/// Base64url-encoded and treated as opaque. Not encrypted, and not pretending to be: a member
/// who decodes it learns a score and an identifier they were already shown. What the encoding
/// buys is that nobody writes a client that constructs one by hand, which is what freezes an
/// internal sort key into a public contract by accident.
/// </para>
/// </summary>
public sealed class FeedCursor : ValueObject
{
    private const char Separator = '|';

    private FeedCursor(
        double sortKey, Guid lastId, string? boundaryIds = null, bool leadingFlag = false)
    {
        SortKey = sortKey;
        LastId = lastId;
        BoundaryIds = boundaryIds ?? string.Empty;
        LeadingFlag = leadingFlag;
    }

    /// <summary>The score or timestamp of the last item on the previous page.</summary>
    public double SortKey { get; }

    /// <summary>
    /// The identifier of that last item. The tie-break, and the reason a feed with a hundred
    /// posts sharing one score does not loop forever on the same page.
    /// </summary>
    public Guid LastId { get; }

    /// <summary>
    /// Every identifier already returned at <see cref="SortKey"/> — what the next page skips.
    /// <para>
    /// <b><see cref="LastId"/> alone is not enough, for two separate reasons.</b> A predicate
    /// comparing it (<c>Id &lt; @lastId</c>) does not translate: these are value-converted
    /// strongly-typed ids, and <c>Guid.CompareTo</c> fails outright on SQLite. And a tie group
    /// wider than a page needs more than one identifier remembered, or page three hands back
    /// page one's rows.
    /// </para>
    /// <para>
    /// Managed by <c>CursorBoundary</c>, which accumulates while the sort key holds still.
    /// </para>
    /// </summary>
    public string BoundaryIds { get; }

    /// <summary>
    /// Whether the last item on the previous page was pinned, for the listings that sort pinned
    /// items first.
    /// <para>
    /// <b>Without it, a pinned item repeats on every page.</b> A listing ordered by
    /// <c>(IsPinned, PublishedAt)</c> whose cursor compares only <c>PublishedAt</c> re-admits
    /// every pinned row on page two — pinned rows sort first regardless of age, so an old pinned
    /// post satisfies "older than the boundary" and jumps back to the top. The cursor has to
    /// carry every column the ORDER BY leads with, and this is the first one.
    /// </para>
    /// </summary>
    public bool LeadingFlag { get; }

    public static FeedCursor Create(
        double sortKey, Guid lastId, string? boundaryIds = null, bool leadingFlag = false) =>
        new(sortKey, lastId, boundaryIds, leadingFlag);

    /// <summary>A chronological cursor. The timestamp is the score.</summary>
    public static FeedCursor FromTimestamp(
        DateTimeOffset timestamp,
        Guid lastId,
        string? boundaryIds = null,
        bool leadingFlag = false) =>
        new(timestamp.ToUnixTimeMilliseconds(), lastId, boundaryIds, leadingFlag);

    public DateTimeOffset AsTimestamp() =>
        DateTimeOffset.FromUnixTimeMilliseconds((long)SortKey);

    public string Encode()
    {
        var raw = string.Concat(
            SortKey.ToString("R", CultureInfo.InvariantCulture),
            Separator,
            LastId.ToString("N", CultureInfo.InvariantCulture),
            Separator,
            BoundaryIds,
            Separator,
            LeadingFlag ? "1" : "0");

        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(raw));
    }

    /// <summary>
    /// Decodes a cursor, or returns null.
    /// <para>
    /// Null rather than an error, on purpose. A malformed cursor is almost always a stale
    /// client or a truncated URL, and the right response to it is the first page — not a 400
    /// that leaves an infinite scroll permanently stuck. A cursor is a hint about where to
    /// resume, and an unreadable hint means resume from the start.
    /// </para>
    /// </summary>
    public static FeedCursor? Decode(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return null;
        }

        try
        {
            var raw = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(encoded));
            var parts = raw.Split(Separator);

            // Two and three parts are older shapes, still held by clients mid-scroll. The
            // missing fields decode to their defaults, which degrades to plain sort-key paging
            // rather than failing — the same reason a malformed cursor returns the first page
            // instead of a 400 that leaves an infinite scroll permanently stuck.
            if (parts.Length is not (2 or 3 or 4))
            {
                return null;
            }

            if (!double.TryParse(
                    parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var sortKey))
            {
                return null;
            }

            return Guid.TryParseExact(parts[1], "N", out var lastId)
                ? new FeedCursor(
                    sortKey,
                    lastId,
                    parts.Length >= 3 ? parts[2] : null,
                    parts.Length >= 4 && parts[3] == "1")
                : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SortKey;
        yield return LastId;
        yield return BoundaryIds;
        yield return LeadingFlag;
    }
}
