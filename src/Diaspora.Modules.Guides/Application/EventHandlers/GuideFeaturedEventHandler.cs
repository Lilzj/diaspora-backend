using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Domain.Events;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Relays a curator featuring a guide.
/// <para>
/// Featuring and unfeaturing share one published contract carrying a boolean, rather than being
/// two events. A consumer maintaining a carousel needs both halves and would otherwise subscribe
/// twice to keep one list correct.
/// </para>
/// </summary>
public sealed class GuideFeaturedEventHandler : DomainEventHandler<GuideFeaturedDomainEvent>
{
    private readonly GuideIntegrationEventRelay _relay;

    public GuideFeaturedEventHandler(GuideIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        GuideFeaturedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
