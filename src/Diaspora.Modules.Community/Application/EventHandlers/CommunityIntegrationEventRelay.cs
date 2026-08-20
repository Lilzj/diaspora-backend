using Diaspora.Contracts.Events;
using Diaspora.Platform.Messaging;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Community.Application.EventHandlers;

/// <summary>
/// The one place outbound integration events leave this module.
/// <para>
/// <b>B1.2 corrected the route.</b> This class used to publish through
/// <see cref="Diaspora.Platform.Services.Abstractions.IDomainEventDispatcher"/>, with a comment
/// explaining that MediatR's publisher would throw because an <see cref="IntegrationEvent"/> was
/// not an <c>INotification</c> — an accurate description of a workaround for the wrong problem.
/// The real problem was that an integration event <em>was</em> a domain event, so the domain
/// dispatcher accepted it and the outbox had to be bolted onto that dispatcher to catch this
/// module and Trust. The hierarchies are now separate:
/// <see cref="IIntegrationEventPublisher"/> is the only thing that accepts an integration event,
/// and handing one to the domain dispatcher does not compile.
/// </para>
/// <para>
/// <b>On failure.</b> These run after the aggregate is committed, so a publish that throws must
/// not unwind a committed post or a decided moderation case. Failures are logged and swallowed;
/// the write stands. Since B1.1 publishing means enqueuing inside the caller's own transaction,
/// so the only failures left are an unserialisable or oversized payload — defects in the event
/// rather than in the world. The catch stays because the guarantee it makes to callers is still
/// worth making, not because it is expected to fire.
/// </para>
/// <para>
/// Every translation goes through this one class rather than each handler publishing for itself,
/// which is why B1.1 had exactly one seam to occupy and B1.2 had exactly one line to change.
/// </para>
/// </summary>
public sealed class CommunityIntegrationEventRelay
{
    private readonly IIntegrationEventPublisher _publisher;
    private readonly ILogger<CommunityIntegrationEventRelay> _logger;

    public CommunityIntegrationEventRelay(
        IIntegrationEventPublisher publisher, ILogger<CommunityIntegrationEventRelay> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task PublishAsync(
        IntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        try
        {
            await _publisher.PublishAsync(integrationEvent, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A publish must not unwind a committed write. See the summary.
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Publishing {EventType} ({EventId}) failed. The write stands and the event is lost.",
                integrationEvent.GetType().Name,
                integrationEvent.EventId);
        }
#pragma warning restore CA1031
    }
}
