using System.Globalization;
using System.Text;

namespace Diaspora.Modules.Creator.Persistence;

/// <summary>
/// The opaque continuation token every Creator Platform page is fetched with.
/// <para>
/// <b>The platform's cursor primitive, not a second one.</b> §26 of the phase brief: "use the
/// established cursor pagination primitive. Do NOT invent another cursor format." This is the same
/// shape B18 uses — a base64 sort key and a boundary, read back through
/// <c>Diaspora.Persistence.Paging.CursorBoundary</c> — and it is a per-module type only because the
/// sort key differs, exactly as every module's does.
/// </para>
/// <para>
/// <b>Every page in this module sorts on a timestamp or a score, and neither is unique</b>, so every
/// one carries a tie-break. The boundary holds the identifiers already served at the boundary value
/// and the query excludes them by set membership — <c>Contains</c> over value-converted identifiers
/// translates on every provider, where an ordering comparison on one does not (DSP3003).
/// </para>
/// </summary>
public sealed record CreatorCursor
{
    private const char Separator = '|';

    private CreatorCursor(string sortKey, string boundaryKey)
    {
        SortKey = sortKey;
        BoundaryKey = boundaryKey;
    }

    /// <summary>The last row's value on whichever column the sort ordered by.</summary>
    public string SortKey { get; }

    /// <summary>
    /// The identifiers already served at <see cref="SortKey"/>, or the exhausted marker. Read back
    /// through <c>CursorBoundary.Exclusion</c>, which reports both the identifiers and whether the
    /// tie group was abandoned.
    /// </summary>
    public string BoundaryKey { get; }

    public static CreatorCursor FromTimestamp(DateTimeOffset timestamp, string boundaryKey) =>
        new(timestamp.UtcTicks.ToString(CultureInfo.InvariantCulture), boundaryKey);

    public static CreatorCursor FromScore(long score, string boundaryKey) =>
        new(score.ToString(CultureInfo.InvariantCulture), boundaryKey);

    /// <summary>
    /// Parses a token, returning null on anything malformed.
    /// <para>
    /// Null rather than throwing, because a stale or hand-edited cursor arriving from a client is an
    /// ordinary event — an old app version, a shared link, a copy-paste — and answering it with the
    /// first page is far better than answering a browse screen with a 400.
    /// </para>
    /// </summary>
    public static CreatorCursor? Parse(string? token)
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

            return new CreatorCursor(decoded[..separator], decoded[(separator + 1)..]);
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

    /// <summary>The sort key as a score. Null when the token did not carry one.</summary>
    public long? AsScore() =>
        long.TryParse(SortKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out var score)
            ? score
            : null;
}
