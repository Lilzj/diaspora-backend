using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Domain.Events;
using Diaspora.Modules.Jobs.Extensibility.Search;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Jobs.Application.EventHandlers;

/// <summary>
/// A posting was soft-deleted: announce the closure and take it out of the index.
/// <para>
/// <b>This handler is the link that was missing.</b> The aggregate raises
/// <see cref="JobPostingDeletedDomainEvent"/> deliberately rather than letting the change tracker
/// delete the row quietly — <c>JobPosting.MarkDeleted</c> and the event's own summary both say so,
/// in the same words: "a delete that happens only in the tracker cannot also raise an event, and
/// downstream de-indexing needs one". <see cref="JobIntegrationEventRelay"/> has carried the
/// translation for that event since the module was written. Nothing connected the two, so the
/// intent recorded in three places had no code behind it.
/// </para>
/// <para>
/// <b>It has no trigger today, and that is a separate gap.</b> The module exposes no delete
/// endpoint and nothing calls <c>IJobPostingRepository.Remove</c>, so a posting cannot currently
/// be deleted at all — unlike a marketplace listing, a housing listing or a community event, each
/// of which has a delete command. Writing the handler makes the chain whole, so that adding the
/// entry point is a one-file change rather than a change that silently leaves postings in search.
/// </para>
/// <para>
/// De-indexing failures are logged rather than thrown, as in the sibling handlers: the posting is
/// already deleted by the time this runs, and throwing here would roll back a completed delete
/// because a search provider was briefly unavailable.
/// </para>
/// </summary>
public sealed class JobPostingDeletedEventHandler
    : DomainEventHandler<JobPostingDeletedDomainEvent>
{
    private readonly JobIntegrationEventRelay _relay;
    private readonly IJobSearchEngine _search;
    private readonly ILogger<JobPostingDeletedEventHandler> _logger;

    public JobPostingDeletedEventHandler(
        JobIntegrationEventRelay relay,
        IJobSearchEngine search,
        ILogger<JobPostingDeletedEventHandler> logger)
    {
        _relay = relay;
        _search = search;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        JobPostingDeletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _relay.PublishAsync(domainEvent, cancellationToken);

        try
        {
            await _search.RemoveAsync(domainEvent.PostingId.Value, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // See the class summary: logged loudly, never thrown.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "De-indexing job {JobId} after it was deleted failed. It may still appear in search.",
                domainEvent.PostingId.Value);
        }
#pragma warning restore CA1031
    }
}
