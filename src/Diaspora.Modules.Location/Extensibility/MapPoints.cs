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

    /// <summary>
    /// Added 21 Oct 2026, for the heat layer's time range. When set, only rows whose own time — the
    /// one the vertical names for the map: when a listing or post was published, when an event
    /// starts — falls on or after this instant are returned.
    /// <para>
    /// <b>A row with no time is outside every window.</b> A filter that let undated rows through
    /// would answer "the last 24 hours" with everything nobody dated.
    /// </para>
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>The window's other end, inclusive. See <see cref="From"/>.</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>Whether the caller asked for a time window at all.</summary>
    public bool HasWindow => From is not null || To is not null;

    public bool CrossesAntimeridian => West > East;

    /// <summary>
    /// Whether a row's time is inside the window. Always true when there is no window; false for an
    /// undated row when there is one.
    /// </summary>
    public bool IsWithinWindow(DateTimeOffset? at) =>
        !HasWindow
        || (at is { } time
            && (From is null || time >= From.Value)
            && (To is null || time <= To.Value));

    /// <summary>Whether a point lies inside the box, honouring the ±180° wrap.</summary>
    public bool Contains(double latitude, double longitude) =>
        latitude >= South && latitude <= North
        && (CrossesAntimeridian
            ? longitude >= West || longitude <= East
            : longitude >= West && longitude <= East);
}

/// <summary>One row's position, as a stranger would be shown it.</summary>
/// <param name="Latitude">The published latitude.</param>
/// <param name="Longitude">The published longitude.</param>
/// <param name="At">
/// Added 21 Oct 2026. The row's own time for the map's time window — published, or for an event its
/// start — when the vertical supplies one. Null otherwise.
/// </param>
public readonly record struct MapPoint(double Latitude, double Longitude, DateTimeOffset? At = null);

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
    public static Task<MapPointPage> ReadAsync<T>(
        MapPointQuery query,
        Func<string?, int, CancellationToken, Task<(IReadOnlyList<T> Items, string? NextCursor)>> fetch,
        Func<T, (double? Latitude, double? Longitude)> position,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        ReadAsync(query, fetch, position, static _ => null, pageSize, cancellationToken);

    /// <summary>
    /// Reads cursor pages until the cap or the last page, keeping only rows whose time is inside the
    /// query's window (21 Oct 2026).
    /// </summary>
    /// <param name="query">The viewport, and optionally a time window.</param>
    /// <param name="fetch">One page: (cursor, page size) to (rows, next cursor or null).</param>
    /// <param name="position">A row's published coordinates; either half null means unpinned.</param>
    /// <param name="time">The row's own time for the map's window: published, or an event's start.</param>
    /// <param name="pageSize">The vertical's own maximum page size.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public static async Task<MapPointPage> ReadAsync<T>(
        MapPointQuery query,
        Func<string?, int, CancellationToken, Task<(IReadOnlyList<T> Items, string? NextCursor)>> fetch,
        Func<T, (double? Latitude, double? Longitude)> position,
        Func<T, DateTimeOffset?> time,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(time);
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
                Collect(points, query, position(item), time(item));
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
        Func<T, (double? Latitude, double? Longitude)> position) =>
        FromList(query, rows, position, static _ => null);

    /// <summary>
    /// <see cref="FromList{T}(MapPointQuery, IReadOnlyList{T}, Func{T, ValueTuple{double?, double?}})"/>
    /// with each row's time, so the query's window applies (21 Oct 2026).
    /// </summary>
    public static MapPointPage FromList<T>(
        MapPointQuery query,
        IReadOnlyList<T> rows,
        Func<T, (double? Latitude, double? Longitude)> position,
        Func<T, DateTimeOffset?> time)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(time);

        var limit = Math.Clamp(query.Limit, 1, MaximumRowsRead);
        var points = new List<MapPoint>();

        foreach (var row in rows.Take(limit))
        {
            Collect(points, query, position(row), time(row));
        }

        return new MapPointPage(points, rows.Count > limit);
    }

    private static void Collect(
        List<MapPoint> points,
        MapPointQuery query,
        (double? Latitude, double? Longitude) position,
        DateTimeOffset? at)
    {
        if (position is { Latitude: { } latitude, Longitude: { } longitude }
            && double.IsFinite(latitude)
            && double.IsFinite(longitude)
            && query.Contains(latitude, longitude)
            && query.IsWithinWindow(at))
        {
            points.Add(new MapPoint(latitude, longitude, at));
        }
    }
}
