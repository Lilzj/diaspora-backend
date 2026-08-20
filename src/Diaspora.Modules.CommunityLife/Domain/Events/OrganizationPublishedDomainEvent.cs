using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>
/// Raised when an organization becomes discoverable.
/// <para>
/// The module's most consequential organization fact. Everything downstream — the directory index,
/// the neighbourhood hub count, the search projection — keys on this rather than on creation.
/// </para>
/// </summary>
public sealed record OrganizationPublishedDomainEvent : DomainEvent
{
    public required OrganizationId OrganizationId { get; init; }

    public required string Name { get; init; }

    public required string Slug { get; init; }

    public required string OrganizationTypeKey { get; init; }

    public required string CountryCode { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>Whether this is the first time it has ever been published. What growth counts.</summary>
    public required bool IsFirstPublication { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }
}
