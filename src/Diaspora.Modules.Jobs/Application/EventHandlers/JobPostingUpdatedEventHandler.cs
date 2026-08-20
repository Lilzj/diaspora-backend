using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Events;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Extensibility.Search;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Jobs.Application.EventHandlers;

/// <summary>
/// A posting changed: publish the contract and re-index it.
/// <para>
/// Re-indexing is best-effort. A stale index entry misranks a result; a rolled-back edit loses
/// the recruiter's correction — and the corrections that matter most are exactly the ones a
/// candidate is relying on, like a fixed salary or a corrected closing date.
/// </para>
/// </summary>
public sealed class JobPostingUpdatedEventHandler
    : DomainEventHandler<JobPostingUpdatedDomainEvent>
{
    private readonly JobIntegrationEventRelay _relay;
    private readonly IJobSearchEngine _search;
    private readonly IJobPostingRepository _postings;
    private readonly IEmployerProfileRepository _employers;
    private readonly ILogger<JobPostingUpdatedEventHandler> _logger;

    public JobPostingUpdatedEventHandler(
        JobIntegrationEventRelay relay,
        IJobSearchEngine search,
        IJobPostingRepository postings,
        IEmployerProfileRepository employers,
        ILogger<JobPostingUpdatedEventHandler> logger)
    {
        _relay = relay;
        _search = search;
        _postings = postings;
        _employers = employers;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        JobPostingUpdatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _relay.PublishAsync(domainEvent, cancellationToken);

        try
        {
            var posting = await _postings.GetAsync(domainEvent.PostingId, cancellationToken);
            var employer = await _employers.GetAsync(domainEvent.EmployerId, cancellationToken);

            if (posting is null || employer is null)
            {
                return;
            }

            var document = JobSearchProjectionService.Project(posting, employer);

            // Null means it should no longer be discoverable — an edit that made it private,
            // for instance. Removing rather than skipping is the safe direction.
            if (document is null)
            {
                await _search.RemoveAsync(posting.Id.Value, cancellationToken);

                return;
            }

            await _search.IndexAsync(document, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // An index failure must not roll back an edit.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Re-indexing job {JobId} after an edit failed.",
                domainEvent.PostingId.Value);
        }
#pragma warning restore CA1031
    }
}
