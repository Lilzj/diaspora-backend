using Diaspora.Contracts.Events;
using Diaspora.Infrastructure.Services;
using Diaspora.Platform.Messaging;

namespace Diaspora.Infrastructure.Identity.Events;

/// <summary>
/// The pre-B1.1 publisher: delivers integration events in-process, immediately, best-effort.
/// <para>
/// <b>Kept unregistered, as the documented rollback path.</b> It publishes inside the caller's
/// transaction and loses the event if anything throws — which is the defect B1.1 exists to
/// remove, and the reason nothing wires it up. It stays in the tree so a deployment that has to
/// turn the outbox off has a named, reviewed implementation to fall back to rather than an
/// improvised one.
/// </para>
/// <para>
/// B1.2 retyped it: it reaches consumers through
/// <see cref="MediatRIntegrationEventDispatcher"/> rather than the domain event dispatcher. That
/// was not a choice — the domain dispatcher no longer accepts an integration event, and this
/// class was one of the call sites the compiler flagged.
/// </para>
/// </summary>
public sealed class MediatRIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly MediatRIntegrationEventDispatcher _dispatcher;

    public MediatRIntegrationEventPublisher(MediatRIntegrationEventDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return _dispatcher.DispatchAsync([integrationEvent], cancellationToken);
    }
}
