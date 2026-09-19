using Diaspora.Contracts.Events;
using Diaspora.Platform.Messaging;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Location.Application.EventHandlers;

/// <summary>
/// The one place outbound integration events leave this module.
/// <para>
/// Location was the last module without one, which is a fair description of how much it used to
/// publish: nothing. It consumed five of Trust's contracts and produced none of its own, so a
/// residence history that only this module could see stayed only this module's business. The
/// first thing to leave is a departure, because that is the one fact in here another module has
/// always needed and never received.
/// </para>
/// <para>
/// <b>On failure.</b> This runs after the residence is committed, so a publish that throws must
/// not unwind it — the member did move, whatever the transport thinks. Failures are logged and
/// swallowed and the write stands. Publishing enqueues inside the caller's transaction, so the
/// only failures left are an unserialisable or oversized payload; the catch stays for the
/// guarantee it makes, not because it is expected to fire.
/// </para>
/// </summary>
public sealed class LocationIntegrationEventRelay
{
    private readonly IIntegrationEventPublisher _publisher;
    private readonly ILogger<LocationIntegrationEventRelay> _logger;

    public LocationIntegrationEventRelay(
        IIntegrationEventPublisher publisher, ILogger<LocationIntegrationEventRelay> logger)
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
