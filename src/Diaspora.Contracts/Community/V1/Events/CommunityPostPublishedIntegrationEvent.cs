using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Community.V1.Events;

/// <summary>
/// A post became visible.
/// <para>
/// The event Search indexes on, Recommendations learns from, and a following-feed cache fans
/// out on. Published on publication rather than creation: a draft is not content, and indexing
/// one would surface something its author has not decided to say.
/// </para>
/// <para>
/// <b>Versioning.</b> The <c>V1</c> namespace segment is the version, and these records are
/// additive-only — a new optional property is compatible, anything else is a <c>V2</c> published
/// alongside. Consumers must treat <c>EventId</c> as an idempotency key: delivery is
/// at-least-once and unordered, which is the same contract Trust and Identity publish under.
/// </para>
/// </summary>
public sealed record CommunityPostPublishedIntegrationEvent : IntegrationEvent
{
    public required Guid PostId { get; init; }

    public required Guid AuthorId { get; init; }

    /// <summary>Set when the post was published as an organization.</summary>
    public Guid? OrganizationId { get; init; }

    public required string Category { get; init; }

    public required string Urgency { get; init; }

    /// <summary>
    /// The audience level name. <b>A consumer that ignores this will leak content.</b> A
    /// recommendation engine surfacing a followers-only post to a stranger has published it.
    /// </summary>
    public required string Visibility { get; init; }

    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    public IReadOnlyList<string> Hashtags { get; init; } = [];

    public required DateTimeOffset PublishedAt { get; init; }
}
