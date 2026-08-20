using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Domain.Events;

namespace Diaspora.Modules.Financial.Application.EventHandlers;

// One handler per event, each a single line, because the translation lives in
// FinancialIntegrationEventRelay. Independent subscriptions mean a failure translating one fact
// cannot stop another being announced.

/// <summary>Announces that a settled payment bought a community membership. GAP-003.</summary>
public sealed class MembershipPaymentSettledEventHandler
    : DomainEventHandler<MembershipPaymentSettledDomainEvent>
{
    private readonly FinancialIntegrationEventRelay _relay;

    public MembershipPaymentSettledEventHandler(FinancialIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        MembershipPaymentSettledDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

/// <summary>Announces a campaign lifecycle move. GAP-006.</summary>
public sealed class CampaignStatusChangedEventHandler
    : DomainEventHandler<CampaignStatusChangedDomainEvent>
{
    private readonly FinancialIntegrationEventRelay _relay;

    public CampaignStatusChangedEventHandler(FinancialIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        CampaignStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

/// <summary>Announces a campaign update. GAP-007's <c>campaign.update</c>.</summary>
public sealed class CampaignUpdatePublishedEventHandler
    : DomainEventHandler<CampaignUpdatePublishedDomainEvent>
{
    private readonly FinancialIntegrationEventRelay _relay;

    public CampaignUpdatePublishedEventHandler(FinancialIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        CampaignUpdatePublishedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

/// <summary>Announces a milestone. GAP-007's <c>campaign.milestone</c>.</summary>
public sealed class CampaignMilestoneReachedEventHandler
    : DomainEventHandler<CampaignMilestoneReachedDomainEvent>
{
    private readonly FinancialIntegrationEventRelay _relay;

    public CampaignMilestoneReachedEventHandler(FinancialIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        CampaignMilestoneReachedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

/// <summary>Announces a recurring donation that took its money. GAP-008.</summary>
public sealed class RecurringDonationChargedEventHandler
    : DomainEventHandler<RecurringDonationChargedDomainEvent>
{
    private readonly FinancialIntegrationEventRelay _relay;

    public RecurringDonationChargedEventHandler(FinancialIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        RecurringDonationChargedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}

/// <summary>Announces a recurring donation that failed. GAP-008.</summary>
public sealed class RecurringDonationFailedEventHandler
    : DomainEventHandler<RecurringDonationFailedDomainEvent>
{
    private readonly FinancialIntegrationEventRelay _relay;

    public RecurringDonationFailedEventHandler(FinancialIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        RecurringDonationFailedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
