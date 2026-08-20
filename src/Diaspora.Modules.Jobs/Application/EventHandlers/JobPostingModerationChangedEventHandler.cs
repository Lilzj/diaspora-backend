using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Events;
using Diaspora.Modules.Jobs.Extensibility.Search;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Jobs.Application.EventHandlers;

/// <summary>
/// A moderation decision changed a posting's standing.
/// <para>
/// <b>The hidden transition must reach the index immediately, and this is the most urgent
/// de-index in the module.</b> Fraudulent job adverts are the most damaging content this platform
/// can carry, and the gap between a moderator hiding one and the index forgetting it is the window
/// in which somebody loses money.
/// </para>
/// </summary>
public sealed class JobPostingModerationChangedEventHandler
    : DomainEventHandler<JobPostingModerationChangedDomainEvent>
{
    private readonly IJobSearchEngine _search;
    private readonly ILogger<JobPostingModerationChangedEventHandler> _logger;

    public JobPostingModerationChangedEventHandler(
        IJobSearchEngine search, ILogger<JobPostingModerationChangedEventHandler> logger)
    {
        _search = search;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        JobPostingModerationChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (domainEvent.CurrentState != JobModerationState.Hidden)
        {
            // Unhiding is handled by the ordinary re-index path: an unhidden posting is an
            // edited posting as far as the index is concerned, and there is no urgency.
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
#pragma warning disable CA1031 // Logged at error: this is the one de-index that must not be lost.
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "De-indexing hidden job {JobId} failed. It may still be discoverable.",
                domainEvent.PostingId.Value);
        }
#pragma warning restore CA1031
    }
}
