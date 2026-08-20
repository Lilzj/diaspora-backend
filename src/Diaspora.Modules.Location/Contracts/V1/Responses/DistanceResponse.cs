namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>
/// How far apart two points are, and roughly how long it takes to cover.
/// <para>
/// The commute estimates are computed with the same constants the frontend's
/// <c>getCommuteTimeEstimate</c> uses — walk 5 km/h, transit 20 km/h plus 5 minutes, drive
/// 40 km/h plus 2 — so the server and the client cannot show a member two different answers
/// for the same journey. The constants are asserted equal by test.
/// </para>
/// </summary>
public sealed record DistanceResponse
{
    public required double DistanceKm { get; init; }

    public required double DistanceMiles { get; init; }

    /// <summary>The client's own <c>formatDistance</c> output, rendered server-side.</summary>
    public required string Formatted { get; init; }

    /// <summary>Minutes by mode, keyed <c>walk</c>, <c>drive</c>, <c>transit</c>.</summary>
    public required IReadOnlyDictionary<string, int> CommuteMinutes { get; init; }
}
