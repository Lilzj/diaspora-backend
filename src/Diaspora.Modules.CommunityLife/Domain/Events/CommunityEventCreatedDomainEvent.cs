using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>
/// Raised when an event is drafted.
/// <para>
/// Not published downstream. A draft event has a date nobody has committed to and a venue that may
/// still change, and a member whose saved search fired on one would turn up to an empty hall.
/// </para>
/// </summary>
public sealed record CommunityEventCreatedDomainEvent : DomainEvent
{
    public required CommunityEventId CommunityEventId { get; init; }

    public OrganizationId? OrganizationId { get; init; }

    public required UserId CreatedBy { get; init; }

    public required string Title { get; init; }

    public required string Slug { get; init; }

    public required string CategoryKey { get; init; }

    public required string CountryCode { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    public required DateTimeOffset StartsAt { get; init; }
}
