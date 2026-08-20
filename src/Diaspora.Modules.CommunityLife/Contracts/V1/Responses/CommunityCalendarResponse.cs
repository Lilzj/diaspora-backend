namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>A window of a community calendar.</summary>
public sealed record CommunityCalendarResponse
{
    public required DateTimeOffset From { get; init; }

    public required DateTimeOffset To { get; init; }

    public string? CountryCode { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    public IReadOnlyList<CalendarEntryResponse> Entries { get; init; } = [];

    /// <summary>
    /// Which sources answered. A source that failed or timed out is simply absent, which is how a
    /// calendar degrades rather than breaks.
    /// </summary>
    public IReadOnlyList<string> Sources { get; init; } = [];
}
