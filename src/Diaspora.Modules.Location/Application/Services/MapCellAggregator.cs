using System.Globalization;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Search;
using Diaspora.Modules.Location.Domain.ValueObjects;
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
/// <see cref="MapCellAggregator.MinimumMembersForCentroid"/>, otherwise the cell's centre — so a
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
public sealed record MapCell(
    string Key,
    Guid Id,
    double Latitude,
    double Longitude,
    double South,
    double West,
    double North,
    double East,
    string Label,
    IReadOnlyDictionary<string, int> Counts,
    int Total);

/// <summary>The cells for a viewport, with everything a caller needs to describe what it got.</summary>
public sealed record MapCellPage
{
    public required IReadOnlyList<MapCell> Cells { get; init; }

    public required BoundingBox Bounds { get; init; }

    /// <summary>The granularity asked for: <c>Neighborhood</c> (finer) or <c>City</c> (coarser).</summary>
    public required AdministrativeLevel Level { get; init; }

    /// <summary>The side of every cell, in degrees of latitude and of longitude.</summary>
    public required double CellSizeDegrees { get; init; }

    public required IReadOnlyList<string> ResourceTypes { get; init; }

    public required int MinimumCellCount { get; init; }

    public required int SuppressedCells { get; init; }

    /// <summary>
    /// True when at least one vertical had more rows in the viewport than it reads for one
    /// request, so the totals are lower bounds. Zooming in makes them exact.
    /// </summary>
    public required bool Truncated { get; init; }
}

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
/// <see cref="CityDivisions"/> cells across for <c>City</c>, <see cref="NeighborhoodDivisions"/>
/// for <c>Neighborhood</c> — or from a size the client names.
/// </para>
/// <para>
/// <b>Still the one aggregation behind clusters and the heat layer</b>, with the one suppression
/// floor (<c>Location:Spatial:MinimumClusterCellCount</c>), for the reasons the original extraction
/// gave: two privacy thresholds that disagree are worse than either.
/// </para>
/// </summary>
public sealed class MapCellAggregator
{
    /// <summary>The most cells one response carries. The cell side doubles until the grid fits.</summary>
    public const int MaxCells = 256;

    /// <summary>Cells across the wider side of the viewport at <c>City</c> granularity.</summary>
    public const int CityDivisions = 6;

    /// <summary>Cells across the wider side of the viewport at <c>Neighborhood</c> granularity.</summary>
    public const int NeighborhoodDivisions = 12;

    /// <summary>
    /// Fewer rows than this in a cell and the marker goes at the cell's centre rather than at the
    /// rows' mean — with one or two rows the mean is a row's own pin, or the midpoint of two.
    /// </summary>
    public const int MinimumMembersForCentroid = 3;

    /// <summary>The finest cell side: 2^-12° ≈ 27 m of latitude.</summary>
    public const int MinimumExponent = -12;

    /// <summary>The coarsest cell side: 2^5° = 32°.</summary>
    public const int MaximumExponent = 5;

    private const double KilometresPerDegree = 111.32d;

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
    public static AdministrativeLevel ParseLevel(string? value) =>
        Enum.TryParse<AdministrativeLevel>(value, ignoreCase: true, out var parsed)
        && parsed is AdministrativeLevel.Neighborhood or AdministrativeLevel.City
            ? parsed
            : AdministrativeLevel.Neighborhood;

