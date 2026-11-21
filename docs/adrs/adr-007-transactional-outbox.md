# ADR-007 — The outbox is filled by intercepting the domain event dispatcher

**Status:** Accepted · **Date:** 2026-07-29 · **Phase:** B1.1

---

## Context

Integration events dispatched in-process immediately after the aggregate was saved. A publish that
threw was logged and swallowed: the write stood, the consumer was never told, and nothing reported an
error. If the process died between commit and publish, the event was simply lost.

**Four separate modules recorded this as a gap before anybody built the fix** — B3, B5, B6 and B7 —
each describing it in almost the same words, each concluding it was not theirs to solve. Trust's
report put the priority precisely: it had risen from "no one to disappoint" to "one consumer,
degrading gracefully".

---

## Decision

**A transactional outbox, filled by intercepting `IDomainEventDispatcher` — not
`IIntegrationEventPublisher`.**

An event is written to the outbox table **inside the producer's own transaction**, so the aggregate
and the intent to publish commit or roll back together. A background relay drains the table, marks
after delivery rather than before, and retries with backoff.

Delivery is therefore **at-least-once**. Every event carries its own `EventId`, which consumers use
as an idempotency key.

---

## Consequences

### Good

- An event that was committed is delivered, or is still in the table being retried. It cannot be lost.
- No module changed. The interception sits beneath every raise site.
- The relay is a hosted service, disable-able, with a public entry point the integration suite drives
  deterministically rather than waiting on a timer.

### Costs, accepted

- **An unavoidable circular DI edge**, resolved with a lazy factory and documented rather than
  designed away: the dispatcher needs the outbox writer, which needs the context, which dispatches.
- **A publish failure still never fails the write.** The relay logs and continues, because throwing
  would roll back a member's action because an analytics consumer was unreachable.
- **The dispatch loop runs inside `SaveChanges`**, so an event raised while handling an event is
  picked up in the same unit of work. That is intended, and it is subtle.
- Consumers must be idempotent. They already were, on `EventId`.

### Rejected alternatives

**Intercept `IIntegrationEventPublisher`.** Rejected: the publisher is the port a module calls
*directly*, and most events are raised on an aggregate and dispatched by the unit of work. Wrapping
the publisher would have missed them — it is not the choke point.

**Two-phase commit or a distributed transaction.** Rejected: the platform is a modular monolith with
one database (ADR-001). The outbox is the same guarantee with none of the operational cost.

**Publish before commit.** Rejected: at-most-once in the other direction, and worse — a consumer can
act on a write that then rolls back.

**Mark delivered before sending.** Rejected: that is at-most-once, which is the defect being fixed.

---

## Related

- ADR-001 — modular monolith
- ADR-008 — messaging separation, which made the interception point unambiguous
- [Reliable Messaging Architecture](../platform/b1.1-reliable-messaging-architecture.md)
