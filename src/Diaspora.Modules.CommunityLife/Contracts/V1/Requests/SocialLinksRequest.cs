namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>An organization's presence elsewhere. Every value must be an http(s) URL.</summary>
public sealed record SocialLinksRequest
{
    public string? Facebook { get; init; }

    public string? Instagram { get; init; }

    public string? Twitter { get; init; }

    public string? YouTube { get; init; }

    public string? LinkedIn { get; init; }

    public string? TikTok { get; init; }
}
