namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>A follow edge. Field names taken from the client's own follow type.</summary>
public sealed record FollowResponse
{
    public required Guid FollowId { get; init; }

    public required Guid FollowerId { get; init; }

    public required Guid FolloweeId { get; init; }

    public required string TargetType { get; init; }

    public required bool IsMuted { get; init; }

    public required int FollowerCount { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
