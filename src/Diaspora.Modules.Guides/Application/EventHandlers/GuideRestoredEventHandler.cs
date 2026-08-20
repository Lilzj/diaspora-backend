using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Domain.Events;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Relays a return from the archive outward.
/// <para>
/// The first of the two restore paths. Both land on one published contract, distinguished by
/// <c>RestoredFrom</c> — see <see cref="GuideRestoreOrigins"/> for why a consumer cares which one
/// it was.
/// </para>
/// </summary>
public sealed class GuideRestoredEventHandler : DomainEventHandler<GuideRestoredDomainEvent>
{
    private readonly GuideIntegrationEventRelay _relay;

    public GuideRestoredEventHandler(GuideIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        GuideRestoredDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
