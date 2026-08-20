using Diaspora.Contracts.Events;
using Diaspora.Infrastructure.Services;
using Diaspora.Platform.Messaging;
using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Infrastructure.Messaging;

/// <summary>
/// The B1.1 implementation of the platform's integration event port: durable enqueue instead
/// of best-effort publish.
/// <para>
/// <b>This one class is the entire behavioural change, and every module is oblivious to it.</b>
/// Trust, Location, Media, Community and Provider each call
/// <c>IIntegrationEventPublisher.PublishAsync</c> from a relay, exactly as they did before. That
/// call used to reach the in-process dispatcher and run every consumer synchronously, inside
/// somebody else's transaction, with failures swallowed by the relay. It now writes a row.
/// Nothing above this line moved, which is what "no business module changes" means in practice.
/// </para>
/// <para>
/// <b>It no longer swallows.</b> Each relay wraps its call in a catch that logs and continues,
/// on the reasoning that a failed notification must not roll back a successful write. That was
/// the right call when publishing meant running consumers; it is the wrong one now. Enqueuing
/// fails only if the event cannot be serialized or exceeds the payload ceiling — both of which
/// are defects in the event itself, and both of which should take the transaction down rather
/// than commit an aggregate whose announcement was silently dropped. The relays' catch blocks
/// still exist and now catch a case that no longer occurs, which is why they needed no edit.
/// </para>
/// <para>
/// <b>B1.2 made this the only route.</b> It was one of two before: Trust and Community reached
/// the outbox through <c>OutboxRoutingDomainEventDispatcher</c>, a compensating interception on
/// the domain dispatcher that existed solely because an integration event was also a domain
/// event. Severing that inheritance removed the second route, and the interception with it.
/// </para>
/// </summary>
public sealed class OutboxIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly IOutboxWriter _outbox;
    private readonly MediatRIntegrationEventDispatcher _dispatcher;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxIntegrationEventPublisher> _logger;

    public OutboxIntegrationEventPublisher(
        IOutboxWriter outbox,
        MediatRIntegrationEventDispatcher dispatcher,
        IOptions<OutboxOptions> options,
        ILogger<OutboxIntegrationEventPublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _outbox = outbox;
        _dispatcher = dispatcher;
        _options = options.Value;
        _logger = logger;
    }

    public Task PublishAsync(
        IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (!_options.Enabled)
        {
            // The escape hatch, and the pre-B1.1 behaviour in full — including the data loss.
            // Logged at warning on every publish so a deployment cannot sit here quietly: the
            // switch exists to buy an afternoon, not a quarter.
            _logger.LogWarning(
                "The outbox is disabled; {EventType} is being published in-process and will be " +
                "lost if this publish or the surrounding transaction fails.",
                integrationEvent.GetType().Name);

            return _dispatcher.DispatchAsync([integrationEvent], cancellationToken);
        }

        var messageId = _outbox.Enqueue(integrationEvent);

        _logger.LogDebug(
            "Enqueued {EventType} ({MessageId}) for delivery. It commits with the aggregate.",
            integrationEvent.GetType().Name, messageId);

        // No I/O happened, so there is nothing to await. Returning a completed task rather than
        // making the method async avoids a state machine on a path that every publishing
        // command runs.
        return Task.CompletedTask;
    }
}
