using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Domain.Events;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Relays a structural edit outward, onto the same update contract as a metadata edit.
/// <para>
/// One contract for both, because to a consumer the fact is identical — this guide changed,
/// reindex it — and two contracts would mean every consumer subscribing to both to learn one
/// thing. The changed-field list carries <c>content</c>, so a consumer that only cares about
/// titles can still tell the difference.
/// </para>
/// <para>
/// Note that this fires on a draft edit too. A guide's living document changes long before the
/// change is published; a consumer that must only see released content reads the published event.
/// </para>
/// </summary>
public sealed class GuideContentUpdatedEventHandler
    : DomainEventHandler<GuideContentUpdatedDomainEvent>
{
    private readonly GuideIntegrationEventRelay _relay;

    public GuideContentUpdatedEventHandler(GuideIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        GuideContentUpdatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
