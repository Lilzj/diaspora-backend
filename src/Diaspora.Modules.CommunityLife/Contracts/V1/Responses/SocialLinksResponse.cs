namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>An organization's presence elsewhere.</summary>
public sealed record SocialLinksResponse
{
    public string? Facebook { get; init; }

    public string? Instagram { get; init; }

    public string? Twitter { get; init; }

    public string? YouTube { get; init; }

    public string? LinkedIn { get; init; }

    public string? TikTok { get; init; }
}
