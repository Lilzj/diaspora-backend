using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Domain.Events;
using Diaspora.Modules.Jobs.Observability;

namespace Diaspora.Modules.Jobs.Application.EventHandlers;

/// <summary>Terms went to a candidate.</summary>
public sealed class OfferExtendedEventHandler : DomainEventHandler<OfferExtendedDomainEvent>
{
    private readonly JobIntegrationEventRelay _relay;

    public OfferExtendedEventHandler(JobIntegrationEventRelay relay) => _relay = relay;

    protected override async Task HandleAsync(
        OfferExtendedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _relay.PublishAsync(domainEvent, cancellationToken);

        JobTelemetry.OffersExtended.Add(1);
    }
}
