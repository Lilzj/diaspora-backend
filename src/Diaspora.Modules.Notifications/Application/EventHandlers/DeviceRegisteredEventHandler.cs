using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Domain.Events;
using Diaspora.Modules.Notifications.Observability;

namespace Diaspora.Modules.Notifications.Application.EventHandlers;

/// <summary>
/// A member added or refreshed a delivery endpoint: publish it.
/// <para>
/// The published contract carries no token. It is a credential for somebody else's service, and the
/// only thing any consumer legitimately needs to know is that the member became reachable.
/// </para>
/// </summary>
public sealed class DeviceRegisteredEventHandler : DomainEventHandler<DeviceRegisteredDomainEvent>
{
    private readonly NotificationIntegrationEventRelay _relay;

    public DeviceRegisteredEventHandler(NotificationIntegrationEventRelay relay) => _relay = relay;

    protected override async Task HandleAsync(
        DeviceRegisteredDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        NotificationTelemetry.DevicesRegistered.Add(
            1, new KeyValuePair<string, object?>("platform", domainEvent.Platform.ToString()));
    }
}
