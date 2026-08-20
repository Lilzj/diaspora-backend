using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Guides.V1.Events;

/// <summary>
/// A guide became readable by somebody other than its author.
/// <para>
/// <b>The event most consumers actually want.</b> This is when a guide enters search, becomes
/// recommendable, and is worth telling a country's members about. It fires on every publication —
/// the first and each republication of an edited draft — and
/// <see cref="IsFirstPublication"/> is how a consumer tells "new knowledge exists" from "existing
/// knowledge was revised": a notification fan-out wants the first, a search index wants them all.
/// </para>
/// <para>
/// The payload carries everything a card-level index needs, so most consumers never have to call
/// back into the Guides module at all. Consumers treat <c>EventId</c> as the idempotency key:
/// delivery is at-least-once and unordered.
/// </para>
/// </summary>
public sealed record GuidePublishedIntegrationEvent : IntegrationEvent
{
    public required Guid GuideId { get; init; }

    /// <summary>The deep-link handle. Stable across renames.</summary>
    public required string Slug { get; init; }

    public required string Title { get; init; }

    public string? Summary { get; init; }

    /// <summary>The catalog key — <c>immigration</c>, <c>housing</c>, <c>healthcare</c>.</summary>
    public required string CategoryKey { get; init; }

    /// <summary>Normalized tag slugs, in display order.</summary>
    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    /// <summary>ISO 639-1 two-letter code, lower case.</summary>
    public required string Language { get; init; }

    public required Guid AuthorId { get; init; }

    /// <summary>The version number this publication produced. <c>1</c> on first publication.</summary>
    public required int Version { get; init; }

    /// <summary>True the first time this guide was ever published; false on republication.</summary>
    public required bool IsFirstPublication { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }
}
