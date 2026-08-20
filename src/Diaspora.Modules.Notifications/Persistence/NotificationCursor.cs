using System.Globalization;
using System.Text;

namespace Diaspora.Modules.Notifications.Persistence;

/// <summary>
/// The opaque continuation token every notification page is fetched with.
/// <para>
/// <b>Cursor rather than offset, and here the difference is not subtle.</b> An inbox is the most
/// actively written table a member has: notifications arrive while they scroll. On an offset page,
/// every arrival shifts the window down, so the reader sees one message twice and never sees
/// another — and the one they never see is, by construction, the one nobody told them about.
/// </para>
/// <para>
/// The token carries the sort key — creation ticks — and the identifier of the last row, and the
/// query excludes by identity at the boundary rather than comparing on it. That is B1.3's rule and
/// it is not optional here: identifiers are value-converted strongly typed ids, and no provider
/// translates an ordering comparison on one, so the textbook keyset predicate cannot be written.
/// Messages created in one unit of work share an instant exactly, so a strict timestamp comparison
/// would silently drop every one of them but the first.
/// </para>
/// <para>
/// Base64 of a compact string rather than JSON, because a readable token is a token clients parse,
/// and a parsed token is a contract nobody meant to freeze.
/// </para>
/// </summary>
public sealed record NotificationCursor
{
    private const char Separator = '|';

    private NotificationCursor(string sortKey, string lastKey)
    {
        SortKey = sortKey;
        LastKey = lastKey;
    }

    /// <summary>The last row's value on whichever column the sort ordered by.</summary>
    public string SortKey { get; }

    /// <summary>The last row's identifier, as an opaque marker for exclusion.</summary>
    public string LastKey { get; }

    public static NotificationCursor FromTimestamp(DateTimeOffset timestamp, string lastKey) =>
        new(timestamp.UtcTicks.ToString(CultureInfo.InvariantCulture), lastKey);

    public static NotificationCursor Create(string? sortKey, string lastKey) =>
        new(sortKey ?? string.Empty, lastKey);

    /// <summary>
    /// Parses a token, returning null on anything malformed.
    /// <para>
    /// Null rather than throwing: a stale or hand-edited cursor is an ordinary event — an old app
    /// version, a shared link — and answering an inbox screen with the first page is far better than
    /// answering it with a 400.
    /// </para>
    /// </summary>
    public static NotificationCursor? Parse(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var separator = decoded.LastIndexOf(Separator);

            if (separator <= 0 || separator == decoded.Length - 1)
            {
                return null;
            }

            return new NotificationCursor(decoded[..separator], decoded[(separator + 1)..]);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    public string Encode() =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{SortKey}{Separator}{LastKey}"));

    public DateTimeOffset? AsTimestamp() =>
        long.TryParse(SortKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks)
            ? new DateTimeOffset(ticks, TimeSpan.Zero)
            : null;
}
