using Diaspora.Modules.Community.Domain.ValueObjects;

namespace Diaspora.Modules.Community.Extensibility.Moderation;

/// <summary>What a review provider is given. The text, and nothing that identifies its author.</summary>
public sealed record ContentReviewRequest
{
    public required ContentReference Content { get; init; }

    public required string Body { get; init; }

    public string? Title { get; init; }

    /// <summary>Media references on the content, for a provider that can look at images.</summary>
    public IReadOnlyCollection<Guid> MediaIds { get; init; } = [];

    public string? CountryCode { get; init; }

    /// <summary>
    /// The body language, if the client declared one. Not detected here — a wrong guess sends
    /// content to a classifier trained on another language, which is worse than no guess.
    /// </summary>
    public string? LanguageCode { get; init; }
}
