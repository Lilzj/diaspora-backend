namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Announce an event.</summary>
public sealed record PublishEventRequest
{
    /// <summary><c>Public</c>, <c>MembersOnly</c>, <c>Unlisted</c>. Defaults to public.</summary>
    public string? Visibility { get; init; }
}
