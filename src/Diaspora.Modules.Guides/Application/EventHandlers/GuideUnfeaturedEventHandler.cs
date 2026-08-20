using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Domain.Events;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Relays a guide leaving the featured carousel, onto the same contract with the flag cleared.
/// <para>
/// Its own handler because it is its own domain fact — and because unfeaturing happens
/// implicitly, when a featured guide is archived or deleted, with no curator to name.
/// </para>
/// </summary>
public sealed class GuideUnfeaturedEventHandler : DomainEventHandler<GuideUnfeaturedDomainEvent>
{
    private readonly GuideIntegrationEventRelay _relay;

    public GuideUnfeaturedEventHandler(GuideIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        GuideUnfeaturedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
