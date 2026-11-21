using Diaspora.Contracts.Location.V1;
using Diaspora.Modules.Location.Integration;
using System.Globalization;
using Diaspora.Modules.Location.Domain.Search;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Location.Application.Services;

/// <summary>
/// One square grid cell inside a viewport, with what every vertical has in it.
/// </summary>
/// <param name="Key">
/// Stable across requests and pans: <c>{exponent}:{row}:{column}</c> on a world grid anchored at
/// 0°,0° whose cell side is 2^exponent degrees.
/// </param>
/// <param name="Id">The same key as a GUID, for clients that key markers by an identifier.</param>
/// <param name="Latitude">
/// Where the marker goes: the mean of the rows in the cell when it holds at least
/// <see cref="MapGrid.MinimumMembersForCentroid"/>, otherwise the cell's centre — so a
/// marker never sits on one row's pin.
/// </param>
/// <param name="Longitude">See <paramref name="Latitude"/>.</param>
/// <param name="South">The cell's southern edge.</param>
/// <param name="West">The cell's western edge.</param>
/// <param name="North">The cell's northern edge.</param>
/// <param name="East">The cell's eastern edge.</param>
/// <param name="Label">A plain description of where the cell is, for an accessible label.</param>
/// <param name="Counts">Per-resource counts. An absent key means that vertical could not be asked.</param>
/// <param name="Total">The sum across the counted verticals.</param>


/// <summary>
/// Where one grid cell is, without anything about what is in it (21 Oct 2026).
/// <para>
/// What a withheld cell is published as: its place on the grid and nothing else, so a client can
/// say "withheld here" without the count that would defeat the floor.
/// </para>
/// </summary>


/// <summary>A viewport parsed and fitted to the world grid: the box, the rung and the cell side.</summary>


/// <summary>The cells for a viewport, with everything a caller needs to describe what it got.</summary>


/// <summary>
/// Aggregates a viewport into square grid cells over the rows' own coordinates.
/// <para>
/// <b>Phase 2B of retiring the place catalogue (14 Oct 2026).</b> The cells used to be catalogue
/// nodes with per-node counts, which put nothing on the map in a country the catalogue never held
/// and put every unfiled row nowhere. Now every vertical lists the points it has inside the
/// viewport (<see cref="ILocationResourceCounter.ListPointsAsync"/>) and this class buckets them.
/// A row with no coordinates is not on the map.
/// </para>
/// <para>
/// <b>The grid is a world grid, not a viewport grid.</b> Cells are anchored at 0°,0° with a side of
/// a power of two degrees, so panning never moves a cell boundary and the same place lands in the
/// same cell on every request. The side is chosen from the viewport's span — about
/// <see cref="MapGrid.CityDivisions"/> cells across for <c>City</c>, <see cref="MapGrid.NeighborhoodDivisions"/>
/// for <c>Neighborhood</c> — or from a size the client names.
/// </para>
/// <para>
/// <b>Still the one aggregation behind clusters and the heat layer</b>, with the one suppression
/// floor (<c>Location:Spatial:MinimumClusterCellCount</c>), for the reasons the original extraction
/// gave: two privacy thresholds that disagree are worse than either.
/// </para>
/// </summary>
public sealed class MapCellAggregator : IMapCells
{
    /// <summary>The most cells one response carries. The cell side doubles until the grid fits.</summary>


    /// <summary>Cells across the wider side of the viewport at <c>City</c> granularity.</summary>


    /// <summary>Cells across the wider side of the viewport at <c>Neighborhood</c> granularity.</summary>


    /// <summary>
    /// Fewer rows than this in a cell and the marker goes at the cell's centre rather than at the
    /// rows' mean — with one or two rows the mean is a row's own pin, or the midpoint of two.
    /// </summary>


    /// <summary>The finest cell side: 2^-12° ≈ 27 m of latitude.</summary>


    /// <summary>The coarsest cell side: 2^5° = 32°.</summary>




    private readonly ResourceCountAggregator _counts;
    private readonly IOptions<LocationSpatialOptions> _options;

    public MapCellAggregator(
        ResourceCountAggregator counts,
        IOptions<LocationSpatialOptions> options)
    {
        _counts = counts;
        _options = options;
    }

    /// <summary>The granularity a caller may ask for, defaulting to the finer one.</summary>


    public Task<Result<MapCellPage>> AggregateAsync(
        string bounds,
        string? level,
        IReadOnlyCollection<string>? resourceTypes,
        string? countryCode,
        double? cellSizeKm = null,
        CancellationToken cancellationToken = default) =>
        AggregateAsync(bounds, level, resourceTypes, countryCode, cellSizeKm, null, null, cancellationToken);

    /// <summary>
    /// Parses a viewport and fits it to the world grid, with the same cap every aggregation here
    /// applies. Public so a layer that buckets its own points (a rent, a safety report) lands them
    /// in exactly the cells the counts use (21 Oct 2026).
    /// </summary>


    /// <summary>The cell a point falls in, on the grid whose side is <paramref name="size"/> degrees.</summary>


    /// <summary>A cell's place on the grid: key, id, edges and a plain label.</summary>


