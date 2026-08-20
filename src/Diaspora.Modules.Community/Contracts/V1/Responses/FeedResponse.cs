namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>A page of a feed.</summary>
public sealed record FeedResponse
{
    public required string FeedKey { get; init; }

    public required string RankingKey { get; init; }

    public required IReadOnlyList<FeedItemResponse> Items { get; init; }

    /// <summary>Opaque. Pass it back to get the next page; null means there is none.</summary>
    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }

    public int? TotalAvailable { get; init; }
}
