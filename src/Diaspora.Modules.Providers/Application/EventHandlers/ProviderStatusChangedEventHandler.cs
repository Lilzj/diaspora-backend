using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Domain.Events;

namespace Diaspora.Modules.Providers.Application.EventHandlers;

/// <summary>
/// Relays a lifecycle transition outward.
/// <para>
/// The event a search index must not miss in the outward direction: a provider suspended or
/// closed and still being served is a member sent to a business that no longer wants to be found.
/// </para>
/// </summary>
public sealed class ProviderStatusChangedEventHandler
    : DomainEventHandler<ProviderStatusChangedDomainEvent>
{
    private readonly ProviderIntegrationEventRelay _relay;

    public ProviderStatusChangedEventHandler(ProviderIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        ProviderStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
