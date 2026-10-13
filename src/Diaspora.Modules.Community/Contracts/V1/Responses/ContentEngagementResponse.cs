namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>
/// The reactions and comment count on one piece of content, for content whose own response does
/// not carry them — an official notice, a guide. Posts carry the same figures in their response.
/// </summary>
public sealed record ContentEngagementResponse
{
    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public required ReactionSummaryResponse Reactions { get; init; }

    public required int CommentCount { get; init; }
}
