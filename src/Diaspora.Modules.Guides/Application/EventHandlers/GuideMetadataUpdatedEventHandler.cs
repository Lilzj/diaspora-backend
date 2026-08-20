using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Domain.Events;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Relays a metadata edit outward, as the update contract.
/// <para>
/// The changed-field list is the payload that matters: a consumer reindexes on a title change and
/// ignores a change to the SEO description, and it can only tell them apart because the domain
/// event names the fields.
/// </para>
/// </summary>
public sealed class GuideMetadataUpdatedEventHandler
    : DomainEventHandler<GuideMetadataUpdatedDomainEvent>
{
    private readonly GuideIntegrationEventRelay _relay;

    public GuideMetadataUpdatedEventHandler(GuideIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        GuideMetadataUpdatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
