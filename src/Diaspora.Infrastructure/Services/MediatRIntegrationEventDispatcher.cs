using System.Collections.Concurrent;
using System.Reflection;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diaspora.Infrastructure.Services;

/// <summary>
/// Hands an integration event to the in-process consumers registered for it.
/// <para>
/// <b>This is delivery, not publication.</b> Nothing in a business module resolves it: a producer
/// calls <c>IIntegrationEventPublisher</c>, which writes an outbox row and returns. This class is
/// reached only from the far side of the outbox, by <c>InProcessOutboxTransport</c>, once the
/// message has been claimed for delivery. Keeping it concrete rather than behind a port is
/// deliberate — a broker transport would not use it at all, and an interface here would suggest
/// there is a choice to make where there is not.
/// </para>
/// <para>
/// It is a near-copy of <see cref="MediatRDomainEventDispatcher"/> and stays one. Collapsing the
/// two onto a shared <c>IMessage</c>-constrained wrapper would restore, in the transport layer,
/// exactly the substitutability B1.2 removed from the type hierarchy: one notification type
/// would reach both pipelines' handlers, and which one ran would be a registration accident. The
/// duplication is about thirty lines and it is the enforcement.
/// </para>
/// </summary>
public sealed class MediatRIntegrationEventDispatcher
{
    /// <summary>
    /// Cached wrapper constructors, keyed by concrete event type. The reflection that builds a
    /// closed generic is not something to repeat on every delivery.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Func<IIntegrationEvent, INotification>> WrapperFactories = new();

    private static readonly MethodInfo WrapMethod = typeof(MediatRIntegrationEventDispatcher)
        .GetMethod(nameof(Wrap), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly IPublisher _publisher;
    private readonly ILogger<MediatRIntegrationEventDispatcher> _logger;

    public MediatRIntegrationEventDispatcher(
        IPublisher publisher, ILogger<MediatRIntegrationEventDispatcher> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task DispatchAsync(
        IEnumerable<IIntegrationEvent> integrationEvents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvents);

        foreach (var integrationEvent in integrationEvents)
        {
            _logger.LogDebug(
                "Delivering integration event {EventType} ({EventId})",
                integrationEvent.GetType().Name, integrationEvent.EventId);

            var notification = WrapperFor(integrationEvent.GetType())(integrationEvent);

            // Anything a consumer throws propagates to the transport, which propagates it to the
            // dispatcher, which retries and eventually dead-letters. Swallowing here would assert
            // a delivery that did not happen.
            await _publisher.Publish(notification, cancellationToken);
        }
    }

    private static Func<IIntegrationEvent, INotification> WrapperFor(Type eventType) =>
        WrapperFactories.GetOrAdd(eventType, static type =>
        {
            var closed = WrapMethod.MakeGenericMethod(type);
            return closed.CreateDelegate<Func<IIntegrationEvent, INotification>>();
        });

    // The return type is fixed by the cached delegate signature; narrowing it to the closed
    // generic would not bind.
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance", "CA1859:Use concrete types when possible for improved performance",
        Justification = "Bound to Func<IIntegrationEvent, INotification> in the wrapper cache.")]
    private static INotification Wrap<TIntegrationEvent>(IIntegrationEvent integrationEvent)
        where TIntegrationEvent : IIntegrationEvent =>
        new IntegrationEventNotification<TIntegrationEvent>((TIntegrationEvent)integrationEvent);
}
