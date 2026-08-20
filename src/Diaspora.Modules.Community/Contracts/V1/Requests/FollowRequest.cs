namespace Diaspora.Modules.Community.Contracts.V1.Requests;

/// <summary>Following a member, an organization or a hashtag.</summary>
public sealed record FollowRequest
{
    /// <summary><c>member</c> (default), <c>organization</c> or <c>hashtag</c>.</summary>
    public string? TargetType { get; init; }

    public bool Muted { get; init; }
}
