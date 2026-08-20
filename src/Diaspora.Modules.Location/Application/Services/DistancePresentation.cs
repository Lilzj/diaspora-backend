using System.Globalization;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;

namespace Diaspora.Modules.Location.Application.Services;

/// <summary>
/// Renders a distance the way the client already renders it.
/// <para>
/// A deliberate reimplementation of the frontend's <c>formatDistance</c> and
/// <c>getCommuteTimeEstimate</c>, constant for constant. The client keeps its copy — it
/// formats distances it computed locally, and taking that away would mean a round trip to
/// render a badge — so the two implementations coexist, which makes agreement between them a
/// requirement rather than a nicety. A member must not see "1.2 km away" on a cached card and
/// "0.9 km away" on the same card after a refresh. The constants below are asserted equal to
/// the client's by test.
/// </para>
/// </summary>
public static class DistancePresentation
{
    /// <summary>Below this, the client renders "In Your Neighborhood" rather than a figure.</summary>
    public const double NeighborhoodThresholdKm = 0.1d;

    /// <summary>Below this, the figure is rendered in metres.</summary>
    public const double MetersThresholdKm = 1d;

    /// <summary>Walking speed, km/h. The client's <c>km / 5 * 60</c>.</summary>
    public const double WalkingSpeedKph = 5d;

    /// <summary>Transit speed, km/h, before the waiting allowance.</summary>
    public const double TransitSpeedKph = 20d;

    /// <summary>Minutes added to a transit estimate for waiting.</summary>
    public const int TransitBufferMinutes = 5;

    /// <summary>Driving speed, km/h, in urban traffic.</summary>
    public const double DrivingSpeedKph = 40d;

    /// <summary>Minutes added to a driving estimate for traffic.</summary>
    public const int DrivingBufferMinutes = 2;

    /// <summary>The client's <c>formatDistance</c>, character for character.</summary>
    public static string Format(Distance distance)
    {
        ArgumentNullException.ThrowIfNull(distance);

        if (distance.Kilometers < NeighborhoodThresholdKm)
        {
            return "In Your Neighborhood";
        }

        if (distance.Kilometers < MetersThresholdKm)
        {
            var meters = (int)Math.Round(distance.Meters, MidpointRounding.AwayFromZero);
            return string.Create(CultureInfo.InvariantCulture, $"{meters}m away");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{distance.Kilometers:F1} km away");
    }

    /// <summary>
    /// Estimated journey time in whole minutes, never less than one for a non-zero distance.
    /// <para>
    /// The floor matches the client, which renders "1 min" rather than "0 mins" — a zero-minute
    /// journey reads as a bug even when the two points really are next door.
    /// </para>
    /// </summary>
    public static int CommuteMinutes(Distance distance, CommuteMode mode)
    {
        ArgumentNullException.ThrowIfNull(distance);

        if (distance.Kilometers <= 0d)
        {
            return 0;
        }

        var minutes = mode switch
        {
            CommuteMode.Walk => distance.Kilometers / WalkingSpeedKph * 60d,
            CommuteMode.Transit => (distance.Kilometers / TransitSpeedKph * 60d) + TransitBufferMinutes,
            _ => (distance.Kilometers / DrivingSpeedKph * 60d) + DrivingBufferMinutes
        };

        return Math.Max(1, (int)Math.Round(minutes, MidpointRounding.AwayFromZero));
    }

    /// <summary>Every mode at once, keyed by the client's own lower-case names.</summary>
    public static IReadOnlyDictionary<string, int> AllCommuteModes(Distance distance) =>
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["walk"] = CommuteMinutes(distance, CommuteMode.Walk),
            ["drive"] = CommuteMinutes(distance, CommuteMode.Drive),
            ["transit"] = CommuteMinutes(distance, CommuteMode.Transit)
        };
}
