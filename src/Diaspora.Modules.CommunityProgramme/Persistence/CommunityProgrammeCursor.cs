using System.Globalization;
using System.Text;

namespace Diaspora.Modules.CommunityProgramme.Persistence;

/// <summary>
/// The opaque continuation token every Community Programme page is fetched with.
/// <para>
/// <b>Cursor rather than offset, and the difference is correctness rather than speed.</b> An offset
/// page over a table being written to skips and repeats rows: an applicant who applies between page
/// one and page two shifts everything down, so an organizer sees one person twice and never sees
/// another at all. The one they never saw does not come round again — they wait, and are not called.
/// </para>
/// <para>
/// <b>Every page in this module sorts on a timestamp, which is not unique</b>, so every one of them
/// carries a tie-break. <c>CreatedAt</c> is stamped by the shared persistence interceptor, so every
/// row written in one unit of work carries an identical instant, and two writes inside one clock tick
/// tie anyway. The boundary is the platform's <c>CursorBoundary</c> — the identifiers already served
/// at the boundary sort key, excluded by <c>Contains</c>, which translates on every provider where a
/// keyset comparison on a value-converted identifier does not.
/// </para>
/// <para>
/// Base64 of a compact string rather than JSON, because a token that is readable is a token clients
/// will parse, and a parsed token becomes a contract nobody meant to freeze.
/// </para>
/// </summary>
public sealed record CommunityProgrammeCursor
{
    private const char Separator = '|';

    private CommunityProgrammeCursor(string sortKey, string boundaryKey)
    {
        SortKey = sortKey;
        BoundaryKey = boundaryKey;
    }

    /// <summary>The last row's value on whichever column the sort ordered by.</summary>
    public string SortKey { get; }

    /// <summary>
    /// The identifiers already served at <see cref="SortKey"/>, or the exhausted marker.
    /// <para>
    /// Read back through <c>CursorBoundary.Exclusion</c>, which reports both the identifiers and
    /// whether the tie group was abandoned. Reading one without the other is the defect that type
    /// exists to make impossible.
    /// </para>
    /// </summary>
    public string BoundaryKey { get; }

    public static CommunityProgrammeCursor FromTimestamp(
        DateTimeOffset timestamp, string boundaryKey) =>
        new(timestamp.UtcTicks.ToString(CultureInfo.InvariantCulture), boundaryKey);

    /// <summary>
    /// Parses a token, returning null on anything malformed.
    /// <para>
    /// Null rather than throwing, because a stale or hand-edited cursor arriving from a client is an
    /// ordinary event — an old app version, a shared link, a copy-paste — and answering it with the
    /// first page is far better than answering a browse screen with a 400.
    /// </para>
    /// </summary>
    public static CommunityProgrammeCursor? Parse(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var separator = decoded.IndexOf(Separator, StringComparison.Ordinal);

            if (separator <= 0)
            {
                return null;
            }

            return new CommunityProgrammeCursor(
                decoded[..separator], decoded[(separator + 1)..]);
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
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{SortKey}{Separator}{BoundaryKey}"));

    /// <summary>The sort key as a moment. Null when the token did not carry one.</summary>
    public DateTimeOffset? AsTimestamp() =>
        long.TryParse(SortKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks)
            ? new DateTimeOffset(ticks, TimeSpan.Zero)
            : null;
}
