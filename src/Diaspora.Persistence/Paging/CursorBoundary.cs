using System.Globalization;

namespace Diaspora.Persistence.Paging;

/// <summary>
/// What the next page must skip at the boundary, and whether there is any point trying.
/// <para>
/// Returned by <see cref="CursorBoundary.Exclusion{TId}"/> and consumed directly in the query
/// predicate. Both members are needed: <see cref="Ids"/> excludes the rows already returned at the
/// boundary, and <see cref="IsExhausted"/> says the tie group grew past what a cursor can carry and
/// the rest of it is being abandoned.
/// </para>
/// <para>
/// <b>Reading one without the other is the defect this type exists to make impossible.</b> The
/// helper used to return the identifiers alone, so an exhausted boundary was indistinguishable
/// from an empty one — and an empty one means "exclude nothing", which re-serves the whole tie
/// group and hands the client the same page forever.
/// </para>
/// </summary>
public sealed class CursorExclusion<TId>
{
    internal CursorExclusion(IReadOnlyList<TId> ids, bool isExhausted)
    {
        Ids = ids;
        IsExhausted = isExhausted;
    }

    /// <summary>The identifiers already returned at the boundary sort key.</summary>
    public IReadOnlyList<TId> Ids { get; }

    /// <summary>
    /// Whether the tie group overflowed <see cref="CursorBoundary.MaxBoundaryIds"/> and paging must
    /// resume strictly after the boundary, abandoning whatever is left of it.
    /// <para>
    /// The predicate has to consult this. Excluding an empty set is not the same as skipping the
    /// group — it is the opposite.
    /// </para>
    /// </summary>
    public bool IsExhausted { get; }
}

/// <summary>
/// The tie-break for cursor paging over a sort key that is not unique — a timestamp, a score, a
/// rating.
/// <para>
/// <b>The problem.</b> A cursor that resumes with a strict comparison on the sort key alone
/// (<c>WHERE CreatedAt &lt; @after</c>) silently drops every row sharing the boundary value. It
/// is not a rare edge: <c>CreatedAt</c> is stamped by the shared persistence interceptor, so
/// every row written in one unit of work carries an identical instant, and two writes inside one
/// clock tick — about fifteen milliseconds on Windows — tie anyway. B9 measured it on a
/// thousand-row applicant list: nineteen applicants vanished, and the person never seen has no
/// way of knowing.
/// </para>
/// <para>
/// <b>Why not the textbook keyset.</b> The correct predicate is
/// <c>key &lt; @after OR (key = @after AND id &lt; @lastId)</c>. It cannot be written in this
/// codebase: identifiers are value-converted <see cref="SharedKernel.Primitives.StronglyTypedId"/>s,
/// and no provider translates an ordering comparison on one — <c>Guid.CompareTo</c> fails
/// outright on SQLite, and <c>EF.Property&lt;Guid&gt;</c> has no C# ordering operator to offer.
/// Ordering on such a column translates fine (<c>ThenByDescending(x =&gt; x.Id)</c> is an ORDER
/// BY on the column); only the comparison does not.
/// </para>
/// <para>
/// <b>What this does instead.</b> The cursor carries the identifiers already returned at the
/// boundary, and the query excludes them: <c>key &lt; @after OR (key = @after AND id NOT IN
/// (…))</c>. <c>Contains</c> over value-converted ids translates on every provider.
/// </para>
/// <para>
/// <b>The set accumulates while the boundary does not move.</b> Usually it holds one identifier
/// and empties as soon as the next page reaches a new key. Carrying only the last page's
/// identifiers is not enough: a tie group wider than a page would hand page one's rows back on
/// page three, which is how the first attempt at this failed.
/// </para>
/// <para>
/// <b>Past <see cref="MaxBoundaryIds"/> the group is abandoned rather than truncated, and
/// B1.3 changed that.</b> The documented behaviour was always to degrade to a strict comparison
/// — lossy but terminating. The implementation truncated the list instead, which is neither: the
/// rows past the cap were not excluded, so they came back on the next page, which produced the same
/// boundary, which produced the same page. A client scrolling an absurdly large tie group would
/// have scrolled it forever. The cursor now carries <see cref="ExhaustedMarker"/> in place of the
/// list, <see cref="CursorExclusion{TId}.IsExhausted"/> reports it, and the query resumes strictly
/// after the boundary. That loses the tail of one such group — bounded, and terminating, which is
/// what the documentation always claimed.
/// </para>
/// </summary>
public static class CursorBoundary
{
    /// <summary>
    /// Roughly 18KB of base64 once encoded. Far beyond any real tie group, and small enough that
    /// a token this size is a header rather than a problem.
    /// </summary>
    public const int MaxBoundaryIds = 500;

