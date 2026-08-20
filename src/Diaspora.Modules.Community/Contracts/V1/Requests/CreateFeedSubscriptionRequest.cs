namespace Diaspora.Modules.Community.Contracts.V1.Requests;

/// <summary>A saved feed view.</summary>
public sealed record CreateFeedSubscriptionRequest
{
    public required string FeedKey { get; init; }

    public string? Label { get; init; }

    public string? RankingKey { get; init; }

    /// <summary>Opaque to Community. Read only by the feed provider that owns the key.</summary>
    public string? Filters { get; init; }

    public bool NotificationsEnabled { get; init; }
}
