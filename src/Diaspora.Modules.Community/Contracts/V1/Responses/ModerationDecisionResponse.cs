namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>One decision in the history. Field names taken from the client's moderation action.</summary>
public sealed record ModerationDecisionResponse
{
    public required Guid ActionId { get; init; }

    public Guid? ModeratorId { get; init; }

    public string? ModeratorName { get; init; }

    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public required string ActionType { get; init; }

    /// <summary>Staff, automated, system or author. An appeal always asks this first.</summary>
    public required string Source { get; init; }

    public string? Reason { get; init; }

    public required string PreviousStatus { get; init; }

    public required string NewStatus { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
