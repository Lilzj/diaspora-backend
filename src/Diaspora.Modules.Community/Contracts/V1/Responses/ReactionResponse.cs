namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>One member's reaction, echoed back after they made it.</summary>
public sealed record ReactionResponse
{
    public required Guid ReactionId { get; init; }

    public required Guid UserId { get; init; }

    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public required string ReactionKey { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>The new counts, so an optimistic client can reconcile in one round trip.</summary>
    public required ReactionSummaryResponse Summary { get; init; }
}
