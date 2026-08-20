using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Domain.Events;

namespace Diaspora.Modules.Financial.Application.EventHandlers;

/// <summary>
/// Announces that a wallet now exists.
/// <para>
/// One line, because the translation lives in <see cref="FinancialIntegrationEventRelay"/>. One
/// handler per event rather than a switch inside one keeps each subscription independent: a failure
/// translating one fact cannot stop another being announced.
/// </para>
/// </summary>
public sealed class WalletCreatedEventHandler : DomainEventHandler<WalletCreatedDomainEvent>
{
    private readonly FinancialIntegrationEventRelay _relay;

    public WalletCreatedEventHandler(FinancialIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        WalletCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
