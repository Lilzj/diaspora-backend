using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Events;
using Diaspora.Modules.Community.Observability;

namespace Diaspora.Modules.Community.Application.EventHandlers;

/// <summary>
/// Publishes a report.
/// <para>
/// Trust subscribes to this rather than B6 adjusting a reputation. Deciding what a sustained
/// pattern of upheld reports is worth is Trust's judgement; making it here would be the
/// duplication the brief forbids. The moderation case is fed separately, by
/// <see cref="ContentReportToTrustEventHandler"/>.
/// </para>
/// </summary>
public sealed class ContentReportedEventHandler : DomainEventHandler<ContentReportedDomainEvent>
{
    private readonly CommunityIntegrationEventRelay _relay;

    public ContentReportedEventHandler(CommunityIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        ContentReportedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        CommunityTelemetry.ReportsFiled.Add(
            1,
            new KeyValuePair<string, object?>(
                "reason", CommunityWireNames.ToWire(domainEvent.Reason)));

        return _relay.PublishAsync(
            new CommunityContentReportedIntegrationEvent
            {
                ReportId = domainEvent.ReportId.Value,
                ContentType = CommunityWireNames.ToWire(domainEvent.Content.Type),
                ContentId = domainEvent.Content.Id,
                ReporterId = domainEvent.ReporterId.Value,
                Reason = CommunityWireNames.ToWire(domainEvent.Reason),
                OpenReportCount = domainEvent.OpenReportCount,
                ContentAuthorId = domainEvent.ContentAuthorId
            },
            cancellationToken);
    }
}
