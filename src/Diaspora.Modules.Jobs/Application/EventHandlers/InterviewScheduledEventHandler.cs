using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Domain.Events;
using Diaspora.Modules.Jobs.Observability;

namespace Diaspora.Modules.Jobs.Application.EventHandlers;

/// <summary>
/// An interview was scheduled or moved. Publishes the contract that produces the module's
/// highest-stakes notification — a candidate who misses it has lost the job.
/// </summary>
public sealed class InterviewScheduledEventHandler
    : DomainEventHandler<InterviewScheduledDomainEvent>
{
    private readonly JobIntegrationEventRelay _relay;

    public InterviewScheduledEventHandler(JobIntegrationEventRelay relay) => _relay = relay;

    protected override async Task HandleAsync(
        InterviewScheduledDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _relay.PublishAsync(domainEvent, cancellationToken);

        // Reschedules are not new interviews and must not inflate the count.
        if (!domainEvent.IsReschedule)
        {
            JobTelemetry.InterviewsScheduled.Add(1);
        }
    }
}
