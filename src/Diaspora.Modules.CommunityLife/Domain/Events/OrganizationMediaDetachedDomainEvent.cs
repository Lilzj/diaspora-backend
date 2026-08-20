using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>Raised when a Media reference is removed. Never deletes the asset — that is B5's call.</summary>
public sealed record OrganizationMediaDetachedDomainEvent : DomainEvent
{
    public required OrganizationId OrganizationId { get; init; }

    public required Guid MediaId { get; init; }

    public required CommunityLifeMediaRole Role { get; init; }
}
