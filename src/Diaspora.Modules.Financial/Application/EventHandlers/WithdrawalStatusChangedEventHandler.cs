using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Domain.Events;

namespace Diaspora.Modules.Financial.Application.EventHandlers;

/// <summary>
/// Announces that a payout request moved.
/// <para>
/// <b>This handler did not exist until BSEC-007 (10 Nov 2026), and its absence was invisible.</b>
/// <c>FinancialIntegrationEventRelay</c> has always carried a <c>PublishAsync</c> overload for
/// <see cref="WithdrawalStatusChangedDomainEvent"/> and nothing ever called it — every other relay
/// overload has a one-line handler like this one, and this was the gap in the set. The withdrawal
/// events that did reach the bus came from the sweeps and the payout webhook, which read the
/// aggregate directly; the transitions a <em>member</em> causes, requesting and cancelling, reached
/// nobody.
/// </para>
/// <para>
/// Nothing failed, which is why it survived: a relay method with no caller compiles, an aggregate
/// that raises an event nobody handles commits, and a published contract with no consumer is
/// indistinguishable from one whose consumers are elsewhere.
/// </para>
/// </summary>
public sealed class WithdrawalStatusChangedEventHandler
    : DomainEventHandler<WithdrawalStatusChangedDomainEvent>
{
    private readonly FinancialIntegrationEventRelay _relay;

    public WithdrawalStatusChangedEventHandler(FinancialIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        WithdrawalStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
