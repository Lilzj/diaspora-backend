using System.Globalization;
using System.Text;

namespace Diaspora.Modules.Automation.Persistence;

/// <summary>
/// The opaque continuation token every automation page is fetched with.
/// <para>
/// <b>The platform's cursor primitive, not a second one.</b> Same shape as B18's and B19's — a base64
/// sort key and a boundary, read back through <c>Diaspora.Persistence.Paging.CursorBoundary</c> — and
/// it is a per-module type only because the sort key differs, exactly as every module's does.
/// </para>
/// <para>
/// <b>Both pages this module serves sort on a timestamp, and a timestamp is not unique.</b> The shared
/// persistence interceptor stamps every row written in one unit of work with an identical instant, so
/// a strict comparison on the sort key alone silently drops every row at the boundary — nineteen
/// applicants vanished the last time this platform did it, and the person never seen has no way of
/// knowing. The boundary holds the identifiers already served and the query excludes them by set
/// membership, because <c>Contains</c> over a value-converted identifier translates on every provider
/// where an ordering comparison on one does not (DSP3003).
/// </para>
/// </summary>
public sealed record AutomationCursor
{
    private const char Separator = '|';

    private AutomationCursor(string sortKey, string boundaryKey)
    {
        SortKey = sortKey;
        BoundaryKey = boundaryKey;
    }

    public string SortKey { get; }

    public string BoundaryKey { get; }

    public static AutomationCursor FromTimestamp(DateTimeOffset timestamp, string boundaryKey) =>
        new(timestamp.UtcTicks.ToString(CultureInfo.InvariantCulture), boundaryKey);

    /// <summary>
    /// Parses a token, returning null on anything malformed.
    /// <para>
    /// Null rather than throwing, because a stale or hand-edited cursor from a client is an ordinary
    /// event — an old app version, a shared link, a copy-paste — and answering it with the first page
    /// is better than answering a list screen with a 400.
    /// </para>
    /// </summary>
    public static AutomationCursor? Parse(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var separator = decoded.IndexOf(Separator, StringComparison.Ordinal);

            return separator <= 0
                ? null
                : new AutomationCursor(decoded[..separator], decoded[(separator + 1)..]);
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
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{SortKey}{Separator}{BoundaryKey}"));

    public DateTimeOffset? AsTimestamp() =>
        long.TryParse(SortKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks)
            ? new DateTimeOffset(ticks, TimeSpan.Zero)
            : null;
}
