using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Contracts.Events;

/// <summary>
/// A fact one module publishes for every other module to consume. A frozen contract, not an
/// internal one.
/// <para>
/// <b>An integration event is not a domain event, and since B1.2 the compiler agrees.</b> The
/// two interfaces meet only at <see cref="IMessage"/>, which carries the two properties they
/// genuinely share — an identity and a moment — and no dispatch semantics whatsoever. The
/// consequences are the point of the phase:
/// </para>
/// <list type="bullet">
/// <item>
/// <c>IDomainEventDispatcher.DispatchAsync</c> takes <c>IEnumerable&lt;IDomainEvent&gt;</c>, so
/// handing it an integration event is a compile error rather than a silent bypass of the
/// outbox.
/// </item>
/// <item>
/// <c>DomainEventHandler&lt;T&gt;</c> constrains <c>T</c> to <c>IDomainEvent</c>, so an
/// integration event cannot be consumed as though it were internal chatter.
/// </item>
/// <item>
/// The only route out of a module is <c>IIntegrationEventPublisher</c>, which accepts nothing
/// else — and its only implementation writes an outbox row inside the producer's transaction.
/// </item>
/// </list>
/// <para>
/// <b>What that costs the author of an event.</b> Publishing outside the module became a
/// deliberate act: translate the domain event into a flat, serialisable contract in this
/// assembly and hand it to the publisher. That translation was always the intent — B3 through
/// B7 all document it — but before B1.2 it was a convention, and conventions are followed until
/// they are inconvenient.
/// </para>
/// <para>
/// <b>Versioning.</b> The <c>V1</c> namespace segment is the version, and the contract's durable
/// name is derived from the type's full name. Renaming a type or moving its namespace changes
/// what is written into the outbox, so both are breaking changes unless pinned with
/// <c>[IntegrationEventContract]</c>.
/// </para>
/// </summary>
public interface IIntegrationEvent : IMessage
{
}
