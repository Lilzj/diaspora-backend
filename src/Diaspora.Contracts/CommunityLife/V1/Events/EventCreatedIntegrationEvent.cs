using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.CommunityLife.V1.Events;

/// <summary>
/// An event was drafted.
/// <para>
/// Carries almost nothing, and for the same reason the organization creation contract does: a draft
/// event has a date nobody has committed to and a venue that may still change. A consumer whose
/// saved search fired on one would send a member to an empty hall.
/// </para>
/// </summary>
public sealed record EventCreatedIntegrationEvent : IntegrationEvent
{
    public required Guid CommunityEventId { get; init; }

    public Guid? OrganizationId { get; init; }

    public required Guid OrganizerUserId { get; init; }

    public required string Title { get; init; }

    public required string Slug { get; init; }

    public required string CategoryKey { get; init; }

    public required string CountryCode { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// The repeating event this date belongs to — B12.1. Appended, never inserted; see the
    /// publication contract's note.
    /// </summary>
    public Guid? SeriesId { get; init; }

    public int? SeriesSequence { get; init; }
}
