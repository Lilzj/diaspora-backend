using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Events;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

/// <summary>Tells the platform a report was upheld, so Trust pays the reporter (REP-13).</summary>
public sealed class CommunityLifeReportUpheldEventHandler
    : DomainEventHandler<CommunityLifeReportUpheldDomainEvent>
{
    private readonly CommunityLifeIntegrationEventRelay _relay;

    public CommunityLifeReportUpheldEventHandler(CommunityLifeIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        CommunityLifeReportUpheldDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
