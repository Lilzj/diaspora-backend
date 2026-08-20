using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Domain.Events;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Relays a publication outward.
/// <para>
/// <b>The most consequential event this module raises.</b> Search (B15) indexes on it,
/// Recommendations (B17) learns from it, and Notifications (B16) tells a country community that
/// new knowledge exists. Deliberately not the creation event: a draft is not knowledge, and a
/// consumer that indexed drafts would surface half-written advice about visa deadlines.
/// </para>
/// </summary>
public sealed class GuidePublishedEventHandler : DomainEventHandler<GuidePublishedDomainEvent>
{
    private readonly GuideIntegrationEventRelay _relay;

    public GuidePublishedEventHandler(GuideIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        GuidePublishedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
