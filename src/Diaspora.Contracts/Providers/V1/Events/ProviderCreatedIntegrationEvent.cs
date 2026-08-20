using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Providers.V1.Events;

/// <summary>
/// A provider was registered.
/// <para>
/// Lives in <c>Diaspora.Contracts</c> rather than inside the Providers module for the same
/// reason Identity's and Trust's do: a consumer must be able to parse the event without
/// referencing the module that raised it. A Notifications module reacting to this takes a
/// dependency on this assembly and on nothing of B7's.
/// </para>
/// <para>
/// <b>Versioning.</b> The <c>V1</c> namespace segment is the version. These records are
/// additive-only: a new optional property is a compatible change, anything else is a <c>V2</c>
/// type published alongside V1 until consumers migrate. Renaming a property here silently breaks
/// every deserialiser in the estate, which is exactly the failure a published contract exists to
/// prevent.
/// </para>
/// <para>
/// <b>Outbox readiness.</b> Every payload is a flat, serialisable record with no domain types,
/// so the same instance dispatched in-process today can be written to an outbox table and
/// relayed later without changing shape. Consumers must treat <c>EventId</c> as the idempotency
/// key: at-least-once delivery is the assumption.
/// </para>
/// <para>
/// <b>Registered is not published.</b> A provider created here is a draft, invisible to
/// everyone but its owner. A consumer that indexes on this event indexes half-written
/// businesses; the one to index on is <see cref="ProviderPublishedIntegrationEvent"/>.
/// </para>
/// </summary>
public sealed record ProviderCreatedIntegrationEvent : IntegrationEvent
{
    public required Guid ProviderId { get; init; }

    public required Guid OwnerId { get; init; }

    /// <summary>The <c>ProviderType</c> name — <c>Business</c>, <c>Church</c>, <c>Individual</c>.</summary>
    public required string ProviderType { get; init; }

    public required Guid CategoryId { get; init; }

    public required string Name { get; init; }

    /// <summary>The deep-link handle. Stable across renames.</summary>
    public required string Slug { get; init; }

    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
