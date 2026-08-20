using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Domain.Events;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Relays the creation of an immutable version outward.
/// <para>
/// Separate from publication because they answer different questions. Publication says "this is
/// now the current advice"; a version says "the previous advice is preserved and citable". A
/// consumer that links to a specific revision — a moderation record, an external citation — needs
/// the second and would be wrong to infer it from the first.
/// </para>
/// </summary>
public sealed class GuideVersionCreatedEventHandler
    : DomainEventHandler<GuideVersionCreatedDomainEvent>
{
    private readonly GuideIntegrationEventRelay _relay;

    public GuideVersionCreatedEventHandler(GuideIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        GuideVersionCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
