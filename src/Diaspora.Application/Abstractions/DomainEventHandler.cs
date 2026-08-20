using Diaspora.SharedKernel.Primitives;
using MediatR;

namespace Diaspora.Application.Abstractions;

/// <summary>
/// Base class for domain event handlers. Unwraps <see cref="DomainEventNotification{T}"/>
/// so implementations deal in domain events, not in transport plumbing.
/// <para>
/// <b>The constraint is load-bearing since B1.2.</b> <c>TDomainEvent : IDomainEvent</c> no longer
/// admits an integration event, so <c>DomainEventHandler&lt;SomeIntegrationEvent&gt;</c> is a
/// compile error. Consuming a published contract means deriving
/// <see cref="IntegrationEventHandler{T}"/> instead — a different pipeline, a different delivery
/// guarantee, and now a different base class.
/// </para>
/// <para>
/// A domain event handler runs inline, inside the transaction that raised the event, and may
/// read uncommitted state from the same unit of work. That is the opposite of what an
/// integration event handler may assume, which is why the two were worth separating.
/// </para>
/// </summary>
public abstract class DomainEventHandler<TDomainEvent> : INotificationHandler<DomainEventNotification<TDomainEvent>>
    where TDomainEvent : IDomainEvent
{
    public Task Handle(DomainEventNotification<TDomainEvent> notification, CancellationToken cancellationToken) =>
        HandleAsync(notification.DomainEvent, cancellationToken);

    protected abstract Task HandleAsync(TDomainEvent domainEvent, CancellationToken cancellationToken);
}
