namespace Diaspora.SharedKernel.Primitives;

/// <summary>
/// Something meaningful that happened inside the domain. An immutable fact, raised by an
/// aggregate and handled inside the transaction that raised it.
/// <para>
/// <b>A domain event is private to the module that raised it.</b> It carries the module's own
/// types — strongly typed identifiers, domain enums, value objects — and it is free to change
/// shape in any release, because nothing outside the module is allowed to depend on it. That
/// freedom is the whole point of the type, and it is only real if a domain event cannot leave
/// the process.
/// </para>
/// <para>
/// <b>It is not an integration event and cannot become one.</b> Since B1.2 the two hierarchies
/// meet only at <see cref="IMessage"/>, which carries no dispatch semantics. A domain event
/// therefore cannot be handed to <c>IIntegrationEventPublisher</c>, cannot be written to the
/// outbox, and cannot be serialized as a published contract — the compiler refuses each of
/// them. Announcing a fact outside the module means translating it into an
/// <c>IIntegrationEvent</c> in <c>Diaspora.Contracts</c>, which is a deliberate act with a
/// frozen payload rather than an accident of inheritance.
/// </para>
/// </summary>
public interface IDomainEvent : IMessage
{
}
