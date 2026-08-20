using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>Raised when a verification claim is lodged for an organization.</summary>
public sealed record OrganizationVerificationRequestedDomainEvent : DomainEvent
{
    public required OrganizationId OrganizationId { get; init; }

    public required UserId RequestedBy { get; init; }

    public required Guid VerificationId { get; init; }
}
