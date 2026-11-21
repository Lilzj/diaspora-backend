# ADR-013 — Only the Communication Platform creates notifications

**Status:** Accepted · **Date:** 2026-08-03 · **Phase:** B13, reinforced B1.4.1

---

## Context

Every vertical has something worth telling a member. Left to themselves, thirteen modules would each
build a notification: thirteen templates, thirteen ideas of what "urgent" means, thirteen places a
member's decision to turn a category off could be ignored.

The kernel had declared `INotificationPublisher` since B1 — unimplemented, uncalled, and reading as
though it were the supported way to do exactly that.

---

## Decision

**Business modules publish integration events. Only the Communication Platform creates
notifications.**

A module that wants somebody notified calls `IIntegrationEventPublisher.PublishAsync`. B13 matches
the published fact against seeded routing rules and decides whether, how and to whom.

If B13 cannot build the notification from the payload, **the contract is enriched** (ADR-014). That
is the correct place for the pressure to land.

---

## Consequences

### Good

- Channel preferences, quiet hours, digest batching, localisation, deep links, delivery retries and
  the recipient's right to have turned a category off are decided in one place, by the only component
  that can see all of them.
- **B13 is the platform's first module with no module references at all.** It knows the contracts and
  nothing else.
- New member-facing messages are a seeded rule and a template — no contract, no code, no
  business-module deploy.

### Costs, accepted

- A vertical cannot express urgency directly. It publishes a fact; B13's seeded category decides.
  B8.2 made that workable by putting the default severity and alert family **on the catalogue row**,
  so a deployment adding "Cyclone Warnings" gets an emergency contract with no code naming the key.
- **22 of B13.1's 39 enrichments are published and unread.** Correct by design: a contract carries
  what a consumer *could* need; a rule decides what is sent.
- Every enrichment names one person, never an audience. Fan-out across a recruiting team or an
  organisation's committee needs a module that owns an opinion about those roles, and none does.

### Rejected alternatives

**Implement `INotificationPublisher` as a thin adapter over B13's dispatcher.** Rejected — *and this
is the important one.* It would work, and that is the objection: it would be a supported,
discoverable, kernel-level way for any module to create a notification directly, which is the single
thing this decision exists to prevent.

**Delete `INotificationPublisher`.** Also rejected: an empty namespace is an invitation to declare the
interface again, and the next author would have no way to know it had been considered and refused.

**What was done instead:** it is **obsoleted as a compile error** under `DSP2004`, which is the only
artefact that answers the shortcut at the moment somebody takes it. `error: true` rather than a
warning because a warning can be waved through with one `#pragma` and an error cannot.

Four guards now ask the same question: the analyzer (`DSP2003`, scoped to verticals), the obsoletion
(everybody, including the kernel and B13), an architecture rule, and an integration test asserting
nothing in a booted container resolves it — because a reflective registration would pass all three of
the others.

---

## Related

- ADR-014 — contract enrichment, the supported escalation path
- ADR-008 — messaging separation
- [Engineering Handbook §18](../platform/engineering-handbook.md)
