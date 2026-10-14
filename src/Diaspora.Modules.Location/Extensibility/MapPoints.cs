namespace Diaspora.Modules.Location.Extensibility;

/// <summary>
/// A map viewport, asked of a vertical as "where are your publicly visible things inside it".
/// <para>
/// <b>Phase 2B of retiring the place catalogue (14 Oct 2026).</b> Map clusters and the heat layer
/// used to count what each vertical had filed under a catalogue node. They now bucket the rows'
/// own coordinates into square grid cells, so a vertical answers with points rather than with a
/// count per node — and a country the catalogue never held is on the map like any other.
/// </para>
/// <para>
/// <b>West greater than East means the box wraps ±180°.</b> The same convention every vertical's
/// own rectangle filter already follows.
/// </para>
/// </summary>
public sealed record MapPointQuery
{
    public required double South { get; init; }

    public required double West { get; init; }

    public required double North { get; init; }

    public required double East { get; init; }

    /// <summary>Optional ISO 3166-1 alpha-2 code narrowing the rows to one country.</summary>
    public string? CountryCode { get; init; }

    /// <summary>
    /// The most rows the vertical reads. Reaching it sets <see cref="MapPointPage.IsTruncated"/>,
    /// so the map can say its totals are lower bounds rather than present them as complete.
    /// </summary>
    public required int Limit { get; init; }

    public bool CrossesAntimeridian => West > East;

    /// <summary>Whether a point lies inside the box, honouring the ±180° wrap.</summary>
    public bool Contains(double latitude, double longitude) =>
        latitude >= South && latitude <= North
        && (CrossesAntimeridian
            ? longitude >= West || longitude <= East
            : longitude >= West && longitude <= East);
}

/// <summary>One row's position, as a stranger would be shown it.</summary>
public readonly record struct MapPoint(double Latitude, double Longitude);

/// <summary>A vertical's points inside a viewport, and whether it stopped reading at its cap.</summary>
public sealed record MapPointPage(IReadOnlyList<MapPoint> Points, bool IsTruncated)
{
    public static MapPointPage Empty { get; } = new([], false);
}

/// <summary>
/// The paging loop every point-listing counter shares, so each vertical states only its filter.
/// <para>
/// <b>Rows without coordinates are not on the map.</b> They are dropped here rather than placed at
/// some stand-in point: a pin for a row whose position nobody recorded would be showing it
/// somewhere it is not. Every point is also re-checked against the box, so a vertical whose filter
/// widens (an unpinned-town fallback, a shipping radius) cannot put a row outside the viewport.
/// </para>
/// </summary>
public static class MapPointReader
{
    /// <summary>The most rows one vertical reads for one viewport.</summary>
    public const int MaximumRowsRead = 1_000;

    /// <summary>
    /// Reads cursor pages until the cap or the last page.
    /// </summary>
    /// <param name="query">The viewport.</param>
    /// <param name="fetch">One page: (cursor, page size) to (rows, next cursor or null).</param>
    /// <param name="position">A row's published coordinates; either half null means unpinned.</param>
    /// <param name="pageSize">The vertical's own maximum page size.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public static async Task<MapPointPage> ReadAsync<T>(
        MapPointQuery query,
        Func<string?, int, CancellationToken, Task<(IReadOnlyList<T> Items, string? NextCursor)>> fetch,
        Func<T, (double? Latitude, double? Longitude)> position,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(fetch);
        ArgumentNullException.ThrowIfNull(position);

        var limit = Math.Clamp(query.Limit, 1, MaximumRowsRead);
        var size = Math.Max(1, pageSize);
        var points = new List<MapPoint>();
        var read = 0;
        string? cursor = null;
        var truncated = false;

        while (true)
        {
            var (items, next) = await fetch(cursor, Math.Min(size, limit - read), cancellationToken);

            foreach (var item in items)
            {
                read++;
                Collect(points, query, position(item));
            }

            if (next is null || items.Count == 0)
            {
                break;
            }

            if (read >= limit)
            {
                truncated = true;
                break;
            }

            cursor = next;
        }

        return new MapPointPage(points, truncated);
    }

    /// <summary>
    /// The points of a list read in one go with one row more than <see cref="MapPointQuery.Limit"/>,
    /// so "there were more" is a fact rather than an inference from a full list.
    /// </summary>
    public static MapPointPage FromList<T>(
        MapPointQuery query,
        IReadOnlyList<T> rows,
        Func<T, (double? Latitude, double? Longitude)> position)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(position);

        var limit = Math.Clamp(query.Limit, 1, MaximumRowsRead);
        var points = new List<MapPoint>();

        foreach (var row in rows.Take(limit))
        {
            Collect(points, query, position(row));
        }

        return new MapPointPage(points, rows.Count > limit);
    }

    private static void Collect(
        List<MapPoint> points, MapPointQuery query, (double? Latitude, double? Longitude) position)
    {
        if (position is { Latitude: { } latitude, Longitude: { } longitude }
            && double.IsFinite(latitude)
            && double.IsFinite(longitude)
            && query.Contains(latitude, longitude))
        {
            points.Add(new MapPoint(latitude, longitude));
        }
    }
}
