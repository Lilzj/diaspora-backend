namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>A saved feed view.</summary>
public sealed record FeedSubscriptionResponse
{
    public required Guid SubscriptionId { get; init; }

    public required string FeedKey { get; init; }

    public string? Label { get; init; }

    public string? RankingKey { get; init; }

    public string? Filters { get; init; }

    public required bool NotificationsEnabled { get; init; }

    public required int Position { get; init; }

    public DateTimeOffset? LastReadAt { get; init; }
}
