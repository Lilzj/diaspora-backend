using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Domain.Events;

namespace Diaspora.Modules.Guides.Application.Knowledge.EventHandlers;

/// <summary>
/// Relays a learning path being created, rearranged, published or archived.
/// <para>
/// Learning paths are the one piece of B8.1 a consumer is likely to render itself — an onboarding
/// checklist, a "step 3 of 7" banner — and every one of those holds its own copy of the step count.
/// This is how they find out it changed.
/// </para>
/// </summary>
public sealed class LearningPathChangedEventHandler
    : DomainEventHandler<LearningPathChangedDomainEvent>
{
    private readonly KnowledgeIntegrationEventRelay _relay;

    public LearningPathChangedEventHandler(KnowledgeIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        LearningPathChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
