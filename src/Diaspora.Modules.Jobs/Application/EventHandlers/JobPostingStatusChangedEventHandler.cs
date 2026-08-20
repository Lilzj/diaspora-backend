using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Events;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Extensibility.Search;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Jobs.Application.EventHandlers;

/// <summary>
/// A posting moved through its lifecycle: publish the contract and de-index if it has left
/// public view.
/// <para>
/// <b>De-indexing is the half that must not be missed.</b> A closed or filled posting left in a
/// search index is a candidate spending an afternoon on an application nobody will read — the
/// single most corrosive thing a job board does to the people using it. A failure here is logged
/// at warning rather than debug for exactly that reason, and the reconciliation sweep re-checks
/// it.
/// </para>
/// </summary>
public sealed class JobPostingStatusChangedEventHandler
    : DomainEventHandler<JobPostingStatusChangedDomainEvent>
{
    private readonly JobIntegrationEventRelay _relay;
    private readonly IJobSearchEngine _search;
    private readonly ILogger<JobPostingStatusChangedEventHandler> _logger;

    public JobPostingStatusChangedEventHandler(
        JobIntegrationEventRelay relay,
        IJobSearchEngine search,
        ILogger<JobPostingStatusChangedEventHandler> logger)
    {
        _relay = relay;
        _search = search;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        JobPostingStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _relay.PublishAsync(domainEvent, cancellationToken);

        // Left public view, or stopped taking applications. Either way it should not be a
        // search result any more.
        var shouldRemove =
            !JobStatusTransitions.IsPubliclyVisible(domainEvent.CurrentStatus)
            || domainEvent.CurrentStatus is JobStatus.Filled or JobStatus.Expired;

        if (!shouldRemove)
        {
            return;
        }

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
                "De-indexing job {JobId} after it reached {Status} failed. It may still appear in search.",
                domainEvent.PostingId.Value,
                domainEvent.CurrentStatus);
        }
#pragma warning restore CA1031
    }
}
