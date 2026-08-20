using System.Globalization;
using System.Text;

namespace Diaspora.Modules.Marketplace.Persistence;

/// <summary>
/// The opaque continuation token every Marketplace listing pages with.
/// <para>
/// <b>Cursor rather than offset, and the difference is correctness rather than speed.</b> An offset
/// page over a table being written to skips and repeats rows: a listing published between page one
/// and page two shifts everything down, so a buyer scrolling sees one item twice and never sees
/// another at all. On a marketplace that is not a cosmetic bug — the listing they never saw is the
/// one they did not buy.
/// </para>
/// <para>
/// The token carries the sort key and a tie-break for the last row, so the next page is a strict
/// "after this" comparison. For listing pages the tie-break is the slug — unique, indexed and a
/// string, because the primary key is a value-converted identifier EF cannot compare in SQL, which
/// is B7's lesson and B8's and B9's verbatim. Pages with no slug — offers, reservations, saved rows
/// — page on their timestamp and carry the identifier only as an opaque marker.
/// </para>
/// <para>
/// Base64 of a compact string rather than JSON, because a token that is readable is a token clients
/// will parse, and a parsed token becomes a contract nobody meant to freeze.
/// </para>
/// </summary>
public sealed record MarketplaceCursor
{
    private const char Separator = '|';

    private MarketplaceCursor(string sortKey, string lastKey)
    {
        SortKey = sortKey;
        LastKey = lastKey;
    }

    /// <summary>The last row's value on whichever column the sort ordered by.</summary>
    public string SortKey { get; }

    /// <summary>The last row's tie-break — the slug for listings, the identifier elsewhere.</summary>
    public string LastKey { get; }

    public static MarketplaceCursor Create(string sortKey, string lastKey) =>
        new(sortKey ?? string.Empty, lastKey);

    /// <summary>A cursor from a timestamp sort — published, created, expiring.</summary>
    public static MarketplaceCursor FromTimestamp(DateTimeOffset timestamp, string lastKey) =>
        new(timestamp.UtcTicks.ToString(CultureInfo.InvariantCulture), lastKey);

    /// <summary>A cursor from a numeric sort — price, view count.</summary>
    public static MarketplaceCursor FromNumber(double value, string lastKey) =>
        new(value.ToString("R", CultureInfo.InvariantCulture), lastKey);

    /// <summary>
    /// Parses a token, returning null on anything malformed.
    /// <para>
    /// Null rather than throwing, because a stale or hand-edited cursor arriving from a client is
    /// an ordinary event — an old app version, a shared link, a copy-paste — and answering it with
    /// the first page is far better than answering a browse screen with a 400.
    /// </para>
    /// </summary>
    public static MarketplaceCursor? Parse(string? token)
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

            return new MarketplaceCursor(decoded[..separator], decoded[(separator + 1)..]);
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

    /// <summary>The token to hand back to the client.</summary>
    public string Encode() =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{SortKey}{Separator}{LastKey}"));

    /// <summary>The sort key as ticks, for a timestamp-ordered query. Null when it is not one.</summary>
    public DateTimeOffset? AsTimestamp() =>
        long.TryParse(SortKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks)
            ? new DateTimeOffset(ticks, TimeSpan.Zero)
            : null;

    /// <summary>The sort key as a number, for a price- or count-ordered query.</summary>
    public double? AsNumber() =>
        double.TryParse(SortKey, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}
