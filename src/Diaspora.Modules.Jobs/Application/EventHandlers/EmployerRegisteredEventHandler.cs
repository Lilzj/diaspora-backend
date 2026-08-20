using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Domain.Events;

namespace Diaspora.Modules.Jobs.Application.EventHandlers;

/// <summary>An employer was created.</summary>
public sealed class EmployerRegisteredEventHandler
    : DomainEventHandler<EmployerRegisteredDomainEvent>
{
    private readonly JobIntegrationEventRelay _relay;

    public EmployerRegisteredEventHandler(JobIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        EmployerRegisteredDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
