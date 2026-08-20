namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>The measured record for one event.</summary>
public sealed record EventAnalyticsResponse
{
    public required Guid EventId { get; init; }

    public required int ViewCount { get; init; }

    public required int UniqueViewCount { get; init; }

    public required int SaveCount { get; init; }

    public required int ShareCount { get; init; }

    public required int RsvpCount { get; init; }

    public required int InterestedCount { get; init; }

    public required int GoingCount { get; init; }

    public required int WaitlistedCount { get; init; }

    public required int CancelledCount { get; init; }

    public required int CheckedInCount { get; init; }

    public required int AttendedCount { get; init; }

    public required int NoShowCount { get; init; }

    /// <summary>Views that became a response. Null before anybody looked.</summary>
    public double? ConversionRate { get; init; }

    /// <summary>Null until the event is over — a no-show rate computed mid-event is meaningless.</summary>
    public double? NoShowRate { get; init; }

    public double? AttendanceRate { get; init; }

    /// <summary>Hours from publication to filling. Null on the majority, which never fill.</summary>
    public double? TimeToFillHours { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }
}
