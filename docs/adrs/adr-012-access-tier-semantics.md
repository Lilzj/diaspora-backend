# ADR-012 — Access tiers express progression; roles express privilege

**Status:** Accepted · **Date:** 2026-07-28 · **Phase:** B2.0.1

---

## Context

B6 declared media capabilities at the onboarding tier, and Identity's specification asserted that the
onboarding rung carried no membership privileges. The two were in direct conflict, and the conflict
had shipped.

The root cause was not either module's code. It was that "what does `Onboarding` mean" had never been
written down as a rule anything could check.

---

## Decision

**An access tier is a rung on a progression: how far a member has got through establishing who they
are. It is never a statement about privilege.**

The admission test for a capability at the onboarding rung is:

> **Would withholding this make onboarding impossible, or create a loop with no entrance?**

Everything else — posting, commenting, reacting, following, messaging, publishing, reading the
community feed, browsing — is a **membership privilege** and opens above it.

`No_onboarding_capability_is_a_membership_privilege` tokenises a capability's name and checks each
segment against a membership vocabulary, with inflections.

---

## Consequences

### Good

- The rule fires **on the name, before anybody has to reason about intent**, which is what makes it
  useful.
- It was right and the new module was wrong, twice. B14's gate was `recommendations.feed`; the rule
  fired on "feed"; the gate became `recommendations.view`.
- The onboarding tier's contents are a specification decision recorded in one document, amended
  deliberately, rather than a side effect of whichever module declared a gate last.

### Costs, accepted

- **The vocabulary is blunt by design.** It cannot tell a personal recommendation feed from the
  community feed, and it must not learn to: widening it to admit one honest use of "feed" would
  disarm it for the privacy failure it was written for.
- Amending the onboarding rung means editing a specification *and* an allow-list in a test, together.
  That friction is the point.

### Rejected alternatives

**Widen the vocabulary, or add a per-name exception.** Rejected: a guard that fires on names is only
worth having while its vocabulary stays sharp. **When a name-based guard fires on a name you used
honestly, rename rather than widen.**

**Let each module decide its own minimum tier without a rule.** Rejected: that is the state that
produced the B6/Identity conflict.

**Model privilege as a higher tier.** Rejected: it conflates two independent axes. A fully verified
member can still be restricted, and a restricted member still keeps their privacy controls.

---

## A rule this generates, stated for B15

**The controls open at the same rung as the thing they control.** B14's personalization is on from
the moment an account exists, so reading the profile behind it, correcting it and switching it off
open at the same rung. **A privacy control that opens one tier later than the processing it governs
is not a control.**

If a module processes member data from registration, its opt-out belongs at registration too.

---

## Related

- ADR-002 — Identity/Trust separation
- [Access Tier Specification](../identity/access-tier-specification.md)
