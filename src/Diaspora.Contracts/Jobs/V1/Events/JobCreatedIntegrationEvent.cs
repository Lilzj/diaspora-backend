using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Jobs.V1.Events;

/// <summary>
/// A job posting was drafted.
/// <para>
/// <b>Deliberately thin, because a draft is not a vacancy.</b> It is worth auditing and counting
/// and emphatically not worth indexing, alerting on or announcing — a candidate whose alert fired
/// on a draft would be applying to something that does not exist yet. Every consumer that puts a
/// job in front of a person keys on <see cref="JobPublishedIntegrationEvent"/> instead.
/// </para>
/// <para>
/// Consumers treat <c>EventId</c> as the idempotency key: delivery is at-least-once and unordered.
/// </para>
/// </summary>
public sealed record JobCreatedIntegrationEvent : IntegrationEvent
{
    public required Guid JobId { get; init; }

    public required Guid EmployerId { get; init; }

    /// <summary>The member who drafted it. Authority over the posting belongs to the employer.</summary>
    public required Guid CreatedBy { get; init; }

    public required string Title { get; init; }

    /// <summary>The deep-link handle. Stable across title edits.</summary>
    public required string Slug { get; init; }

    /// <summary>The employment-domain key — <c>healthcare</c>, <c>technology</c>.</summary>
    public required string CategoryKey { get; init; }

    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    public required string CountryCode { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
