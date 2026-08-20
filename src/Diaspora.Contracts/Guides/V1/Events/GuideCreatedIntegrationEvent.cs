using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Guides.V1.Events;

/// <summary>
/// A guide was created.
/// <para>
/// Lives in <c>Diaspora.Contracts</c> rather than inside the Guides module for the same reason
/// Providers' and Trust's do: a consumer must be able to parse the event without referencing the
/// module that raised it. A Notifications module reacting to this takes a dependency on this
/// assembly and on nothing of B8's.
/// </para>
/// <para>
/// <b>Versioning.</b> The <c>V1</c> namespace segment is the version. These records are
/// additive-only: a new optional property is a compatible change, anything else is a <c>V2</c>
/// type published alongside V1 until consumers migrate. Renaming a property here silently breaks
/// every deserialiser in the estate, which is exactly the failure a published contract exists to
/// prevent.
/// </para>
/// <para>
/// <b>Outbox readiness.</b> Every payload is a flat, serialisable record with no domain types, so
/// the same instance dispatched in-process today can be written to an outbox table and relayed
/// later without changing shape. Consumers must treat <c>EventId</c> as the idempotency key:
/// at-least-once delivery is the assumption.
/// </para>
/// <para>
/// <b>Created is not published.</b> A guide created here is a draft, visible to nobody but its
/// author. A consumer building an index of published knowledge indexes half-written articles if
/// it indexes on this event; the one to index on is <see cref="GuidePublishedIntegrationEvent"/>.
/// </para>
/// </summary>
public sealed record GuideCreatedIntegrationEvent : IntegrationEvent
{
    public required Guid GuideId { get; init; }

    public required Guid AuthorId { get; init; }

    public required string Title { get; init; }

    /// <summary>The deep-link handle. Stable across renames.</summary>
    public required string Slug { get; init; }

    /// <summary>The catalog key — <c>immigration</c>, <c>housing</c>, <c>healthcare</c>.</summary>
    public required string CategoryKey { get; init; }

    /// <summary>ISO 639-1 two-letter code, lower case.</summary>
    public required string Language { get; init; }

    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    public required string CountryCode { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
