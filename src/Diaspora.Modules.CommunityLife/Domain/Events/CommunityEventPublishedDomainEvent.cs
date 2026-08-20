using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>
/// Raised when an event becomes discoverable.
/// <para>
/// The module's most consequential fact. The search index, the neighbourhood calendar, the
/// community feed and every count downstream key on this rather than on creation.
/// </para>
/// </summary>
public sealed record CommunityEventPublishedDomainEvent : DomainEvent
{
    public required CommunityEventId CommunityEventId { get; init; }

    public OrganizationId? OrganizationId { get; init; }

    public required string Title { get; init; }

    public required string Slug { get; init; }

    public required string CategoryKey { get; init; }

    public required string CountryCode { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    public required bool IsFirstPublication { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }
}
