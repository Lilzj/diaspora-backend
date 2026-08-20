using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Domain.Events;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Relays a recategorisation.
/// <para>
/// Its own contract rather than a field in the update event, because it moves the guide between
/// two browse surfaces: a consumer maintaining per-category counts or listings needs both the old
/// key and the new one to fix them, and an update event naming only the changed field would force
/// a read back into B8 to discover where the guide came from.
/// </para>
/// </summary>
public sealed class GuideCategoryChangedEventHandler
    : DomainEventHandler<GuideCategoryChangedDomainEvent>
{
    private readonly GuideIntegrationEventRelay _relay;

    public GuideCategoryChangedEventHandler(GuideIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        GuideCategoryChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
