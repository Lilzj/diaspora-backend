using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Domain.Events;

namespace Diaspora.Modules.Housing.Application.EventHandlers;

/// <summary>
/// Publishes the media-attached contract.
/// <para>
/// Media is the consumer that matters: an asset nothing ever claimed is swept, and a listing whose
/// gallery was uploaded and never claimed loses its photographs a day later.
/// </para>
/// </summary>
public sealed class HousingMediaAttachedEventHandler
    : DomainEventHandler<HousingMediaAttachedDomainEvent>
{
    private readonly HousingIntegrationEventRelay _relay;

    public HousingMediaAttachedEventHandler(HousingIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        HousingMediaAttachedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
