using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.CommunityLife.V1.Events;

/// <summary>
/// Somebody joined an organization.
/// <para>
/// <b>Carries the member's identifier and no more about them.</b> Membership of a religious or
/// political association is sensitive information in several of the countries this platform's
/// members come from, and a contract that also carried a name and a photograph would put it on
/// every consumer's queue whether that consumer needed it or not.
/// </para>
/// <para>
/// <see cref="IsMembershipPublic"/> travels with the event so a consumer knows whether it may
/// render the fact at all. A consumer that ignores it is doing something the organization
/// explicitly turned off.
/// </para>
/// </summary>
public sealed record OrganizationMemberJoinedIntegrationEvent : IntegrationEvent
{
    public required Guid MembershipId { get; init; }

    public required Guid OrganizationId { get; init; }

    public required Guid MemberUserId { get; init; }

    /// <summary>The role granted on admission, as its name. Almost always <c>Member</c>.</summary>
    public required string Role { get; init; }

    /// <summary>Whether the organization publishes who belongs. See the summary.</summary>
    public required bool IsMembershipPublic { get; init; }

    /// <summary>The size after the join. What a growth consumer counts without a second call.</summary>
    public required int MemberCount { get; init; }

    public required DateTimeOffset JoinedAt { get; init; }

    /// <summary>
    /// The organization's registered administrator — B13.1. The member who created it.
    /// <para>
    /// <b>One person, and that is the whole point.</b> The obvious reading of "tell the
    /// organization somebody joined" is a fan-out across its leadership, which for a body with
    /// forty thousand members is a message volume nothing downstream can bound. Naming the
    /// registered administrator turns an unbounded audience into a single addressable member, and
    /// leaves any wider fan-out to the module that owns membership.
    /// </para>
    /// <para>
    /// <see cref="IsMembershipPublic"/> still governs what may be rendered. An administrator
    /// learning who joined their own organization is not publication.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the organization could not be read.</para>
    /// </summary>
    public Guid? OrganizerUserId { get; init; }
}
