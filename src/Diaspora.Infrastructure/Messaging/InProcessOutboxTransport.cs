using Diaspora.Contracts.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Infrastructure.Services;
using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.Infrastructure.Messaging;

/// <summary>
/// Delivers a message to the in-process consumers, which is the correct transport for a
/// modular monolith.
/// <para>
/// <b>The seam that makes the rest of B1.1 transport-agnostic.</b> Everything the outbox does —
/// durability, ordering, retries, dead letters, replay, telemetry — is already in place and
/// stays in place when a module is extracted. That day, a broker client implements this
/// interface and this class is deleted; no consumer, no relay and no dispatcher code changes.
/// </para>
/// <para>
/// <b>Each message gets its own scope, and that is not a detail.</b> A consumer resolves a
/// <c>DbContext</c>, writes a projection, and expects to own its unit of work. Sharing the
/// dispatcher's scope would mean one consumer's half-written state could be flushed by the
/// outbox's own bookkeeping save — an aggregate committed as a side effect of marking a message
/// delivered. A scope per message also matches how a broker consumer behaves, so a future
/// extraction changes nothing about the environment a handler runs in.
/// </para>
/// <para>
/// The scope's unit of work is saved after the handlers run, because a consumer written against
/// the domain event pipeline never had to save its own work — the producer's <c>SaveChanges</c>
/// did it. Preserving that is what lets every existing handler keep working untouched.
/// </para>
/// </summary>
public sealed class InProcessOutboxTransport : IOutboxTransport
{
    private readonly IServiceScopeFactory _scopes;

    public InProcessOutboxTransport(IServiceScopeFactory scopes)
    {
        _scopes = scopes;
    }

    public string Name => "in-process";

    public async Task PublishAsync(
        IIntegrationEvent integrationEvent,
        OutboxPublishContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        using var scope = _scopes.CreateScope();

        // Restores the producing request's correlation identifier onto the consumer's thread,
        // so a log line written by a handler minutes later still joins the trail of the request
        // that caused it. Without this the whole chain breaks at the outbox boundary, and
        // "which request caused this projection" becomes unanswerable.
        //
        // Reached through the concrete type on purpose: ICorrelationContext is a read-only
        // contract that other modules depend on, and widening it to add setters — so that one
        // infrastructure component can restore a value — would be a contract change made for
        // the convenience of the caller rather than the benefit of the callers.
        if (scope.ServiceProvider.GetService<ICorrelationContext>() is Services.DefaultCorrelationContext correlation &&
            context.CorrelationId is { Length: > 0 } correlationId)
        {
            correlation.CorrelationId = correlationId;
            correlation.CausationId = context.MessageId.ToString();
        }

        // The integration event pipeline, which since B1.2 is a separate one end to end. Under
        // B1.1 this line reached for the concrete MediatRDomainEventDispatcher and carried a
        // warning: resolving IDomainEventDispatcher instead would have routed the message back
        // into the outbox and re-enqueued it forever. That hazard is gone — an integration event
        // cannot enter the domain pipeline at all — and what remains is simply the right
        // dispatcher for the kind of message being delivered.
        var dispatcher = scope.ServiceProvider.GetRequiredService<MediatRIntegrationEventDispatcher>();

        // Any exception a handler throws propagates. That is the contract with the dispatcher:
        // a transport that returns quietly has asserted delivery, and the message will be
        // marked delivered on the strength of it.
        await dispatcher.DispatchAsync([integrationEvent], cancellationToken);

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
