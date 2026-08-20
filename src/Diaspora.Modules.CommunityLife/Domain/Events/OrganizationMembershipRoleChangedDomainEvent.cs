using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>
/// Raised when a member's role inside an organization changes.
/// <para>
/// Separate from the status event because the two answer different questions and are audited
/// differently: somebody becoming an administrator is a privilege grant, and a reviewer looking for
/// those should not have to filter a stream of joins and departures to find them.
/// </para>
/// </summary>
public sealed record OrganizationMembershipRoleChangedDomainEvent : DomainEvent
{
    public required OrganizationMembershipId MembershipId { get; init; }

    public required OrganizationId OrganizationId { get; init; }

    public required UserId MemberUserId { get; init; }

    public required MembershipRole PreviousRole { get; init; }

    public required MembershipRole CurrentRole { get; init; }

    /// <summary>
    /// The membership's status when the role moved.
    /// <para>
    /// Carried because a role can change on a membership that is still pending — an invitation names
    /// the role it offers — and the counters only count people who have actually joined. Without it
    /// the settlement handler would credit an administrator to a body for somebody who has not
    /// accepted, and decrement it again when they do.
    /// </para>
    /// </summary>
    public required MembershipStatus Status { get; init; }

    public UserId? ChangedBy { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
