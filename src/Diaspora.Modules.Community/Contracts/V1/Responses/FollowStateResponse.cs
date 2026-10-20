namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>
/// Whether the caller follows a target, and how many do. What a Follow button needs to draw itself
/// before anybody has pressed it.
/// </summary>
public sealed record FollowStateResponse
{
    public required bool IsFollowing { get; init; }

    public required int FollowerCount { get; init; }
}
