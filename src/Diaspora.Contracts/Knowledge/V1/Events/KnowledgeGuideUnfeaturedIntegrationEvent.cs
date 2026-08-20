using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Knowledge.V1.Events;

/// <summary>
/// An editor removed a guide from the featured shelf of one country.
/// <para>
/// The counterpart of <see cref="KnowledgeGuideFeaturedIntegrationEvent"/>. A consumer that
/// mirrors the shelf removes the entry; a consumer that only cached the page invalidates it.
/// Unfeaturing says nothing about the guide itself — it remains published, discoverable and
/// exactly as good as it was.
/// </para>
/// </summary>
public sealed record KnowledgeGuideUnfeaturedIntegrationEvent : IntegrationEvent
{
    public required Guid GuideId { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    /// <summary>The knowledge category it was featured within, when it was scoped to one.</summary>
    public string? KnowledgeCategoryKey { get; init; }

    public required Guid ChangedBy { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
