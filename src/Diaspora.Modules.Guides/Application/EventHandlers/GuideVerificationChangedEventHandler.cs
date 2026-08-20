using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Domain.Events;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Relays a change to a guide's trust mark.
/// <para>
/// B8 raises this whatever moved the mark — a submission entering the queue, Trust approving,
/// Trust revoking — because the fact a consumer cares about is the guide's current standing, not
/// which system caused it. A revocation is the case that matters: a consumer still showing a
/// verified badge on advice Trust has withdrawn is the failure this event exists to prevent.
/// </para>
/// </summary>
public sealed class GuideVerificationChangedEventHandler
    : DomainEventHandler<GuideVerificationChangedDomainEvent>
{
    private readonly GuideIntegrationEventRelay _relay;

    public GuideVerificationChangedEventHandler(GuideIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        GuideVerificationChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
