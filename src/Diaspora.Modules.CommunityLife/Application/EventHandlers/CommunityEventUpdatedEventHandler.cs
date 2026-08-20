using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Events;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

/// <summary>
/// An event's content changed.
/// <para>
/// <b>Added in B12.1, and its absence was a real defect rather than a deliberate omission.</b> The
/// aggregate has raised <c>CommunityEventUpdatedDomainEvent</c> on every content change since B12
/// shipped and nothing subscribed to it, so the fact never left the module: a saved search kept a
/// stale title, a member holding a reminder was never told the venue moved, and the automation
/// platform's <c>event.updated</c> trigger had nothing behind it. One handler and one contract close
/// all three.
/// </para>
/// </summary>
public sealed class CommunityEventUpdatedEventHandler
    : DomainEventHandler<CommunityEventUpdatedDomainEvent>
{
    private readonly CommunityLifeIntegrationEventRelay _relay;

    public CommunityEventUpdatedEventHandler(CommunityLifeIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        CommunityEventUpdatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
