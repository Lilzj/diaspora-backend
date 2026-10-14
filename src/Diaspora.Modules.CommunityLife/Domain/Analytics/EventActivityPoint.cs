namespace Diaspora.Modules.CommunityLife.Domain.Analytics;

/// <summary>
/// One event's activity totals with the coordinate of its venue. What the area around a point sums
/// once it has kept the events inside its circle.
/// </summary>
public sealed record EventActivityPoint(
    double Latitude,
    double Longitude,
    int RsvpCount,
    int AttendedCount);
