using System.Globalization;
using System.Text;

namespace Diaspora.Modules.Providers.Persistence;

/// <summary>
/// The opaque continuation token discovery pages with.
/// <para>
/// <b>Cursor rather than offset, and the difference is correctness rather than speed.</b> An
/// offset page over a table that is being written to skips and repeats rows: a provider
/// published between page one and page two shifts everything down, so a member scrolling sees
/// one provider twice and never sees another at all. On an infinite-scroll list — which is
/// exactly what the client's discovery tab is — that happens constantly.
/// </para>
/// <para>
/// The token carries the sort key and the identifier of the last row, so the next page is a
/// strict "after this" comparison. The identifier is the tie-break, and it is what makes the
/// paging total rather than merely mostly-total: two providers published in the same
/// millisecond, or with identical ratings, are still ordered deterministically.
/// </para>
/// <para>
/// Base64 of a compact string rather than JSON, because a token that is readable is a token
/// clients will parse, and a parsed token becomes a contract nobody meant to freeze.
/// </para>
/// </summary>
public sealed record ProviderCursor
{
    private const char Separator = '|';

    private ProviderCursor(string sortKey, string lastSlug, bool leadingFlag = false)
    {
        SortKey = sortKey;
        LastSlug = lastSlug;
        LeadingFlag = leadingFlag;
    }

    /// <summary>The last row's value on whichever column the sort ordered by.</summary>
    public string SortKey { get; }

    /// <summary>
    /// The last row's slug. The deterministic tie-break.
    /// <para>
    /// The slug rather than the identifier, and the reason is a database one: the primary key is
    /// a value-converted <c>ProviderId</c>, and EF cannot translate a comparison on it. The slug
    /// is unique, indexed and a string, so it compares in SQL on every provider.
    /// </para>
    /// </summary>
    public string LastSlug { get; }

    /// <summary>
    /// Whether the last row on the previous page carried the listing's leading boolean sort
    /// column — <c>IsFeatured</c> on a recommendation listing.
    /// <para>
    /// <b>Without it, a featured row repeats on every page.</b> A listing ordered by
    /// <c>(IsFeatured, CreatedAt)</c> whose cursor compares only <c>CreatedAt</c> re-admits every
    /// featured row on page two: featured rows sort first regardless of age, so an old one
    /// satisfies "older than the boundary" and jumps straight back to the top. A cursor has to
    /// carry every column the ORDER BY leads with.
    /// </para>
    /// </summary>
    public bool LeadingFlag { get; }

    public static ProviderCursor Create(string sortKey, string lastSlug) =>
        new(sortKey ?? string.Empty, lastSlug);

    /// <summary>A cursor from a timestamp sort — publication or creation.</summary>
    public static ProviderCursor FromTimestamp(
        DateTimeOffset timestamp, string lastSlug, bool leadingFlag = false) =>
        new(timestamp.UtcTicks.ToString(CultureInfo.InvariantCulture), lastSlug, leadingFlag);

    /// <summary>A cursor from a numeric sort — rating, review count, distance.</summary>
    public static ProviderCursor FromNumber(double value, string lastSlug) =>
        new(value.ToString("R", CultureInfo.InvariantCulture), lastSlug);

    /// <summary>
    /// Parses a token, returning null on anything malformed.
    /// <para>
    /// Null rather than throwing, because a stale or hand-edited cursor arriving from a client
    /// is an ordinary event — an old app version, a shared link, a copy-paste — and answering it
    /// with the first page is far better than answering it with a 400 on a browse screen.
    /// </para>
    /// </summary>
    public static ProviderCursor? Parse(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));

            // Split on the first separator, not the last: the last key may now be followed by an
            // optional leading-sort flag. Safe because a sort key is a number and a last key is
            // a slug or a comma-joined identifier list — neither can contain a separator.
            var parts = decoded.Split(Separator);

            if (parts.Length is not (2 or 3) || parts[0].Length == 0 || parts[1].Length == 0)
            {
                return null;
            }

            // Two parts is the older shape, still held by clients mid-scroll. It decodes with
            // the flag defaulted, which degrades to plain paging rather than failing.
            return new ProviderCursor(parts[0], parts[1], parts.Length == 3 && parts[2] == "1");
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
        Convert.ToBase64String(
            Encoding.UTF8.GetBytes(
                $"{SortKey}{Separator}{LastSlug}{Separator}{(LeadingFlag ? "1" : "0")}"));

    /// <summary>The sort key as ticks, for a timestamp-ordered query. Null when it is not one.</summary>
    public DateTimeOffset? AsTimestamp() =>
        long.TryParse(SortKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks)
            ? new DateTimeOffset(ticks, TimeSpan.Zero)
            : null;

    /// <summary>The sort key as a number, for a rating- or count-ordered query.</summary>
    public double? AsNumber() =>
        double.TryParse(SortKey, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}
