using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Guides.V1.Events;

/// <summary>
/// A guide's declared language changed.
/// <para>
/// Rare but consequential: language drives search analyzers, per-locale browse lists and
/// translation grouping, so a consumer that ignores it serves a Yoruba guide to a French filter.
/// Carries both ends of the change for the same reason
/// <see cref="GuideCategoryChangedIntegrationEvent"/> does — the old language's list needs
/// invalidating too.
/// </para>
/// </summary>
public sealed record GuideLanguageChangedIntegrationEvent : IntegrationEvent
{
    public required Guid GuideId { get; init; }

    /// <summary>ISO 639-1 two-letter code, lower case — before the change.</summary>
    public required string PreviousLanguage { get; init; }

    /// <summary>ISO 639-1 two-letter code, lower case — after it.</summary>
    public required string CurrentLanguage { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
