using System.Globalization;
using System.Text;

namespace Diaspora.Modules.Jobs.Persistence;

/// <summary>
/// The opaque continuation token every Jobs listing pages with.
/// <para>
/// <b>Cursor rather than offset, and the difference is correctness rather than speed.</b> An
/// offset page over a table being written to skips and repeats rows: a job published between
/// page one and page two shifts everything down, so a candidate scrolling sees one posting
/// twice and never sees another at all. On a job board that is not a cosmetic bug — the posting
/// they never saw is the one they did not apply to.
/// </para>
/// <para>
/// The token carries the sort key and a tie-break for the last row, so the next page is a
/// strict "after this" comparison. For posting listings the tie-break is the slug — unique,
/// indexed and a string, because the primary key is a value-converted <c>JobPostingId</c> that
/// EF cannot compare in SQL, which is B7's lesson and B8's verbatim. Listings with no slug —
/// applications, interviews, saved jobs — page on their timestamp and carry the identifier
/// only as an opaque marker.
/// </para>
/// <para>
/// Base64 of a compact string rather than JSON, because a token that is readable is a token
/// clients will parse, and a parsed token becomes a contract nobody meant to freeze.
/// </para>
/// </summary>
public sealed record JobCursor
{
    private const char Separator = '|';

    private JobCursor(string sortKey, string lastKey)
    {
        SortKey = sortKey;
        LastKey = lastKey;
    }

    /// <summary>The last row's value on whichever column the sort ordered by.</summary>
    public string SortKey { get; }

    /// <summary>The last row's tie-break — the slug for postings, the identifier elsewhere.</summary>
    public string LastKey { get; }

    public static JobCursor Create(string sortKey, string lastKey) =>
        new(sortKey ?? string.Empty, lastKey);

    /// <summary>A cursor from a timestamp sort — published, submitted, scheduled, saved.</summary>
    public static JobCursor FromTimestamp(DateTimeOffset timestamp, string lastKey) =>
        new(timestamp.UtcTicks.ToString(CultureInfo.InvariantCulture), lastKey);

    /// <summary>A cursor from a numeric sort — salary, application count.</summary>
    public static JobCursor FromNumber(double value, string lastKey) =>
        new(value.ToString("R", CultureInfo.InvariantCulture), lastKey);

    /// <summary>
    /// Parses a token, returning null on anything malformed.
    /// <para>
    /// Null rather than throwing, because a stale or hand-edited cursor arriving from a client
    /// is an ordinary event — an old app version, a shared link, a copy-paste — and answering
    /// it with the first page is far better than answering a browse screen with a 400.
    /// </para>
    /// </summary>
    public static JobCursor? Parse(string? token)
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

            return new JobCursor(decoded[..separator], decoded[(separator + 1)..]);
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

    /// <summary>The sort key as a number, for a salary- or count-ordered query.</summary>
    public double? AsNumber() =>
        double.TryParse(SortKey, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}
