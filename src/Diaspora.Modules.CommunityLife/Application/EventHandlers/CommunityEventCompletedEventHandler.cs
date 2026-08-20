using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Events;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Observability;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

/// <summary>
/// An event finished: publish the figures, credit the host body, and record how many of the people
/// who committed actually came.
/// <para>
/// <b>The attendance rate is recorded from the frozen figures on the domain event, not from the
/// aggregate.</b> The sweep completes the event first and settles its no-shows afterwards, which is
/// the only order that leaves an honest ratio — settling first would release every absent member's
/// place and leave the going count equal to the checked-in count, reporting perfect attendance at
/// every event on the platform.
/// </para>
/// <para>
/// <b>Recorded only where somebody committed.</b> An event nobody responded to has no attendance
/// rate, and feeding a zero into the histogram would drag the platform's median toward a number
/// that describes nothing.
/// </para>
/// </summary>
public sealed class CommunityEventCompletedEventHandler
    : DomainEventHandler<CommunityEventCompletedDomainEvent>
{
    private readonly CommunityLifeIntegrationEventRelay _relay;
    private readonly IOrganizationRepository _organizations;

    public CommunityEventCompletedEventHandler(
        CommunityLifeIntegrationEventRelay relay, IOrganizationRepository organizations)
    {
        _relay = relay;
        _organizations = organizations;
    }

    protected override async Task HandleAsync(
        CommunityEventCompletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        await CreditOrganizationAsync(domainEvent, cancellationToken);

        RecordTelemetry(domainEvent);
    }

    private async Task CreditOrganizationAsync(
        CommunityEventCompletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent.OrganizationId is not { } organizationId)
        {
            return;
        }

        var organization = await _organizations.GetAsync(organizationId, cancellationToken);

        if (organization is null)
        {
            return;
        }

        organization.SettleStatistics(domainEvent.CompletedAt, completedEvents: 1);
        organization.RecordActivity(domainEvent.CompletedAt);
    }

    private static void RecordTelemetry(CommunityEventCompletedDomainEvent domainEvent)
    {
        CommunityLifeTelemetry.EventsCompleted.Add(
            1, new KeyValuePair<string, object?>("category", domainEvent.CategoryKey));

        if (domainEvent.NoShowCount > 0)
        {
            CommunityLifeTelemetry.NoShows.Add(domainEvent.NoShowCount);
        }

        if (domainEvent.GoingCount > 0)
        {
            CommunityLifeTelemetry.AttendanceRate.Record(
                (double)domainEvent.CheckedInCount / domainEvent.GoingCount,
                new KeyValuePair<string, object?>("category", domainEvent.CategoryKey));
        }
    }
}
