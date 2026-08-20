using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Events;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.Policies;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

/// <summary>
/// Somebody was promoted or stood down inside one organization.
/// <para>
/// <b>Deliberately not published.</b> Who runs a body is internal bookkeeping — no other module acts
/// on it, and a bus is a poor place to learn that a named member was demoted. What it does move are
/// the two role counters, which is why it shares the movement calculation with the status handler
/// rather than keeping its own: somebody promoted to administrator and somebody admitted as one are
/// the same movement, and two copies of that arithmetic would drift.
/// </para>
/// </summary>
public sealed class OrganizationMembershipRoleChangedEventHandler
    : DomainEventHandler<OrganizationMembershipRoleChangedDomainEvent>
{
    private readonly IOrganizationRepository _organizations;

    public OrganizationMembershipRoleChangedEventHandler(
        IOrganizationRepository organizations) => _organizations = organizations;

    protected override async Task HandleAsync(
        OrganizationMembershipRoleChangedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        var organization = await _organizations.GetAsync(
            domainEvent.OrganizationId, cancellationToken);

        if (organization is null)
        {
            return;
        }

        // The status is the same on both sides — only the role moved — so only the two role
        // counters can move. It is read off the event rather than assumed active, because a role
        // can change on a membership that is still an invitation, and nobody who has not joined
        // belongs in a body's administrator count.
        var movement = MembershipStatusTransitions.Movement(
            domainEvent.Status,
            domainEvent.PreviousRole,
            domainEvent.Status,
            domainEvent.CurrentRole);

        if (movement.IsEmpty)
        {
            return;
        }

        organization.SettleStatistics(
            domainEvent.ChangedAt,
            administrators: movement.Administrators,
            volunteers: movement.Volunteers);
    }
}
