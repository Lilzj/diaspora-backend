namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>One row in the moderator queue. Field names taken from the client's queue item.</summary>
public sealed record ModerationQueueItemResponse
{
    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public required string Title { get; init; }

    public required Guid AuthorId { get; init; }

    public string? AuthorName { get; init; }

    /// <summary>From Trust. Never computed here.</summary>
    public string? AuthorLevel { get; init; }

    public ContentReviewResponse? AiReview { get; init; }

    public required int ReportCount { get; init; }

    public required IReadOnlyList<ReportResponse> Reports { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
