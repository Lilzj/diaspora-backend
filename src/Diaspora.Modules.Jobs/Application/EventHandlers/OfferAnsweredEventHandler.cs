using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Domain.Analytics;
using Diaspora.Modules.Jobs.Domain.Applications;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Events;
using Diaspora.Modules.Jobs.Observability;

namespace Diaspora.Modules.Jobs.Application.EventHandlers;

/// <summary>
/// An offer reached an ending.
/// <para>
/// An acceptance is the end of the funnel and the only fact in this module that means somebody
/// got a job. The time-to-hire measurement is taken here, from the application's own submission
/// instant — the honest span, which is what the candidate actually experienced, not the time
/// since the offer went out.
/// </para>
/// </summary>
public sealed class OfferAnsweredEventHandler : DomainEventHandler<OfferAnsweredDomainEvent>
{
    private readonly JobIntegrationEventRelay _relay;
    private readonly IJobAnalyticsRepository _analytics;
    private readonly IJobApplicationRepository _applications;

    public OfferAnsweredEventHandler(
        JobIntegrationEventRelay relay,
        IJobAnalyticsRepository analytics,
        IJobApplicationRepository applications)
    {
        _relay = relay;
        _analytics = analytics;
        _applications = applications;
    }

    protected override async Task HandleAsync(
        OfferAnsweredDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _relay.PublishAsync(domainEvent, cancellationToken);

        if (domainEvent.Status != OfferStatus.Accepted)
        {
            return;
        }

        JobTelemetry.OffersAccepted.Add(1);

        var application = await _applications.GetAsync(
            domainEvent.ApplicationId, cancellationToken);

        var rollup = await _analytics.GetForPostingAsync(
            domainEvent.PostingId, cancellationToken);

        if (application is null || rollup is null)
        {
            return;
        }

        // From submission, not from the offer. That is the span the candidate lived through.
        var timeToHire = domainEvent.AnsweredAt - application.SubmittedAt;

        rollup.RecordHire(timeToHire);

        JobTelemetry.TimeToHireDays.Record(timeToHire.TotalDays);
    }
}
