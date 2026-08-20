namespace Diaspora.Modules.CommunityLife.Domain.Analytics;

/// <summary>
/// How one kind of event performs.
/// <para>
/// A projection rather than an aggregate: it is computed by a grouped read and never stored, which
/// is what keeps it correct when the window moves.
/// </para>
/// </summary>
public sealed record CategoryPerformance(
    string CategoryKey,
    int EventCount,
    int PublishedCount,
    int CompletedCount,
    int CancelledCount,
    int TotalRsvps,
    int TotalAttendances,
    int TotalNoShows,
    int TotalViews)
{
    /// <summary>Commitments that turned up. Null until at least one event in the window finished.</summary>
    public double? AttendanceRate =>
        TotalAttendances + TotalNoShows == 0
            ? null
            : (double)TotalAttendances / (TotalAttendances + TotalNoShows);

    /// <summary>Views that became a response.</summary>
    public double? ConversionRate =>
        TotalViews == 0 ? null : (double)TotalRsvps / TotalViews;
}
