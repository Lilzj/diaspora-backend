using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Domain.Analytics;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Events;
using Diaspora.Modules.Jobs.Observability;

namespace Diaspora.Modules.Jobs.Application.EventHandlers;

/// <summary>
/// An application moved: publish the contract and advance the funnel.
/// <para>
/// The response histogram is recorded on the first employer engagement only, which is what makes
/// the platform-wide "how long do employers take" figure a measurement rather than an average of
/// every status change.
/// </para>
/// </summary>
public sealed class JobApplicationStatusChangedEventHandler
    : DomainEventHandler<JobApplicationStatusChangedDomainEvent>
{
    private readonly JobIntegrationEventRelay _relay;
    private readonly IJobAnalyticsRepository _analytics;

    public JobApplicationStatusChangedEventHandler(
        JobIntegrationEventRelay relay, IJobAnalyticsRepository analytics)
    {
        _relay = relay;
        _analytics = analytics;
    }

    protected override async Task HandleAsync(
        JobApplicationStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _relay.PublishAsync(domainEvent, cancellationToken);

        if (domainEvent.IsFirstEmployerResponse)
        {
            JobTelemetry.ApplicationsAnswered.Add(1);
        }

        var rollup = await _analytics.GetForPostingAsync(
            domainEvent.PostingId, cancellationToken);

        if (rollup is null)
        {
            return;
        }

        switch (domainEvent.CurrentStatus)
        {
            case ApplicationStatus.Shortlisted:
                rollup.RecordShortlisted();
                break;

            case ApplicationStatus.InterviewCompleted:
                rollup.RecordInterviewed();
                break;

            case ApplicationStatus.OfferExtended:
                rollup.RecordOffered();
                break;

            case ApplicationStatus.Rejected:
                rollup.RecordRejected();
                break;
        }
    }
}
