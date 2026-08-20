namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>One item on an event's programme.</summary>
public sealed record AgendaItemResponse
{
    public required Guid Id { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public required int StartOffsetMinutes { get; init; }

    /// <summary>Resolved against the event's own start, so it moves when the event does.</summary>
    public required DateTimeOffset StartsAt { get; init; }

    public int? DurationMinutes { get; init; }

    public string? SpeakerName { get; init; }

    public required int Order { get; init; }
}
