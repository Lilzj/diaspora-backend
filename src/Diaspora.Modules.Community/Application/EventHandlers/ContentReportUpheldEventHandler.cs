using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Content.V1.Events;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Events;

namespace Diaspora.Modules.Community.Application.EventHandlers;

/// <summary>Tells the platform a report was upheld, so Trust pays the reporter (REP-13).</summary>
public sealed class ContentReportUpheldEventHandler : DomainEventHandler<ContentReportUpheldDomainEvent>
{
    private readonly CommunityIntegrationEventRelay _relay;

    public ContentReportUpheldEventHandler(CommunityIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        ContentReportUpheldDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _relay.PublishAsync(
            new ContentReportUpheldIntegrationEvent
            {
                ReportId = domainEvent.ReportId.Value,
                Module = "community",
                ContentType = CommunityWireNames.ToWire(domainEvent.Content.Type),
                ContentId = domainEvent.Content.Id,
                ReporterUserId = domainEvent.ReporterId.Value,
                ContentOwnerUserId = domainEvent.ContentAuthorId,
                UpheldAt = domainEvent.UpheldAt
            },
            cancellationToken);
    }
}
