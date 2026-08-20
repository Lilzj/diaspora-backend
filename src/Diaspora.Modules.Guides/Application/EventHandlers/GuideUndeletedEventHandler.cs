using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Domain.Events;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Relays a return from soft deletion outward.
/// <para>
/// The second restore path, onto the same published contract as un-archiving. It is a separate
/// handler because it is a separate domain fact: a deleted guide was invisible to everyone
/// including its author, so a consumer must re-add it rather than re-rank it.
/// </para>
/// </summary>
public sealed class GuideUndeletedEventHandler : DomainEventHandler<GuideUndeletedDomainEvent>
{
    private readonly GuideIntegrationEventRelay _relay;

    public GuideUndeletedEventHandler(GuideIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        GuideUndeletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
