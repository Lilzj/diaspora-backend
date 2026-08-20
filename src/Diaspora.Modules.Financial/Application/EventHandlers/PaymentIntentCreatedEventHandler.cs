using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Domain.Events;

namespace Diaspora.Modules.Financial.Application.EventHandlers;

/// <summary>
/// Announces that somebody started paying.
/// <para>
/// One line, because the translation lives in <see cref="FinancialIntegrationEventRelay"/>. One
/// handler per event rather than a switch inside one keeps each subscription independent: a failure
/// translating one fact cannot stop another being announced.
/// </para>
/// </summary>
public sealed class PaymentIntentCreatedEventHandler : DomainEventHandler<PaymentIntentCreatedDomainEvent>
{
    private readonly FinancialIntegrationEventRelay _relay;

    public PaymentIntentCreatedEventHandler(FinancialIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        PaymentIntentCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
