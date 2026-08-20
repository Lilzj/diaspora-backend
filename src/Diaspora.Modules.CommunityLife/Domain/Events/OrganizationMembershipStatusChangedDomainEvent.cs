using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>
/// Raised on every membership transition.
/// <para>
/// One event for the whole journey rather than one per state, and the relay decides which
/// transitions become published facts. A join and a departure are; a refusal is not, because a
/// refused request is between the member and the organization's administrators.
/// </para>
/// </summary>
public sealed record OrganizationMembershipStatusChangedDomainEvent : DomainEvent
{
    public required OrganizationMembershipId MembershipId { get; init; }

    public required OrganizationId OrganizationId { get; init; }

    public required UserId MemberUserId { get; init; }

    public required MembershipStatus PreviousStatus { get; init; }

    public required MembershipStatus CurrentStatus { get; init; }

    public required MembershipRole Role { get; init; }

    public UserId? ChangedBy { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
