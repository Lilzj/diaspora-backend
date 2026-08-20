using System.Globalization;
using System.Text;

namespace Diaspora.Modules.Recommendations.Persistence;

/// <summary>
/// The opaque continuation token every paged recommendation list is fetched with.
/// <para>
/// <b>Cursor rather than offset, and on a recommendation feed the difference is worse than
/// usual.</b> The underlying set changes constantly — candidates are projected and withdrawn as
/// other modules publish — so on an offset page a member scrolling sees an item twice and misses
/// another. On a personalised feed the one they miss is, by construction, one the platform thought
/// they would want.
/// </para>
/// <para>
/// The token carries the sort key and the identifier of the last row, and the query excludes by
/// identity at the boundary rather than comparing on it. That is B1.3's rule and it is not optional
/// here: identifiers are value-converted strongly typed ids, and no provider translates an ordering
/// comparison on one, so the textbook keyset predicate cannot be written. Rows created in one unit
/// of work share an instant exactly, so a strict timestamp comparison would silently drop every one
/// of them but the first.
/// </para>
/// <para>
/// Base64 of a compact string rather than JSON, because a readable token is a token clients parse,
/// and a parsed token is a contract nobody meant to freeze.
/// </para>
/// </summary>
public sealed record RecommendationCursor
{
    private const char Separator = '|';

    private RecommendationCursor(string sortKey, string lastKey)
    {
        SortKey = sortKey;
        LastKey = lastKey;
    }

    /// <summary>The last row's value on whichever column the sort ordered by.</summary>
    public string SortKey { get; }

    /// <summary>The last row's identifier, as an opaque marker for exclusion.</summary>
    public string LastKey { get; }

    public static RecommendationCursor FromTimestamp(DateTimeOffset timestamp, string lastKey) =>
        new(timestamp.UtcTicks.ToString(CultureInfo.InvariantCulture), lastKey);

    public static RecommendationCursor FromScore(decimal score, string lastKey) =>
        new(score.ToString("F6", CultureInfo.InvariantCulture), lastKey);

    public static RecommendationCursor Create(string? sortKey, string lastKey) =>
        new(sortKey ?? string.Empty, lastKey);

    /// <summary>
    /// Parses a token, returning null on anything malformed.
    /// <para>
    /// Null rather than throwing: a stale or hand-edited cursor is an ordinary event — an old app
    /// version, a shared link — and answering a feed with the first page is far better than
    /// answering it with a 400.
    /// </para>
    /// </summary>
    public static RecommendationCursor? Parse(string? token)
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

            return new RecommendationCursor(decoded[..separator], decoded[(separator + 1)..]);
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

    public decimal? AsScore() =>
        decimal.TryParse(SortKey, NumberStyles.Number, CultureInfo.InvariantCulture, out var score)
            ? score
            : null;
}
