using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>
/// Raised when an organization is drafted, visible to nobody but its creator.
/// <para>
/// Separate from <see cref="OrganizationPublishedDomainEvent"/> deliberately: a creation is worth
/// auditing and counting, and emphatically not worth indexing or putting in a directory. A draft is
/// not a real organization yet, and a member who found one would be looking at a page nobody has
/// finished writing.
/// </para>
/// </summary>
public sealed record OrganizationCreatedDomainEvent : DomainEvent
{
    public required OrganizationId OrganizationId { get; init; }

    public required UserId CreatedBy { get; init; }

    public required string Name { get; init; }

    public required string Slug { get; init; }

    public required string OrganizationTypeKey { get; init; }

    public required string CountryCode { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }
}
