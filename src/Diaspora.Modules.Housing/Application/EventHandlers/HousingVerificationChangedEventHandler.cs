using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Domain.Events;

namespace Diaspora.Modules.Housing.Application.EventHandlers;

/// <summary>
/// Publishes that Housing has caught up with a Trust decision.
/// <para>
/// Revocations travel on the same contract with IsVerified false. A consumer that subscribed only
/// to grants would keep showing a badge Trust had withdrawn.
/// </para>
/// </summary>
public sealed class HousingVerificationChangedEventHandler
    : DomainEventHandler<HousingVerificationChangedDomainEvent>
{
    private readonly HousingIntegrationEventRelay _relay;

    public HousingVerificationChangedEventHandler(HousingIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        HousingVerificationChangedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
