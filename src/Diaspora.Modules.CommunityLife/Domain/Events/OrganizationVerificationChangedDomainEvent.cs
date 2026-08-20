using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>Raised when Trust's decision about an organization changes.</summary>
public sealed record OrganizationVerificationChangedDomainEvent : DomainEvent
{
    public required OrganizationId OrganizationId { get; init; }

    public required OrganizationVerificationState PreviousState { get; init; }

    public required OrganizationVerificationState CurrentState { get; init; }

    public Guid? VerificationId { get; init; }

    public string? Badge { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
