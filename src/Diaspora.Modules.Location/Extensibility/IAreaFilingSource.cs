namespace Diaspora.Modules.Location.Extensibility;

/// <summary>
/// Implemented by a vertical whose rows can be filed under an area after they were written.
/// <para>
/// <b>Why this exists at all.</b> Filing on create fixes every row written from now on and none
/// written before it — and before 16 Sep 2026 no row on the platform had ever been filed, so
/// "before" is everything. A one-off script would fix today's rows and leave the next deployment
/// of an older client, or the next import, to recreate the problem silently. A sweep over a seam
/// keeps fixing it, and the vertical that owns the table is the only thing that writes to it.
/// </para>
/// <para>
/// The same contributor shape as <see cref="ILocationResourceCounter"/>, and discovered the same
/// way: Location never learns that Housing exists.
/// </para>
/// </summary>
public interface IAreaFilingSource
{
    /// <summary>The resource type the rows count under — <c>housing</c>, <c>jobs</c>, …</summary>
    string ResourceType { get; }

    /// <summary>
    /// Examines up to <paramref name="limit"/> unfiled rows, starting <paramref name="skip"/> rows
    /// into the unfiled set, and files every one the catalogue can place.
    /// <para>
    /// <b>Why skip rather than a cursor:</b> a filed row leaves the unfiled set, so the rows that
    /// could not be placed are exactly the ones still ahead of the caller. Skipping those, and
    /// only those, reaches the next page without comparing identifiers — which a strongly typed id
    /// cannot do in a query that must also translate on the SQLite test host.
    /// </para>
    /// <para>
    /// Filing is a correction, not an edit: it must not raise an "updated" event, notify a
    /// follower or move a row up a "recently changed" list.
    /// </para>
    /// </summary>
    Task<AreaFilingBatch> FileUnplacedAsync(
        int skip, int limit, CancellationToken cancellationToken = default);
}

/// <summary>What one page of a filing sweep did.</summary>
/// <param name="Examined">Unfiled rows read. Fewer than the limit means the set is exhausted.</param>
/// <param name="Filed">Rows now filed under an area.</param>
public sealed record AreaFilingBatch(int Examined, int Filed)
{
    public static AreaFilingBatch Empty { get; } = new(0, 0);

    /// <summary>Rows read that the catalogue could not place. They stay ahead of the next page.</summary>
    public int Unplaced => Examined - Filed;
}
