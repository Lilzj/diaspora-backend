# ADR-008 — Domain and integration messages are separated by shape, not by convention

**Status:** Accepted · **Date:** 2026-07-30 · **Phase:** B1.2

---

## Context

`IntegrationEvent` derived from `DomainEvent`. `DomainEvent` is not a MediatR `INotification`, so
publishing an integration event through `IPublisher` threw *"notification does not implement
INotification"* on every write path in B3 — code that compiled cleanly and that no unit test caught.

The deeper problem was that one base type made the two kinds of message interchangeable at every call
site. Nothing prevented handing a domain event to the integration publisher, or the reverse.

---

## Decision

**Two disjoint hierarchies under one marker, `IMessage`, and no signature anywhere that accepts the
wrong one.**

A domain event is settled inside the transaction that raised it. An integration event is a published
contract, lives in `Diaspora.Contracts`, and goes out through `IIntegrationEventPublisher`.

**The distinction is the message's *shape*, not the channel it happens to travel on.** A contract in
`Diaspora.Contracts` is published because of what it is, not because of who sent it.

---

## Consequences

### Good

- **The mistake is not expressible.** B14 shipped 34 consumers and 6 published contracts and the
  question never came up.
- `DSP2001` fails a type that is both; `DSP2002` fails a publish that bypasses the port.
- The outbox (ADR-007) has one unambiguous place to intercept.

### Costs, accepted

- A one-time breaking change to every event declaration, done in a phase that said so.
- Some duplication between a domain event and the integration event derived from it. That is the
  point: one is internal and free to change, the other is a contract.
- System.Text.Json serialises in declaration order, so **a property must be appended, never
  inserted** — an inserted property changes the wire order of everything after it.

### Rejected alternatives

**A naming convention plus an architecture test.** Rejected: it reports the mistake after somebody
writes it. The platform had a prefix-based architecture rule that turned out to work only by accident.

**One base type with a flag.** Rejected: a flag is checked at runtime, by code somebody has to
remember to write.

**Keep the inheritance and fix the MediatR registration.** Rejected: it fixes the symptom and leaves
the two kinds interchangeable at every call site.

---

## Related

- ADR-007 — the outbox
- ADR-014 — what a published contract must carry
- [Messaging Architecture Guide](../platform/b1.2-messaging-architecture-guide.md)
