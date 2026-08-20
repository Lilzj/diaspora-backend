using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Domain.Events;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Relays a soft deletion outward.
/// <para>
/// The instruction every consumer holding a copy must act on: drop it. A soft delete is
/// recoverable inside B8, but outside it the guide is gone — a search index or a recommendation
/// model that keeps serving a deleted guide is serving content its author withdrew.
/// </para>
/// </summary>
public sealed class GuideDeletedEventHandler : DomainEventHandler<GuideDeletedDomainEvent>
{
    private readonly GuideIntegrationEventRelay _relay;

    public GuideDeletedEventHandler(GuideIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        GuideDeletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
