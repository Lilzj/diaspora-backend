namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>One item on an event's programme.</summary>
public sealed record AgendaItemRequest
{
    public required string Title { get; init; }

    public string? Description { get; init; }

    /// <summary>
    /// Minutes after the event's own start. An offset rather than a wall-clock time, so a
    /// rescheduled conference does not need its fourteen agenda rows retyped.
    /// </summary>
    public int StartOffsetMinutes { get; init; }

    public int? DurationMinutes { get; init; }

    public string? SpeakerName { get; init; }
}
