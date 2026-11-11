using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Domain.Events;

namespace Diaspora.Modules.Financial.Application.EventHandlers;

/// <summary>Announces a new payout destination so the member hears about it (BSEC-007).</summary>
public sealed class PayoutDestinationRegisteredEventHandler
    : DomainEventHandler<PayoutDestinationRegisteredDomainEvent>
{
    private readonly FinancialIntegrationEventRelay _relay;

    public PayoutDestinationRegisteredEventHandler(FinancialIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        PayoutDestinationRegisteredDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
