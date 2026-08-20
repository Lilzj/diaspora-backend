using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>Raised when an organization is soft-deleted.</summary>
public sealed record OrganizationDeletedDomainEvent : DomainEvent
{
    public required OrganizationId OrganizationId { get; init; }

    public required DateTimeOffset DeletedAt { get; init; }
}
