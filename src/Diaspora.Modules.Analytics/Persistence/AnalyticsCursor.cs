using System.Globalization;
using System.Text;

namespace Diaspora.Modules.Analytics.Persistence;

/// <summary>
/// The opaque continuation token the content-performance list is paged with.
/// <para>
/// <b>The platform's cursor shape, not a second one</b> — a base64 sort key with a boundary
/// identifier — and the same rule behind it: the sort key is not unique, so the token carries the
/// identity at the boundary and the next page resumes strictly after it. §26 of the phase brief:
/// "use the established cursor pagination primitive. Do not create another."
/// </para>
/// <para>
/// <b>Why the boundary rather than an offset.</b> The ordering is by views, and views change between
/// one page and the next: an offset of fifteen would skip a row the moment an item overtook another,
/// and would serve one twice when an item fell. Resuming from the last row served is stable under
/// both.
/// </para>
/// <para>
/// <b>Why the ordering key is not a stored column.</b> A row's rank is a sum over the period's daily
/// cells and exists only once the period is known, so this cursor's sort key is composed at query
/// time rather than read from an index. That is the one thing it does differently from the platform's
/// other cursors, and it is why the boundary is compared with the same comparator the ordering used
/// rather than translated into a <c>WHERE</c> clause.
/// </para>
/// </summary>
public sealed record AnalyticsCursor
{
    private const char Separator = '|';

    private AnalyticsCursor(long views, long publishedTicks, Guid contentId)
    {
        Views = views;
        PublishedTicks = publishedTicks;
        ContentId = contentId;
    }

    /// <summary>The last row's view count — the primary sort key.</summary>
    public long Views { get; }

    /// <summary>The last row's publication instant, for the first tie-break.</summary>
    public long PublishedTicks { get; }

    /// <summary>The last row's identity, for the final tie-break and for the resume point.</summary>
    public Guid ContentId { get; }

    public static AnalyticsCursor For(long views, DateTimeOffset? publishedAt, Guid contentId) =>
        new(views, (publishedAt ?? DateTimeOffset.MinValue).UtcTicks, contentId);

    /// <summary>
    /// Parses a token, returning null on anything malformed.
    /// <para>
    /// Null rather than throwing, because a stale or hand-edited cursor arriving from a client is an
    /// ordinary event — an old app version, a shared link, a copy-paste — and answering it with the
    /// first page is far better than answering a dashboard with a 400.
    /// </para>
    /// </summary>
    public static AnalyticsCursor? Parse(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts = decoded.Split(Separator);

            if (parts.Length != 3
                || !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var views)
                || !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks)
                || !Guid.TryParse(parts[2], out var contentId))
            {
                return null;
            }

            return new AnalyticsCursor(views, ticks, contentId);
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
    public string Encode() => Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Create(
        CultureInfo.InvariantCulture,
        $"{Views}{Separator}{PublishedTicks}{Separator}{ContentId:D}")));
}
