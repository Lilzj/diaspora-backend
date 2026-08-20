using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Domain.Events;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Relays an archival outward.
/// <para>
/// The signal to stop surfacing a guide. Archived knowledge is out of date rather than wrong, so
/// it stays readable at its permalink and stops appearing in discovery — a consumer that deletes
/// its index entry rather than demoting it breaks every link that already exists.
/// </para>
/// </summary>
public sealed class GuideArchivedEventHandler : DomainEventHandler<GuideArchivedDomainEvent>
{
    private readonly GuideIntegrationEventRelay _relay;

    public GuideArchivedEventHandler(GuideIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        GuideArchivedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
