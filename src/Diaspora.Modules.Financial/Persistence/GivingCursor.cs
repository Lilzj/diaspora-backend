using System.Globalization;
using System.Text;

namespace Diaspora.Modules.Financial.Persistence;

/// <summary>
/// The opaque continuation token B15.1's paged reads are fetched with.
/// <para>
/// <b>ADR-017's boundary, not a keyset comparison.</b> Every page here sorts newest-first on a
/// timestamp, and a timestamp is not unique: <c>CreatedAt</c> is stamped by the shared persistence
/// interceptor, so every row written in one unit of work carries an identical instant. A plain
/// <c>&lt; boundary</c> drops the whole tie group and the rows in it are never served — the
/// supporter nobody sees, the campaign that never appears on page two. The token therefore carries
/// the boundary instant plus the identifiers already served at it, and the query excludes those by
/// <c>Contains</c>, which every provider translates where a comparison on a value-converted
/// identifier does not.
/// </para>
/// <para>
/// <b>Base64 of a compact string rather than JSON</b>, because a token that is readable is a token
/// clients parse, and a parsed token is a contract nobody meant to freeze.
/// </para>
/// <para>
/// <b>B15's older reads take <c>before</c> and <c>excludeIds</c> as query parameters instead.</b>
/// That was the shape B15 shipped, the client is written against it, and changing it would be a
/// breaking change to close a gap that did not ask for one. The register's campaign routes ask for
/// <c>cursor</c>, so the new reads use this and the old ones are left alone.
/// </para>
/// </summary>
public sealed record GivingCursor
{
    private const char Separator = '|';
    private const char IdSeparator = ',';

    /// <summary>How many tie identifiers a token will carry before it gives up on the group.</summary>
    private const int MaxBoundaryIds = 64;

    private GivingCursor(DateTimeOffset boundary, IReadOnlyList<Guid> seen, bool isExhausted)
    {
        Boundary = boundary;
        Seen = seen;
        IsExhausted = isExhausted;
    }

    /// <summary>The last row's sort instant.</summary>
    public DateTimeOffset Boundary { get; }

    /// <summary>The identifiers already served at <see cref="Boundary"/>.</summary>
    public IReadOnlyList<Guid> Seen { get; }

    /// <summary>
    /// Whether the tie group at the boundary was abandoned because it exceeded
    /// <see cref="MaxBoundaryIds"/>.
    /// <para>
    /// A tie group larger than the token can carry is a real possibility — a bulk import stamps
    /// hundreds of rows with one instant — and the honest answer is to skip past the instant
    /// entirely rather than serve part of it for ever. Recorded rather than silent, so the read can
    /// say so.
    /// </para>
    /// </summary>
    public bool IsExhausted { get; }

    /// <summary>
    /// Parses a token, returning null on anything malformed.
    /// <para>
    /// Null rather than throwing: a stale or hand-edited cursor from an old app version is an
    /// ordinary event, and answering a browse screen with the first page beats answering it with a
    /// 400.
    /// </para>
    /// </summary>
    public static GivingCursor? Parse(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token.Trim()));
            var parts = decoded.Split(Separator);

            if (parts.Length < 1
                || !long.TryParse(parts[0], CultureInfo.InvariantCulture, out var ticks))
            {
                return null;
            }

            var boundary = new DateTimeOffset(ticks, TimeSpan.Zero);
            var marker = parts.Length > 1 ? parts[1] : string.Empty;

            if (string.Equals(marker, "*", StringComparison.Ordinal))
            {
                return new GivingCursor(boundary, [], true);
            }

            var ids = marker
                .Split(IdSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => Guid.TryParse(part, out var id) ? id : (Guid?)null)
                .Where(id => id is not null)
                .Select(id => id!.Value)
                .ToList();

            return new GivingCursor(boundary, ids, false);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Builds the token for the next page from the page just served.
    /// <para>
    /// The identifiers carried forward are those at the new boundary instant only — including any
    /// the incoming cursor already carried, when the boundary has not moved. Carrying the whole page
    /// would grow the token without bound on a busy cause.
    /// </para>
    /// </summary>
    public static string Encode<T>(
        IReadOnlyList<T> page,
        Func<T, DateTimeOffset> sortKey,
        Func<T, Guid> identifier,
        GivingCursor? incoming)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(sortKey);
        ArgumentNullException.ThrowIfNull(identifier);

        var boundary = sortKey(page[^1]);

        var atBoundary = page
            .Where(row => sortKey(row) == boundary)
            .Select(identifier)
            .ToList();

        if (incoming is { IsExhausted: false } && incoming.Boundary == boundary)
        {
            atBoundary = [.. incoming.Seen, .. atBoundary];
        }

        var distinct = atBoundary.Distinct().ToList();

        var marker = distinct.Count > MaxBoundaryIds
            ? "*"
            : string.Join(IdSeparator, distinct);

        var payload = string.Concat(
            boundary.UtcTicks.ToString(CultureInfo.InvariantCulture),
            Separator,
            marker);

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    /// <summary>
    /// The instant a repository should filter at, given the cursor.
    /// <para>
    /// When the tie group was abandoned the boundary itself is excluded — one tick earlier — because
    /// serving the rest of a group the token can no longer describe would repeat rows for ever.
    /// </para>
    /// </summary>
    public DateTimeOffset FilterBoundary =>
        IsExhausted ? Boundary.AddTicks(-1) : Boundary;

    /// <summary>The identifiers to exclude. Empty once the tie group has been skipped past.</summary>
    public IReadOnlyList<Guid> ExcludedIds => IsExhausted ? [] : Seen;
}