    /// <summary>
    /// What a cursor carries instead of identifiers once the tie group is abandoned.
    /// <para>
    /// A tilde, because the identifiers are 32-character hexadecimal strings and this can never be
    /// mistaken for one — including by a client that started parsing tokens it was asked not to.
    /// </para>
    /// </summary>
    public const string ExhaustedMarker = "~";

    private const char Separator = ',';

    /// <summary>
    /// The identifiers the next page must skip: those on this page sharing its last sort key,
    /// plus any the previous cursor already carried for that same key.
    /// </summary>
    /// <param name="rows">The page, already trimmed to its final length and in sort order.</param>
    /// <param name="sortKey">The value the query ordered by.</param>
    /// <param name="id">The row's identifier.</param>
    /// <param name="previousKey">
    /// The boundary the incoming cursor carried, or null on a first page.
    /// </param>
    /// <param name="previousSortKey">
    /// The sort key the incoming cursor carried. Identifiers are carried forward only when this
    /// equals the new boundary — a new key makes every earlier identifier irrelevant, and
    /// keeping them would grow the token for nothing.
    /// </param>
    public static string Key<T, TKey>(
        IReadOnlyList<T> rows,
        Func<T, TKey> sortKey,
        Func<T, Guid> id,
        string? previousKey = null,
        TKey? previousSortKey = default)
        where TKey : struct, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(sortKey);
        ArgumentNullException.ThrowIfNull(id);

        if (rows.Count == 0)
        {
            return string.Empty;
        }

        var boundary = sortKey(rows[^1]);

        var carriedForward = previousSortKey is { } carried
            && carried.Equals(boundary)
            && !string.IsNullOrWhiteSpace(previousKey);

        // Already abandoned, and the boundary has not moved: stay abandoned. Starting to collect
        // identifiers again here would exclude the rows of one page out of a group whose earlier
        // pages were never excluded, which is worse than either behaviour on its own.
        if (carriedForward && IsExhausted(previousKey))
        {
            return ExhaustedMarker;
        }

        var keys = new List<string>();

        if (carriedForward)
        {
            keys.AddRange(
                previousKey!.Split(Separator, StringSplitOptions.RemoveEmptyEntries));
        }

        foreach (var row in rows)
        {
            if (sortKey(row).Equals(boundary))
            {
                keys.Add(id(row).ToString("N", CultureInfo.InvariantCulture));
            }
        }

        return keys.Count > MaxBoundaryIds
            ? ExhaustedMarker
            : string.Join(Separator, keys);
    }

    /// <summary>
    /// Reads a cursor's boundary back: the identifiers to exclude, and whether the tie group was
    /// abandoned.
    /// <para>
    /// Anything malformed yields an empty, unexhausted exclusion — which excludes nothing and
    /// therefore serves the boundary's tie group again. That is a bounded, one-off repeat rather
    /// than an error, and it is the right trade: a stale or hand-edited cursor is an ordinary event
    /// — an old app version, a shared link, a truncated URL — and answering an infinite scroll
    /// with a 400 is far worse than answering it with rows the client has already seen. The
    /// alternative, skipping the group, would lose rows nobody can ask for again.
    /// </para>
    /// </summary>
    public static CursorExclusion<TId> Exclusion<TId>(string? boundaryKey, Func<Guid, TId> from)
    {
        ArgumentNullException.ThrowIfNull(from);

        if (IsExhausted(boundaryKey))
        {
            return new CursorExclusion<TId>([], isExhausted: true);
        }

        var ids = new List<TId>();

        if (string.IsNullOrWhiteSpace(boundaryKey))
        {
            return new CursorExclusion<TId>(ids, isExhausted: false);
        }

        foreach (var part in boundaryKey.Split(Separator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (Guid.TryParse(part, out var id))
            {
                ids.Add(from(id));
            }
        }

        return new CursorExclusion<TId>(ids, isExhausted: false);
    }

    /// <summary>The exclusion for entities keyed on a raw <see cref="Guid"/>.</summary>
    public static CursorExclusion<Guid> Exclusion(string? boundaryKey) =>
        Exclusion(boundaryKey, id => id);

    private static bool IsExhausted(string? boundaryKey) =>
        string.Equals(boundaryKey?.Trim(), ExhaustedMarker, StringComparison.Ordinal);
}
