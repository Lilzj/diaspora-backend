using Diaspora.Contracts.Events;

namespace Diaspora.Platform.Messaging;

/// <summary>
/// The one way an integration event leaves the module that raised it.
/// <para>
/// <b>Since B1.2 this is the only way, and the compiler is what says so.</b> The port has always
/// documented that intent — "domain events are a module talking to itself; integration events are
/// a published contract" — but documentation cannot close a route the type system leaves open,
/// and until B1.2 <c>IntegrationEvent</c> derived from <c>DomainEvent</c>, so
/// <c>IDomainEventDispatcher</c> accepted both. Two of six modules took that route with 29 of the
/// platform's 42 contracts. Severing the inheritance removed the alternative rather than
/// discouraging it.
/// </para>
/// <para>
/// <b>Publishing is enqueuing, not delivering.</b> The registered implementation writes a row into
/// the caller's own unit of work, so the aggregate and the promise to announce it commit together
/// or not at all. Nothing is sent while the caller's transaction is open, and the caller never
/// waits on a consumer. Delivery happens later, from the outbox, through
/// <c>IOutboxTransport</c>.
/// </para>
/// <para>
/// <b>It throws, and callers should let it.</b> Enqueuing fails only when the event cannot be
/// serialized or exceeds the payload ceiling — both defects in the event itself, and both
/// better as a rolled-back transaction than as an aggregate committed with its announcement
/// silently dropped.
/// </para>
/// <para>
/// <b>Why it lives in the platform kernel.</b> Before B1.2 it sat in
/// <c>Diaspora.Application.Identity.Abstractions</c>, because Identity published first. Every
/// module in the platform depends on it and none of them depend on Identity, so its home was an
/// accident of history that made the dependency graph read backwards. The type name is
/// unchanged; migrating is a <c>using</c> edit. See
/// <c>docs/platform/b1.2-migration-guide.md</c>.
/// </para>
/// </summary>
public interface IIntegrationEventPublisher
{
    /// <summary>
    /// Records the event for durable delivery inside the caller's current unit of work.
    /// </summary>
    Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}
