using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Domain.Events;

namespace Diaspora.Modules.Providers.Application.EventHandlers;

/// <summary>
/// Relays a registration outward.
/// <para>
/// One handler per domain event rather than one that switches, because MediatR resolves them by
/// type and a switch would put every future event's failure mode in the same class. It also
/// means a consumer reading the code can see, from the file list alone, exactly which domain
/// events cross the module boundary — which is eight of the twenty B7 raises.
/// </para>
/// </summary>
public sealed class ProviderRegisteredEventHandler : DomainEventHandler<ProviderRegisteredDomainEvent>
{
    private readonly ProviderIntegrationEventRelay _relay;

    public ProviderRegisteredEventHandler(ProviderIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        ProviderRegisteredDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