    /// <summary>
    /// Where a cell's marker goes: the mean of its rows when there are at least
    /// <see cref="MapGrid.MinimumMembersForCentroid"/>, otherwise the cell's centre.
    /// </summary>


    /// <summary>
    /// The cells for a viewport, counting only rows whose own time is inside
    /// [<paramref name="from"/>, <paramref name="to"/>] when either is given (21 Oct 2026, the heat
    /// layer's time range). A vertical that supplies no time for its rows contributes nothing to a
    /// windowed read.
    /// </summary>
    public async Task<Result<MapCellPage>> AggregateAsync(
        string bounds,
        string? level,
        IReadOnlyCollection<string>? resourceTypes,
        string? countryCode,
        double? cellSizeKm,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var plan = MapGrid.Plan(bounds, level, cellSizeKm);

        if (plan.IsFailure)
        {
            return Result.Failure<MapCellPage>(plan.Error);
        }

        var box = plan.Value.Box;
        var rung = plan.Value.Level;

        var country = string.IsNullOrWhiteSpace(countryCode)
            ? null
            : CountryCode.TryCreate(countryCode) is { IsSuccess: true } code
                ? code.Value.Value
                : null;

        var requested = resourceTypes is { Count: > 0 }
            ? resourceTypes
                .Select(type => type.Trim())
                .Where(type => type.Length > 0)
                .ToList()
            : null;

        var exponent = plan.Value.Exponent;
        var size = plan.Value.CellSizeDegrees;

        var points = await _counts.ListPointsAsync(
            new MapPointQuery
            {
                South = box.South,
                West = box.West,
                North = box.North,
                East = box.East,
                CountryCode = country,
                Limit = MapPointReader.MaximumRowsRead,
                From = from,
                To = to
            },
            requested,
            cancellationToken);

        var minimum = Math.Max(1, _options.Value.MinimumClusterCellCount);
        var answered = points.Keys.ToList();

        var buckets = new Dictionary<(long Row, long Column), Bucket>();

        foreach (var (resourceType, page) in points)
        {
            foreach (var point in page.Points)
            {
                var key = MapGrid.CellOf(size, point.Latitude, point.Longitude);

                if (!buckets.TryGetValue(key, out var bucket))
                {
                    bucket = new Bucket();
                    buckets[key] = bucket;
                }

                bucket.Add(resourceType, point);
            }
        }

        var cells = new List<MapCell>(buckets.Count);
        var withheld = new List<MapCellFrame>();

        foreach (var ((row, column), bucket) in buckets)
        {
            if (bucket.Total < minimum)
            {
                withheld.Add(MapGrid.Frame(exponent, row, column));
                continue;
            }

            cells.Add(ToCell(exponent, row, column, bucket, answered));
        }

        return Result.Success(new MapCellPage
        {
            Cells =
            [
                .. cells
                    .OrderByDescending(cell => cell.Total)
                    .ThenBy(cell => cell.Key, StringComparer.Ordinal)
            ],
            Bounds = box,
            Level = rung,
            CellSizeDegrees = size,
            ResourceTypes = requested ?? _counts.ResourceTypes,
            MinimumCellCount = minimum,
            SuppressedCells = withheld.Count,
            WithheldCells = [.. withheld.OrderBy(frame => frame.Key, StringComparer.Ordinal)],
            Truncated = points.Values.Any(page => page.IsTruncated)
        });
    }

    /// <summary>
    /// The cell side, as a power of two degrees: from a size the client named, else from the
    /// viewport's span; then doubled until the viewport holds no more than <see cref="MapGrid.MaxCells"/>.
    /// </summary>




    private static MapCell ToCell(
        int exponent,
        long row,
        long column,
        Bucket bucket,
        IReadOnlyList<string> answered)
    {
        var frame = MapGrid.Frame(exponent, row, column);
        var (latitude, longitude) = MapGrid.Marker(frame, bucket.Total, bucket.LatitudeSum, bucket.LongitudeSum);

        // A vertical that answered with nothing in this cell is a zero; one that did not answer
        // at all is an absent key. The difference is the whole point of the counts contract.
        var counts = answered.ToDictionary(
            type => type,
            type => bucket.Counts.TryGetValue(type, out var count) ? count : 0,
            StringComparer.Ordinal);

        return new MapCell(
            Key: frame.Key,
            Id: frame.Id,
            Latitude: latitude,
            Longitude: longitude,
            South: frame.South,
            West: frame.West,
            North: frame.North,
            East: frame.East,
            Label: frame.Label,
            Counts: counts,
            Total: bucket.Total);
    }

    /// <summary>A deterministic GUID for a cell, so the same cell has the same id on every request.</summary>




    private sealed class Bucket
    {
        public Dictionary<string, int> Counts { get; } = new(StringComparer.Ordinal);

        public int Total { get; private set; }

        public double LatitudeSum { get; private set; }

        public double LongitudeSum { get; private set; }

        public void Add(string resourceType, MapPoint point)
        {
            Counts[resourceType] = Counts.TryGetValue(resourceType, out var count) ? count + 1 : 1;
            Total++;
            LatitudeSum += point.Latitude;
            LongitudeSum += point.Longitude;
        }
    }
}