    public async Task<Result<MapCellPage>> AggregateAsync(
        string bounds,
        string? level,
        IReadOnlyCollection<string>? resourceTypes,
        string? countryCode,
        double? cellSizeKm = null,
        CancellationToken cancellationToken = default)
    {
        var parsed = BoundingBox.TryParse(bounds);

        if (parsed.IsFailure)
        {
            return Result.Failure<MapCellPage>(parsed.Error);
        }

        var box = parsed.Value;

        // The same continental cap every viewport read on this module applies: wider than that is
        // a request to browse by country, not a map.
        if (box.North - box.South > BoundingBoxSearch.MaxLatitudeSpan)
        {
            return Result.Failure<MapCellPage>(Error.Validation(
                LocationErrorCodes.BoundsInvalid,
                $"A viewport may not span more than {BoundingBoxSearch.MaxLatitudeSpan} degrees of "
                + "latitude. Zoom in, or browse by country."));
        }

        var rung = ParseLevel(level);

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

        var exponent = ChooseExponent(box, rung, cellSizeKm);
        var size = Math.Pow(2d, exponent);

        var points = await _counts.ListPointsAsync(
            new MapPointQuery
            {
                South = box.South,
                West = box.West,
                North = box.North,
                East = box.East,
                CountryCode = country,
                Limit = MapPointReader.MaximumRowsRead
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
                var key = (Row: (long)Math.Floor(point.Latitude / size),
                    Column: (long)Math.Floor(point.Longitude / size));

                if (!buckets.TryGetValue(key, out var bucket))
                {
                    bucket = new Bucket();
                    buckets[key] = bucket;
                }

                bucket.Add(resourceType, point);
            }
        }

        var cells = new List<MapCell>(buckets.Count);
        var suppressed = 0;

        foreach (var ((row, column), bucket) in buckets)
        {
            if (bucket.Total < minimum)
            {
                suppressed++;
                continue;
            }

            cells.Add(ToCell(exponent, size, row, column, bucket, answered));
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
            SuppressedCells = suppressed,
            Truncated = points.Values.Any(page => page.IsTruncated)
        });
    }

    /// <summary>
    /// The cell side, as a power of two degrees: from a size the client named, else from the
    /// viewport's span; then doubled until the viewport holds no more than <see cref="MaxCells"/>.
    /// </summary>
    public static int ChooseExponent(BoundingBox box, AdministrativeLevel level, double? cellSizeKm = null)
    {
        ArgumentNullException.ThrowIfNull(box);

        var latitudeSpan = Math.Max(box.North - box.South, 1e-9);
        var longitudeSpan = Math.Max(
            box.CrossesAntimeridian ? 360d - (box.West - box.East) : box.East - box.West,
            1e-9);

        var target = cellSizeKm is { } km && km > 0 && double.IsFinite(km)
            ? km / KilometresPerDegree
            : Math.Max(latitudeSpan, longitudeSpan)
              / (level == AdministrativeLevel.City ? CityDivisions : NeighborhoodDivisions);

        var exponent = (int)Math.Ceiling(Math.Log2(target));
        exponent = Math.Clamp(exponent, MinimumExponent, MaximumExponent);

        while (exponent < MaximumExponent && CellsAcross(latitudeSpan, longitudeSpan, exponent) > MaxCells)
        {
            exponent++;
        }

        return exponent;
    }

    private static double CellsAcross(double latitudeSpan, double longitudeSpan, int exponent)
    {
        var size = Math.Pow(2d, exponent);

        // +1 on each axis: a viewport rarely starts on a cell boundary.
        return (Math.Ceiling(latitudeSpan / size) + 1) * (Math.Ceiling(longitudeSpan / size) + 1);
    }

    private static MapCell ToCell(
        int exponent,
        double size,
        long row,
        long column,
        Bucket bucket,
        IReadOnlyList<string> answered)
    {
        var south = Math.Max(row * size, -90d);
        var north = Math.Min((row + 1) * size, 90d);
        var west = Math.Max(column * size, -180d);
        var east = Math.Min((column + 1) * size, 180d);

        var centreLatitude = (south + north) / 2d;
        var centreLongitude = (west + east) / 2d;

        var (latitude, longitude) = bucket.Total >= MinimumMembersForCentroid
            ? (bucket.LatitudeSum / bucket.Total, bucket.LongitudeSum / bucket.Total)
            : (centreLatitude, centreLongitude);

        // A vertical that answered with nothing in this cell is a zero; one that did not answer
        // at all is an absent key. The difference is the whole point of the counts contract.
        var counts = answered.ToDictionary(
            type => type,
            type => bucket.Counts.TryGetValue(type, out var count) ? count : 0,
            StringComparer.Ordinal);

        return new MapCell(
            Key: string.Create(CultureInfo.InvariantCulture, $"{exponent}:{row}:{column}"),
            Id: CellId(exponent, row, column),
            Latitude: Math.Round(latitude, 6),
            Longitude: Math.Round(longitude, 6),
            South: south,
            West: west,
            North: north,
            East: east,
            Label: Describe(centreLatitude, centreLongitude),
            Counts: counts,
            Total: bucket.Total);
    }

    /// <summary>A deterministic GUID for a cell, so the same cell has the same id on every request.</summary>
    public static Guid CellId(int exponent, long row, long column)
    {
        var tail = BitConverter.GetBytes(column);

        return new Guid((int)row, (short)exponent, (short)(row >> 32), tail);
    }

    private static string Describe(double latitude, double longitude) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Area around {Math.Abs(latitude):0.00}°{(latitude < 0 ? "S" : "N")}, "
            + $"{Math.Abs(longitude):0.00}°{(longitude < 0 ? "W" : "E")}");

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
