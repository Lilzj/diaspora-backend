namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>What search engines are told about the guide, when the author said anything.</summary>
public sealed record GuideSeoResponse
{
    public string? MetaTitle { get; init; }

    public string? MetaDescription { get; init; }

    /// <summary>Absolute https when present.</summary>
    public string? CanonicalUrl { get; init; }

    /// <summary>Comma-joined keywords, as the author typed them.</summary>
    public string? Keywords { get; init; }
}
