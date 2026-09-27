namespace Diaspora.Maps.Google;

/// <summary>
/// Google's encoded polyline algorithm, decoded.
/// <para>
/// Each coordinate is a delta from the previous one, times 1e5, zig-zag encoded into 5-bit chunks
/// offset by 63. A malformed string yields the points decoded so far rather than throwing: a
/// truncated line still draws the part of the journey it covers.
/// </para>
/// </summary>
internal static class GooglePolyline
{
    public static IReadOnlyList<(double Latitude, double Longitude)> Decode(string? encoded)
    {
        if (string.IsNullOrEmpty(encoded)) return [];

        var points = new List<(double, double)>();
        var index = 0;
        var latitude = 0;
        var longitude = 0;

        while (index < encoded.Length)
        {
            if (!TryNext(encoded, ref index, out var deltaLatitude)) break;
            if (!TryNext(encoded, ref index, out var deltaLongitude)) break;

            latitude += deltaLatitude;
            longitude += deltaLongitude;

            points.Add((latitude / 1e5, longitude / 1e5));
        }

        return points;
    }

    private static bool TryNext(string encoded, ref int index, out int value)
    {
        var result = 0;
        var shift = 0;
        int chunk;

        do
        {
            if (index >= encoded.Length || shift > 30)
            {
                value = 0;
                return false;
            }

            chunk = encoded[index++] - 63;

            if (chunk < 0)
            {
                value = 0;
                return false;
            }

            result |= (chunk & 0x1F) << shift;
            shift += 5;
        }
        while (chunk >= 0x20);

        value = (result & 1) != 0 ? ~(result >> 1) : result >> 1;
        return true;
    }
}
