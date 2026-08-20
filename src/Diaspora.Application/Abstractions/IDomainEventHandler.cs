using MediatR;

namespace Diaspora.Application.Abstractions;

/// <summary>
/// <b>Superseded by B1.2 and retired in PSR-1 Final Certification. Do not implement it.</b>
/// <para>
/// Declared in B1 as a marker over <c>INotificationHandler&lt;T&gt;</c>, before the platform had a
/// separated messaging model. B1.2 replaced markers with base classes for one reason: <b>the choice
/// of base class states which pipeline a handler belongs to, and the compiler holds it there.</b>
/// <c>DomainEventHandler&lt;T&gt;</c> is in-process, inside the transaction, and runs before the
/// outbox; <c>IntegrationEventConsumer&lt;T&gt;</c> is out-of-process, after commit, and may run on
/// another machine. Those are different reliability contracts, and the difference is the whole point
/// of ADR-008.
/// </para>
/// <para>
/// <b>Why this interface is worse than nothing.</b> A handler implementing it joins the domain
/// pipeline without saying so — it reads as a generic MediatR handler, gets none of the base class's
/// guarantees, and gives a reviewer no signal about which side of the commit boundary it runs on.
/// It is the exact shape B1.2 was created to make impossible, still declared, still discoverable by
/// IntelliSense, and with nothing pointing at the replacement.
/// </para>
/// <para>
/// <b>Why obsoleted rather than deleted</b>, following the precedent B1.4.1 set for
/// <c>INotificationPublisher</c> and PSR-1.2 recorded as DEBT-072: an empty namespace is an
/// invitation to declare the interface again, and the next author would have no way to know it had
/// been considered and refused. Obsoletion is a compiler diagnostic, not a change to the assembly's
/// surface, so anything already compiled against it still binds.
/// </para>
/// <para>
/// <b>What to use instead.</b> Derive from <c>DomainEventHandler&lt;T&gt;</c> for in-process work
/// inside the transaction, or <c>IntegrationEventConsumer&lt;T&gt;</c> for work after commit. See
/// the [Messaging Architecture Guide](../../../docs/platform/b1.2-messaging-architecture-guide.md).
/// </para>
/// </summary>
/// <seealso href="../../../docs/adrs/adr-008-messaging-separation.md"/>
[Obsolete(
    "IDomainEventHandler<T> is superseded by B1.2's handler base classes and must not be " +
    "implemented. Derive from DomainEventHandler<T> for in-process work inside the transaction, " +
    "or IntegrationEventConsumer<T> for work after commit — the base class states which pipeline " +
    "the handler belongs to and the compiler holds it there. See ADR-008 and " +
    "docs/platform/b1.2-messaging-architecture-guide.md.",
    error: true,
    DiagnosticId = "DSP1005")]
public interface IDomainEventHandler<in TEvent> : INotificationHandler<TEvent>
    where TEvent : INotification
{
}
