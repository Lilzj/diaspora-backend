namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>A post as a feed card renders it. The client's update summary.</summary>
public sealed record CommunityPostSummaryResponse
{
    public required Guid PostId { get; init; }

    public required Guid UpdateId { get; init; }

    public required AuthorSummaryResponse Author { get; init; }

    public string? Title { get; init; }

    public required string Body { get; init; }

    public required string Description { get; init; }

    public required string Category { get; init; }

    public required string Urgency { get; init; }

    public required string Status { get; init; }

    public required string ContentType { get; init; }

    public required LocationContextResponse Location { get; init; }

    public required IReadOnlyList<Guid> ImageMediaIds { get; init; }

    public required IReadOnlyList<string> Hashtags { get; init; }

    public required ReactionSummaryResponse Reactions { get; init; }

    public required int CommentCount { get; init; }

    public required int ConfirmationCount { get; init; }

    public required int VerifiedResidentConfirmationCount { get; init; }

    public required int RequiredConfirmations { get; init; }

    public required string VerificationStatus { get; init; }

    public required bool BookmarkedByViewer { get; init; }

    public required bool IsPinned { get; init; }

    public required bool HasPoll { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public required bool IsExpired { get; init; }
}
