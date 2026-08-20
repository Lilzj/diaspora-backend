using Diaspora.SharedKernel.Primitives;

namespace Diaspora.UnitTests.Platform.Outbox;

/// <summary>
/// A plain domain event: internal to a module, in-process, and not a published contract.
/// <para>
/// Exists so the routing tests can prove the negative — that a domain event does <em>not</em>
/// become durable. Making domain events durable would be a different phase and a much larger
/// behavioural change, including for handlers that deliberately read uncommitted state from the
/// same unit of work that raised them.
/// </para>
/// </summary>
public sealed record RoutingProbeDomainEvent : DomainEvent;
