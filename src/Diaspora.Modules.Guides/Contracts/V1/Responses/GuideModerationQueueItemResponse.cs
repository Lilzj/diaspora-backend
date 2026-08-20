namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>One guide awaiting a moderator's attention — flagged, hidden, or drawing suggestions.</summary>
public sealed record GuideModerationQueueItemResponse
{
    public required Guid GuideId { get; init; }

    public required string Slug { get; init; }

    public required string Title { get; init; }

    public required Guid AuthorId { get; init; }

    /// <summary>A <c>GuideStatus</c> name.</summary>
    public required string Status { get; init; }

    /// <summary>A <c>GuideModerationState</c> name — why it is in the queue.</summary>
    public required string ModerationState { get; init; }

    public required string CountryCode { get; init; }

    public required string CategoryKey { get; init; }

    /// <summary>Open suggestions on the guide, when the queue includes them.</summary>
    public int OpenSuggestions { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}
