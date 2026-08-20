namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>How one kind of event performs, over a window.</summary>
public sealed record CategoryPerformanceResponse
{
    public required string CategoryKey { get; init; }

    public string? CategoryName { get; init; }

    public required int EventCount { get; init; }

    public required int PublishedCount { get; init; }

    public required int CompletedCount { get; init; }

    public required int TotalRsvps { get; init; }

    public required int TotalAttendances { get; init; }

    public required int TotalNoShows { get; init; }

    public required int TotalViews { get; init; }

    public double? AttendanceRate { get; init; }

    public double? ConversionRate { get; init; }
}
