using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Domain.Analytics;
using Diaspora.Modules.Jobs.Domain.Events;
using Diaspora.Modules.Jobs.Observability;

namespace Diaspora.Modules.Jobs.Application.EventHandlers;

/// <summary>
/// A candidate withdrew.
/// <para>
/// Counted separately from a rejection, deliberately: a withdrawal says nothing about the
/// candidate's quality and must not count against the employer's response reputation. Products
/// that collapse the two quietly penalise everyone involved.
/// </para>
/// </summary>
public sealed class JobApplicationWithdrawnEventHandler
    : DomainEventHandler<JobApplicationWithdrawnDomainEvent>
{
    private readonly JobIntegrationEventRelay _relay;
    private readonly IJobAnalyticsRepository _analytics;

    public JobApplicationWithdrawnEventHandler(
        JobIntegrationEventRelay relay, IJobAnalyticsRepository analytics)
    {
        _relay = relay;
        _analytics = analytics;
    }

    protected override async Task HandleAsync(
        JobApplicationWithdrawnDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _relay.PublishAsync(domainEvent, cancellationToken);

        JobTelemetry.ApplicationsWithdrawn.Add(1);

        var rollup = await _analytics.GetForPostingAsync(
            domainEvent.PostingId, cancellationToken);

        rollup?.RecordWithdrawal();
    }
}
