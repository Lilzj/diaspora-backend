using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Events;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Observability;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

/// <summary>
/// Somebody joined, was admitted, left, was removed or lapsed: settle the organization's counters,
/// then publish.
/// <para>
/// <b>A newly opened membership arrives with its previous status equal to its current one.</b> That
/// is how the aggregate says "this did not exist before", and passing it through unchanged would
/// compute a zero movement — an organization would never count its first member, which is its owner.
/// The null below is the whole of the fix and the reason this handler is not three lines.
/// </para>
/// <para>
/// <b>Settlement happens before the publish, and the member count is handed over explicitly.</b>
/// Letting the relay read the number back would make the payload depend on the order of two lines
/// in this file.
/// </para>
/// <para>
/// <b>A failed settlement must not fail the admission.</b> A body showing a stale size beside itself
/// is cosmetic; a member told their request failed because a counter would not move is not.
/// </para>
/// </summary>
public sealed class OrganizationMembershipStatusChangedEventHandler
    : DomainEventHandler<OrganizationMembershipStatusChangedDomainEvent>
{
    private readonly CommunityLifeIntegrationEventRelay _relay;
    private readonly IOrganizationRepository _organizations;

    public OrganizationMembershipStatusChangedEventHandler(
        CommunityLifeIntegrationEventRelay relay, IOrganizationRepository organizations)
    {
        _relay = relay;
        _organizations = organizations;
    }

    protected override async Task HandleAsync(
        OrganizationMembershipStatusChangedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        var organization = await _organizations.GetAsync(
            domainEvent.OrganizationId, cancellationToken);

        // Equal statuses mean the membership is new. See the class summary.
        var previousStatus = domainEvent.PreviousStatus == domainEvent.CurrentStatus
            ? (MembershipStatus?)null
            : domainEvent.PreviousStatus;

        var movement = MembershipStatusTransitions.Movement(
            previousStatus, domainEvent.Role, domainEvent.CurrentStatus, domainEvent.Role);

        if (organization is not null && !movement.IsEmpty)
        {
            organization.SettleStatistics(
                domainEvent.ChangedAt,
                movement.Members,
                movement.Pending,
                movement.Administrators,
                movement.Volunteers);

            organization.RecordActivity(domainEvent.ChangedAt);
        }

        await _relay.PublishAsync(
            domainEvent,
            organization?.MembershipPolicy.IsMembershipPublic ?? false,
            organization?.Statistics.MemberCount ?? 0,
            cancellationToken);

        RecordTelemetry(domainEvent, previousStatus);
    }

    private static void RecordTelemetry(
        OrganizationMembershipStatusChangedDomainEvent domainEvent,
        MembershipStatus? previousStatus)
    {
        if (domainEvent.CurrentStatus == MembershipStatus.Active
            && previousStatus != MembershipStatus.Active)
        {
            CommunityLifeTelemetry.MembersJoined.Add(
                1, new KeyValuePair<string, object?>("role", domainEvent.Role.ToString()));

            return;
        }

        if (previousStatus != MembershipStatus.Active
            || domainEvent.CurrentStatus is not (MembershipStatus.Left
                or MembershipStatus.Removed
                or MembershipStatus.Lapsed))
        {
            return;
        }

        CommunityLifeTelemetry.MembersLeft.Add(
            1,
            new KeyValuePair<string, object?>("outcome", domainEvent.CurrentStatus.ToString()));
    }
}
