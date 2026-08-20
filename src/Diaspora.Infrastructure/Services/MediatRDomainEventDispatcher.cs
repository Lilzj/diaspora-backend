using System.Collections.Concurrent;
using System.Reflection;
using Diaspora.Application.Abstractions;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Primitives;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diaspora.Infrastructure.Services;

/// <summary>
/// Publishes domain events to their in-process handlers, inside the transaction that raised them.
/// <para>
/// <b>Only domain events reach it, and since B1.2 that is a compile-time fact rather than a
/// convention.</b> <c>IDomainEventDispatcher</c> takes <c>IEnumerable&lt;IDomainEvent&gt;</c>, and
/// an integration event is no longer one, so the route Trust and Community used to publish 29 of
/// the platform's 42 contracts does not typecheck any more. Integration events travel through
/// <c>IIntegrationEventPublisher</c> to the outbox and reach consumers via
/// <see cref="MediatRIntegrationEventDispatcher"/>.
/// </para>
/// </summary>
public sealed class MediatRDomainEventDispatcher : IDomainEventDispatcher
{
    /// <summary>
    /// Cached wrapper constructors, keyed by concrete event type. Building the closed
    /// generic through reflection on every dispatch would be needless work on the
    /// authentication hot path.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Func<IDomainEvent, INotification>> WrapperFactories = new();

    private static readonly MethodInfo WrapMethod = typeof(MediatRDomainEventDispatcher)
        .GetMethod(nameof(Wrap), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly IPublisher _publisher;
    private readonly ILogger<MediatRDomainEventDispatcher> _logger;

    public MediatRDomainEventDispatcher(IPublisher publisher, ILogger<MediatRDomainEventDispatcher> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            _logger.LogDebug("Dispatching domain event {EventType} ({EventId})",
                domainEvent.GetType().Name, domainEvent.EventId);

            // Wrapped so the Domain project stays framework-free. Before B1.2 this line also
            // carried an `as INotification` fallback, for integration events that had arrived
            // here by deriving from DomainEvent. Nothing can arrive that way now.
            var notification = WrapperFor(domainEvent.GetType())(domainEvent);

            await _publisher.Publish(notification, cancellationToken);
        }
    }

    private static Func<IDomainEvent, INotification> WrapperFor(Type eventType) =>
        WrapperFactories.GetOrAdd(eventType, static type =>
        {
            var closed = WrapMethod.MakeGenericMethod(type);
            return closed.CreateDelegate<Func<IDomainEvent, INotification>>();
        });

    // The return type is fixed by the cached delegate signature; narrowing it to the
    // closed generic would not bind.
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance", "CA1859:Use concrete types when possible for improved performance",
        Justification = "Bound to Func<IDomainEvent, INotification> in the wrapper cache.")]
    private static INotification Wrap<TDomainEvent>(IDomainEvent domainEvent)
        where TDomainEvent : IDomainEvent =>
        new DomainEventNotification<TDomainEvent>((TDomainEvent)domainEvent);
}
