using Diaspora.Contracts.Events;
using MediatR;

namespace Diaspora.Application.Abstractions;

/// <summary>
/// Base class for a consumer of a published contract.
/// <para>
/// <b>The consumer half of the B1.2 separation.</b> Location and Providers previously subscribed
/// to Trust's contracts by deriving <c>DomainEventHandler&lt;VerificationApprovedIntegrationEvent&gt;</c>
/// — legal only because an integration event was a domain event, and misleading in a way that
/// mattered: it read as though Trust's internal facts were being handled directly across a
/// module boundary. That constraint no longer admits an integration event, so the choice of base
/// class now states which pipeline a handler belongs to, and the compiler holds it to it.
/// </para>
/// <para>
/// <b>What a consumer must assume.</b> Delivery is at-least-once and out of band: this runs on a
/// background thread, in its own scope, after the producer's transaction committed — possibly
/// minutes later, possibly a second time. <c>EventId</c> is the idempotency key. A handler that
/// throws leaves the message to be retried and eventually dead-lettered, which is the correct
/// behaviour and the reason not to swallow.
/// </para>
/// <para>
/// A handler saves its own work. The in-process transport calls <c>SaveChanges</c> on the
/// scope's unit of work after the handlers run, which is what let every pre-outbox consumer keep
/// working untouched, but a handler that needs its write visible to a later step in its own body
/// must save explicitly.
/// </para>
/// </summary>
public abstract class IntegrationEventHandler<TIntegrationEvent>
    : INotificationHandler<IntegrationEventNotification<TIntegrationEvent>>
    where TIntegrationEvent : IIntegrationEvent
{
    public Task Handle(
        IntegrationEventNotification<TIntegrationEvent> notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return HandleAsync(notification.IntegrationEvent, cancellationToken);
    }

    protected abstract Task HandleAsync(
        TIntegrationEvent integrationEvent, CancellationToken cancellationToken);
}
