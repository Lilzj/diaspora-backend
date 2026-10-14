using Diaspora.SharedKernel.Geography;

namespace Diaspora.Modules.Location.Extensibility;

/// <summary>
/// Implemented by a vertical to say how much of it sits within a circle around a point.
/// <para>
/// <b>Phase 2A of retiring the place catalogue (14 Oct 2026).</b> The owner chose "the area around a
/// point" in place of named catalogue neighbourhoods: a page describes what is within R km of a
/// Google place the member picked, or of their own location, in any country. The catalogue-shaped
/// <see cref="ILocationResourceCounter"/> counts what is filed under a node; this counts rows by
/// their own coordinates, so it answers for a place the catalogue never held.
/// </para>
/// <para>
/// <b>Exact circle, not its rectangle.</b> An implementation narrows with the rectangle around the
/// circle over its indexed coordinate columns and then keeps only the rows
/// <see cref="CircleArea.Contains"/> admits. A row with no coordinates is never counted: nobody can
/// say it is within anything.
/// </para>
/// <para>
/// Discovered by assembly scan, like every other Location seam — a vertical appears on the area page
/// the moment its assembly ships one.
/// </para>
/// </summary>
public interface ICircleResourceCounter
{
    /// <summary>
    /// The key the count appears under: <c>housing</c>, <c>marketplace</c>, <c>jobs</c>,
    /// <c>events</c>, <c>updates</c> — the same keys <see cref="ILocationResourceCounter"/> uses.
    /// </summary>
    string ResourceType { get; }

    /// <summary>How many of this vertical's publicly visible things sit within the circle.</summary>
    Task<CircleCount> CountWithinAsync(CircleArea circle, CancellationToken cancellationToken = default);
}

/// <summary>
/// A count within a circle, and whether it stopped short.
/// </summary>
/// <param name="Count">Rows within the circle that were counted.</param>
/// <param name="IsLowerBound">
/// True when the vertical stopped reading at its cap, so the true figure is at least
/// <paramref name="Count"/>. A client says "200+" rather than "200".
/// </param>
public sealed record CircleCount(int Count, bool IsLowerBound)
{
    /// <summary>
    /// The most rows a counter reads for one circle. Far beyond what the platform holds within ten
    /// kilometres of anywhere today, and a bound on what one request can load.
    /// </summary>
    public const int MaximumRowsRead = 1_000;

    /// <summary>A count of rows measured from at most <see cref="MaximumRowsRead"/> read.</summary>
    public static CircleCount Of(int count, int rowsRead) =>
        new(count, rowsRead >= MaximumRowsRead);
}
