using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>Raised when a moderator's standing decision about an organization changes.</summary>
public sealed record OrganizationModerationChangedDomainEvent : DomainEvent
{
    public required OrganizationId OrganizationId { get; init; }

    public required CommunityLifeModerationState PreviousState { get; init; }

    public required CommunityLifeModerationState CurrentState { get; init; }

    public UserId? ChangedBy { get; init; }

    public string? Reason { get; init; }
}
