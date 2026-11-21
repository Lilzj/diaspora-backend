using Diaspora.Contracts.Location.V1;
using Diaspora.SharedKernel.Results;
using System.Globalization;

namespace Diaspora.Modules.Location.Extensibility;

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

public sealed record MapCellFrame(
    string Key,
    Guid Id,
    double South,
    double West,
    double North,
    double East,
    string Label)
{
    public double CentreLatitude => (South + North) / 2d;

    public double CentreLongitude => (West + East) / 2d;
}

public sealed record MapGridPlan(BoundingBox Box, AdministrativeLevel Level, int Exponent)
{
    public double CellSizeDegrees => Math.Pow(2d, Exponent);
}

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
    /// Added 21 Oct 2026. Which cells the floor withheld — their place on the grid, never their
    /// count — so a client can draw "withheld" where it would otherwise draw nothing.
    /// </summary>
    public IReadOnlyList<MapCellFrame> WithheldCells { get; init; } = [];

    /// <summary>
    /// True when at least one vertical had more rows in the viewport than it reads for one
    /// request, so the totals are lower bounds. Zooming in makes them exact.
    /// </summary>
    public required bool Truncated { get; init; }
}

/// <summary>
/// <b>The world grid every map on this platform lands on.</b>
/// <para>
/// Pure geometry: a bounding box and a level in; a plan, a cell key, a frame or a marker out. No
/// state, no dependencies, nothing to register — which is exactly why it was the wrong thing to
/// keep inside an application service. B21's heat layers named <c>MapCellAggregator</c> seven
/// times to reach four static methods, and naming a service to borrow its arithmetic is how a
/// module ends up depending on another module's application layer for something that is not an
/// operation at all.
/// </para>
/// <para>
/// <b>Shared deliberately rather than duplicated.</b> A cell key means the same thing to
/// <c>GET /location/map/clusters</c> and to every heat layer because both compute it here. Two
/// implementations of one grid would agree until the day a rounding rule changed in one of them,
/// and the symptom would be two maps disagreeing about which cell a point is in — a defect nobody
/// would recognise as a rounding rule.
/// </para>
/// <para>
/// Moved on 21 November 2026, with the last crossing on the platform. An earlier attempt the same
/// day was reverted because the geometry was entangled with the aggregator's private constants and
/// with the bounds parsing in <c>Domain.Search</c>; the constants came too, and
/// <see cref="BoundingBoxSearch"/> is published beside the box it parses.
/// </para>
/// </summary>
public static class MapGrid
{
    public const int MaxCells = 256;

    public const int CityDivisions = 6;

    public const int NeighborhoodDivisions = 12;

    public const int MinimumMembersForCentroid = 3;

    public const int MinimumExponent = -12;

    public const int MaximumExponent = 5;

    public const double KilometresPerDegree = 111.32d;

    public static AdministrativeLevel ParseLevel(string? value) =>
        Enum.TryParse<AdministrativeLevel>(value, ignoreCase: true, out var parsed)
        && parsed is AdministrativeLevel.Neighborhood or AdministrativeLevel.City
            ? parsed
            : AdministrativeLevel.Neighborhood;

    public static Result<MapGridPlan> Plan(string bounds, string? level, double? cellSizeKm = null)
    {
        var parsed = BoundingBox.TryParse(bounds);

        if (parsed.IsFailure)
        {
            return Result.Failure<MapGridPlan>(parsed.Error);
        }

        var box = parsed.Value;

        // The same continental cap every viewport read on this module applies: wider than that is
        // a request to browse by country, not a map.
        if (box.North - box.South > BoundingBoxSearch.MaxLatitudeSpan)
        {
            return Result.Failure<MapGridPlan>(Error.Validation(
                LocationErrorCodes.BoundsInvalid,
                $"A viewport may not span more than {BoundingBoxSearch.MaxLatitudeSpan} degrees of "
                + "latitude. Zoom in, or browse by country."));
        }

        var rung = ParseLevel(level);

        return Result.Success(new MapGridPlan(box, rung, ChooseExponent(box, rung, cellSizeKm)));
    }

    public static (long Row, long Column) CellOf(double size, double latitude, double longitude) =>
        ((long)Math.Floor(latitude / size), (long)Math.Floor(longitude / size));

    public static MapCellFrame Frame(int exponent, long row, long column)
    {
        var size = Math.Pow(2d, exponent);

        var south = Math.Max(row * size, -90d);
        var north = Math.Min((row + 1) * size, 90d);
        var west = Math.Max(column * size, -180d);
        var east = Math.Min((column + 1) * size, 180d);

        return new MapCellFrame(
            Key: string.Create(CultureInfo.InvariantCulture, $"{exponent}:{row}:{column}"),
            Id: CellId(exponent, row, column),
            South: south,
            West: west,
            North: north,
            East: east,
            Label: Describe((south + north) / 2d, (west + east) / 2d));
    }

    public static (double Latitude, double Longitude) Marker(
        MapCellFrame frame, int members, double latitudeSum, double longitudeSum)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var (latitude, longitude) = members >= MinimumMembersForCentroid
            ? (latitudeSum / members, longitudeSum / members)
            : (frame.CentreLatitude, frame.CentreLongitude);

        return (Math.Round(latitude, 6), Math.Round(longitude, 6));
    }

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

    public static Guid CellId(int exponent, long row, long column)
    {
        var tail = BitConverter.GetBytes(column);

        return new Guid((int)row, (short)exponent, (short)(row >> 32), tail);
    }

    public static double CellsAcross(double latitudeSpan, double longitudeSpan, int exponent)
    {
        var size = Math.Pow(2d, exponent);

        // +1 on each axis: a viewport rarely starts on a cell boundary.
        return (Math.Ceiling(latitudeSpan / size) + 1) * (Math.Ceiling(longitudeSpan / size) + 1);
    }

    public static string Describe(double latitude, double longitude) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Area around {Math.Abs(latitude):0.00}°{(latitude < 0 ? "S" : "N")}, "
            + $"{Math.Abs(longitude):0.00}°{(longitude < 0 ? "W" : "E")}");
}
