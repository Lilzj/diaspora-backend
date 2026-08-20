using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Domain.Events;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Relays a guide's creation outward.
/// <para>
/// <b>It announces a draft, and the contract says so.</b> A consumer that indexes this has
/// indexed something nobody may read — the guide has no content yet and its author may never
/// publish it. Analytics and moderation want it; a search index wants
/// <c>GuidePublishedIntegrationEvent</c> instead.
/// </para>
/// </summary>
public sealed class GuideCreatedEventHandler : DomainEventHandler<GuideCreatedDomainEvent>
{
    private readonly GuideIntegrationEventRelay _relay;

    public GuideCreatedEventHandler(GuideIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        GuideCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
