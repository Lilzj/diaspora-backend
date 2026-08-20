namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>One badge Trust awarded the author. The pair (badge, country) is the identity.</summary>
public sealed record GuideAuthorBadgeResponse
{
    public required string Badge { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset AwardedAt { get; init; }
}
