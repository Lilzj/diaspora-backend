using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>
/// Raised on every organization lifecycle move.
/// <para>
/// One event for every transition rather than one per destination, so the machine is consulted
/// once and the audit trail is a single stream a reader can follow.
/// </para>
/// </summary>
public sealed record OrganizationStatusChangedDomainEvent : DomainEvent
{
    public required OrganizationId OrganizationId { get; init; }

    public required OrganizationStatus PreviousStatus { get; init; }

    public required OrganizationStatus CurrentStatus { get; init; }

    public UserId? ChangedBy { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
