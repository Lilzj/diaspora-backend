namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>
/// One entry in a feed.
/// <para>
/// The post is present when Community owns the item and absent when it does not, which is what
/// lets one feed shape carry a saved list of guides, jobs and posts. A client that only knows
/// about posts reads <c>post</c> and skips the rest; one that knows more reads the reference.
/// </para>
/// </summary>
public sealed record FeedItemResponse
{
    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public CommunityPostSummaryResponse? Post { get; init; }

    public double? DistanceKm { get; init; }

    /// <summary>Why the item is here, when the ranking can say. Usually null.</summary>
    public string? Reason { get; init; }
}
