namespace Diaspora.Modules.CommunityLife.Extensibility.Calendar;

/// <summary>What a calendar is being asked for.</summary>
public sealed record CalendarWindow
{
    public required DateTimeOffset From { get; init; }

    public required DateTimeOffset To { get; init; }

    public string? CountryCode { get; init; }

    /// <summary>B4's area, when the calendar is scoped to a neighbourhood.</summary>
    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>The reader, so a source can decide what they may see.</summary>
    public Guid? ViewerUserId { get; init; }
}
