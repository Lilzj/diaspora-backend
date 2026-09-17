using System.Text;

namespace Diaspora.Platform.Communication;

/// <summary>
/// The grid the live map's channels are cut on: geohash cells at four characters.
/// <para>
/// <b>Why a grid, and why this one.</b> "Everything near me" is not an entity, so a per-entity
/// channel cannot carry it — the map needs a channel per place, and places have to be named the same
/// way by the server that publishes and by every phone that listens. A geohash is computable on both
/// sides from nothing but coordinates, is the same everywhere on Earth, and needs no catalogue lookup —
/// the catalogue is uneven, and an alert in a town Location has never heard of still belongs on the
/// map.
/// </para>
/// <para>
/// <b>Four characters is about 39 km by 20 km at London's latitude.</b> Coarse on purpose: a signal on
/// a cell only tells a client to refetch its viewport, so a cell a little larger than the screen costs
/// an occasional unnecessary refetch, while a cell smaller than the screen costs a subscription per
/// street. A phone watching a city subscribes to a handful of cells; one looking at a whole country
/// subscribes to none and says its figures are periodic.
/// </para>
/// <para>
/// The client computes these exactly as this class does. The encoding is the standard one, pinned by
/// published test vectors on both sides, so the two cannot quietly disagree about where a cell is.
/// </para>
/// </summary>
public static class GeoCells
{
    /// <summary>Characters in a cell name.</summary>
    public const int Precision = 4;

    /// <summary>
    /// The most cells one alert is announced on. Fifty kilometres of radius — the largest affected
    /// area an alert may declare — covers about forty at London's latitude; the cap is a guard, not a
    /// rule a real alert reaches.
    /// </summary>
    public const int MaxCellsPerAnnouncement = 64;

    private const string Alphabet = "0123456789bcdefghjkmnpqrstuvwxyz";

    private const double MetresPerDegreeLatitude = 111_320d;

    /// <summary>How tall one cell is, in degrees: ten bits of latitude.</summary>
    public static double CellHeightDegrees => 180d / 1024d;

    /// <summary>How wide one cell is, in degrees: ten bits of longitude.</summary>
    public static double CellWidthDegrees => 360d / 1024d;

    /// <summary>The standard geohash of a point, at <see cref="Precision"/> characters by default.</summary>
    public static string Encode(double latitude, double longitude, int precision = Precision)
    {
        if (precision is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(precision), precision, "Between 1 and 12.");
        }

        double latitudeLow = -90d, latitudeHigh = 90d, longitudeLow = -180d, longitudeHigh = 180d;
        var hash = new StringBuilder(precision);
        var bit = 0;
        var character = 0;
        var longitudeBit = true;

        while (hash.Length < precision)
        {
            if (longitudeBit)
            {
                var middle = (longitudeLow + longitudeHigh) / 2d;

                if (longitude >= middle)
                {
                    character |= 1 << (4 - bit);
                    longitudeLow = middle;
                }
                else
                {
                    longitudeHigh = middle;
                }
            }
            else
            {
                var middle = (latitudeLow + latitudeHigh) / 2d;

                if (latitude >= middle)
                {
                    character |= 1 << (4 - bit);
                    latitudeLow = middle;
                }
                else
                {
                    latitudeHigh = middle;
                }
            }

            longitudeBit = !longitudeBit;

            if (bit < 4)
            {
                bit++;
            }
            else
            {
                hash.Append(Alphabet[character]);
                bit = 0;
                character = 0;
            }
        }

        return hash.ToString();
    }

    /// <summary>Whether a string names a cell on this grid.</summary>
    public static bool IsCell(string? cell) =>
        cell is { Length: Precision } && cell.All(character => Alphabet.Contains(character, StringComparison.Ordinal));

    /// <summary>
    /// Every cell a circle touches, the centre's cell first.
    /// <para>
    /// Samples the circle's bounding rectangle one cell apart, with the last sample on each side
    /// clamped to the edge, so no cell the rectangle overlaps is stepped over. A rectangle slightly
    /// over-covers a circle; the cost is a client refetching a viewport the alert does not quite
    /// reach, which is the cheap direction to be wrong in.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> Cover(double latitude, double longitude, double radiusMetres)
    {
        var cells = new List<string> { Encode(latitude, longitude) };

        if (radiusMetres <= 0d)
        {
            return cells;
        }

        var latitudeSpan = radiusMetres / MetresPerDegreeLatitude;
        var longitudeSpan = radiusMetres
            / (MetresPerDegreeLatitude * Math.Max(Math.Cos(latitude * Math.PI / 180d), 0.01d));

        var south = Math.Max(latitude - latitudeSpan, -90d);
        var north = Math.Min(latitude + latitudeSpan, 90d);
        var west = Math.Max(longitude - longitudeSpan, -180d);
        var east = Math.Min(longitude + longitudeSpan, 180d);

        var seen = new HashSet<string>(cells, StringComparer.Ordinal);

        for (var row = south; ; row += CellHeightDegrees)
        {
            var sampleLatitude = Math.Min(row, north);

            for (var column = west; ; column += CellWidthDegrees)
            {
                var sampleLongitude = Math.Min(column, east);
                var cell = Encode(sampleLatitude, sampleLongitude);

                if (seen.Add(cell))
                {
                    cells.Add(cell);

                    if (cells.Count >= MaxCellsPerAnnouncement)
                    {
                        return cells;
                    }
                }

                if (sampleLongitude >= east)
                {
                    break;
                }
            }

            if (sampleLatitude >= north)
            {
                break;
            }
        }

        return cells;
    }
}
