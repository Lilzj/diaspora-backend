using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Domain.Events;

namespace Diaspora.Modules.Providers.Application.EventHandlers;

/// <summary>
/// Relays a publication outward.
/// <para>
/// The event Search (B15) indexes on, Recommendations (B17) learns from, and Notifications (B16)
/// tells a neighbourhood about. Deliberately not the registration event: a draft is not a
/// business.
/// </para>
/// </summary>
public sealed class ProviderPublishedEventHandler : DomainEventHandler<ProviderPublishedDomainEvent>
{
    private readonly ProviderIntegrationEventRelay _relay;

    public ProviderPublishedEventHandler(ProviderIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        ProviderPublishedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
