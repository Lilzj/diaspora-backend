using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Domain.Analytics;
using Diaspora.Modules.Jobs.Domain.Candidates;
using Diaspora.Modules.Jobs.Domain.Events;
using Diaspora.Modules.Jobs.Observability;

namespace Diaspora.Modules.Jobs.Application.EventHandlers;

/// <summary>
/// An application arrived: publish the contract and record it in the funnel.
/// <para>
/// The applicant's country is tallied on the analytics rollup — the geographic distribution the
/// brief asks for, and on this platform genuinely useful: an employer who can see that most of
/// their applicants are applying from abroad learns something about whether their sponsorship
/// claim is being read.
/// </para>
/// </summary>
public sealed class JobApplicationSubmittedEventHandler
    : DomainEventHandler<JobApplicationSubmittedDomainEvent>
{
    private readonly JobIntegrationEventRelay _relay;
    private readonly IJobAnalyticsRepository _analytics;
    private readonly ICandidateProfileRepository _candidates;

    public JobApplicationSubmittedEventHandler(
        JobIntegrationEventRelay relay,
        IJobAnalyticsRepository analytics,
        ICandidateProfileRepository candidates)
    {
        _relay = relay;
        _analytics = analytics;
        _candidates = candidates;
    }

    protected override async Task HandleAsync(
        JobApplicationSubmittedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _relay.PublishAsync(domainEvent, cancellationToken);

        JobTelemetry.ApplicationsSubmitted.Add(1);

        var rollup = await _analytics.GetForPostingAsync(
            domainEvent.PostingId, cancellationToken);

        if (rollup is null)
        {
            return;
        }

        var candidate = await _candidates.GetForReadAsync(
            domainEvent.CandidateId, cancellationToken);

        rollup.RecordApplication(candidate?.CountryCode);
    }
}
