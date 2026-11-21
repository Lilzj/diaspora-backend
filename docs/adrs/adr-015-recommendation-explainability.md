# ADR-015 — A recommendation carries its reason, enforced by the type system

**Status:** Accepted · **Date:** 2026-08-04 · **Phase:** B14

---

## Context

A ranked result a member cannot interrogate is a promise the platform immediately breaks. The
platform tells members it will explain what it believes about them and let them correct it; a feed
of unexplained suggestions is that promise withdrawn silently.

The failure mode is specific: an ML adapter added later returns a score and no reason, and the
explanation degrades to "recommended for you" everywhere at once — with no build failure, no test
failure, and no moment where anybody decided.

---

## Decision

**A ranking input carries a reason or it does not compile.**

Policy is separated from ranking, structurally: a **policy** decides what may be recommended
(blocked content, muted categories, a member's own listings) and a **strategy** decides how to order
what remains. An architecture test makes the separation real rather than conventional.

**Three AI seams are declared with nothing behind them**, and an architecture test fails if anybody
quietly fills one in B14's own assembly.

---

## Consequences

### Good

- If a future phase adds a ranking input, it carries a reason **whether or not the author thought
  about it**.
- Six system policies cannot be disabled; every policy change publishes an event and writes an audit
  entry — because a policy misconfiguration silently widening what is shown is the module's
  highest-severity risk.
- The candidate projection means B14 depends on **no business module**: everything it ranks arrived
  as a published contract.

### Costs, accepted

- **The seam enforces that a reason exists, not that it is good.** An ML adapter can still degrade
  explanations, and that is recorded as a risk rather than pretended away.
- The candidate projection can drift from source truth. Every consumer is an upsert and a full outbox
  replay rebuilds the table.
- **Cursor pagination is built and unused**, deliberately: a recommendation page is a ranked set of
  bounded size, not a scrollable list. Paging into position 21 means either re-ranking a pool that has
  moved underneath the member or persisting the whole ranked pool.
- The feed is composed sequentially, because eight sections share one `DbContext` and a concurrent
  fan-out over one would fail intermittently in a way that reproduces on nobody's machine.

### Rejected alternatives

**A convention that strategies should populate `Reason`.** Rejected: the platform's own history says
a rule enforced by remembering is enforced until the next author.

**Explain at render time from the strategy name.** Rejected: it produces a label, not a reason, and it
is wrong the moment two strategies contribute to one result.

**Depend on the kernel's `ICacheService`.** Rejected: it is declared and unimplemented, and depending
on it makes a component fail in silence. B14 declares `IRecommendationCache` and ships a working
in-memory implementation, **so the seam a deployment replaces with Redis works before it is
replaced.** That is the pattern for any capability the platform declares and does not implement.

---

## Related

- ADR-011 — the contributor model, which B14 uses in both directions at once
- ADR-014 — the contracts B14's projection is built from
- [AI Readiness Guide](../recommendations/ai-readiness.md) · [Explainability](../recommendations/explainability.md)
