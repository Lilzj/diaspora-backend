using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>Raised when an organization's content changes. Carries what changed, not the values.</summary>
public sealed record OrganizationUpdatedDomainEvent : DomainEvent
{
    public required OrganizationId OrganizationId { get; init; }

    public UserId? ChangedBy { get; init; }

    /// <summary>
    /// The property names that moved. Names rather than values, because an update event that
    /// carried the new content would put an unpublished draft's text on the bus.
    /// </summary>
    public required IReadOnlyList<string> ChangedFields { get; init; }
}
