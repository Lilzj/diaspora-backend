using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Domain.Events;
using Diaspora.Modules.Notifications.Observability;

namespace Diaspora.Modules.Notifications.Application.EventHandlers;

/// <summary>A summary was assembled: publish it.</summary>
public sealed class DigestGeneratedEventHandler : DomainEventHandler<DigestGeneratedDomainEvent>
{
    private readonly NotificationIntegrationEventRelay _relay;

    public DigestGeneratedEventHandler(NotificationIntegrationEventRelay relay) => _relay = relay;

    protected override async Task HandleAsync(
        DigestGeneratedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        NotificationTelemetry.DigestsGenerated.Add(
            1, new KeyValuePair<string, object?>("frequency", domainEvent.Frequency.ToString()));
    }
}
