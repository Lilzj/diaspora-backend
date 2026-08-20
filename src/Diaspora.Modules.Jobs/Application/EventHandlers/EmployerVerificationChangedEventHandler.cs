using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Domain.Events;

namespace Diaspora.Modules.Jobs.Application.EventHandlers;

/// <summary>
/// Trust changed an employer's mark.
/// <para>
/// The revocation direction is the one that must not be missed: a stale badge on a posting from
/// an employer Trust has just revoked is the platform vouching for exactly the wrong party.
/// Every consumer holding a denormalised copy acts on the contract this publishes.
/// </para>
/// </summary>
public sealed class EmployerVerificationChangedEventHandler
    : DomainEventHandler<EmployerVerificationChangedDomainEvent>
{
    private readonly JobIntegrationEventRelay _relay;

    public EmployerVerificationChangedEventHandler(JobIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        EmployerVerificationChangedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
