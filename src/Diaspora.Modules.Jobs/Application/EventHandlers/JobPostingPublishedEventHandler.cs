using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Events;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Extensibility.Search;
using Diaspora.Modules.Jobs.Observability;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Jobs.Application.EventHandlers;

/// <summary>
/// A posting became visible: publish the contract, index it, count it.
/// <para>
/// <b>Indexing is best-effort and deliberately does not fail the publication.</b> A posting that
/// is live but not yet indexed is discoverable by every other route and will be picked up by the
/// next reconciliation pass; a publication rolled back because the index was briefly unreachable
/// would lose the employer's work.
/// </para>
/// </summary>
public sealed class JobPostingPublishedEventHandler
    : DomainEventHandler<JobPostingPublishedDomainEvent>
{
    private readonly JobIntegrationEventRelay _relay;
    private readonly IJobSearchEngine _search;
    private readonly IJobPostingRepository _postings;
    private readonly IEmployerProfileRepository _employers;
    private readonly ILogger<JobPostingPublishedEventHandler> _logger;

    public JobPostingPublishedEventHandler(
        JobIntegrationEventRelay relay,
        IJobSearchEngine search,
        IJobPostingRepository postings,
        IEmployerProfileRepository employers,
        ILogger<JobPostingPublishedEventHandler> logger)
    {
        _relay = relay;
        _search = search;
        _postings = postings;
        _employers = employers;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        JobPostingPublishedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _relay.PublishAsync(domainEvent, cancellationToken);

        JobTelemetry.JobsPublished.Add(
            1,
            new KeyValuePair<string, object?>("country", domainEvent.CountryCode),
            new KeyValuePair<string, object?>("category", domainEvent.CategoryKey));

        await IndexAsync(domainEvent, cancellationToken);
    }

    private async Task IndexAsync(
        JobPostingPublishedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        try
        {
            var posting = await _postings.GetAsync(domainEvent.PostingId, cancellationToken);
            var employer = await _employers.GetAsync(domainEvent.EmployerId, cancellationToken);

            if (posting is null || employer is null)
            {
                return;
            }

            var document = JobSearchProjectionService.Project(posting, employer);

            if (document is not null)
            {
                await _search.IndexAsync(document, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // An index failure must not roll back a publication.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Indexing job {JobId} failed. It is live and will be picked up by the next sweep.",
                domainEvent.PostingId.Value);
        }
#pragma warning restore CA1031
    }
}
