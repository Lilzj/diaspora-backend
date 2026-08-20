using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Domain.Events;

namespace Diaspora.Modules.Jobs.Application.EventHandlers;

/// <summary>A posting was drafted. Audited and counted; deliberately not indexed or announced.</summary>
public sealed class JobPostingCreatedEventHandler
    : DomainEventHandler<JobPostingCreatedDomainEvent>
{
    private readonly JobIntegrationEventRelay _relay;

    public JobPostingCreatedEventHandler(JobIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        JobPostingCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
