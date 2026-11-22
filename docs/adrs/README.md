# Architectural Decision Register

**Established:** PSR-1 — Platform Stabilization Release
**Status:** permanent. Every phase that takes a significant architectural decision adds one.

The catalogue of decisions that shaped the Diaspora backend. Each records the decision, the context
that forced it, the alternatives that were rejected **and why**, its consequences, and its status.

---

## The register

| # | Decision | Status | Phase | Date |
|---|---|---|---|---|
| [001](adr-001-modular-monolith.md) | Modular monolith architecture | Accepted | B1 | 2026-07-26 |
| [002](adr-002-identity-trust-separation.md) | Identity and Trust are separate bounded contexts | Accepted | B2 | 2026-07-27 |
| [003](adr-003-refresh-token-rotation.md) | Refresh tokens rotate, with replay detection | Accepted | B2 | 2026-07-27 |
| [004](adr-004-single-aggregate-geographic-hierarchy.md) | One aggregate for the geographic hierarchy | Accepted | B4 | 2026-07-27 |
| [005](adr-005-media-references-not-urls.md) | Media is a reference, never a URL | Accepted | B5 | 2026-07-28 |
| [006](adr-006-content-reference-social-model.md) | Social records address a content reference, not a post | Accepted | B6 | 2026-07-28 |
| [007](adr-007-transactional-outbox.md) | The outbox is filled by intercepting the domain event dispatcher | Accepted | B1.1 | 2026-07-29 |
| [008](adr-008-messaging-separation.md) | Domain and integration messages are separated by shape | Accepted | B1.2 | 2026-07-30 |
| [009](adr-009-guarded-atomic-updates.md) | Contended counters are arbitrated by the database, behind a port | Accepted | B1.3 | 2026-08-01 |
| [010](adr-010-engineering-guardrails.md) | Platform rules are enforced at four layers, each catalogued | Accepted | B1.4 | 2026-08-02 |
| [011](adr-011-contributor-extension-model.md) | A module declares what it owns; the kernel discovers it | Accepted | B2.6 | 2026-07-27 |
| [012](adr-012-access-tier-semantics.md) | Access tiers express progression; roles express privilege | Accepted | B2.0.1 | 2026-07-28 |
| [013](adr-013-communication-ownership.md) | Only the Communication Platform creates notifications | Accepted | B13 | 2026-08-03 |
| [014](adr-014-event-contract-enrichment.md) | A published event carries enough context to act alone | Accepted | B13.1 | 2026-08-03 |
| [015](adr-015-recommendation-explainability.md) | A recommendation carries its reason, enforced by the type system | Accepted | B14 | 2026-08-04 |
| [016](adr-016-knowledge-and-intelligence-extend-guides.md) | Country knowledge and intelligence extend the Guides module | Accepted | B8.1, B8.2 | 2026-08-04 |
| [017](adr-017-cursor-paging-excludes-by-identity.md) | Cursor paging excludes by identity rather than comparing keys | Accepted | B1.3 | 2026-08-01 |
| [018](adr-018-reference-data-seeders-are-discovered.md) | Reference-data seeders are discovered by interface, never listed | Accepted | PSR-1 | 2026-08-04 |
| [019](adr-019-ledger-first-financial-ownership.md) | The ledger is the truth, the wallet is a projection, and only B15 owns money | Accepted | B15 | 2026-08-05 |
| [020](adr-020-superseded-abstractions-are-retired-not-deleted.md) | A superseded abstraction is retired, never deleted | Accepted | PSR-1 Final Certification | 2026-08-06 |
| [021](adr-021-an-occurrence-is-an-event.md) | A recurring event's occurrence is an event, not a second kind of thing | Accepted | B12.1 | 2026-08-09 |

**Nothing is Superseded or Deprecated.** Twenty-one decisions, all live. Where a decision was revised —
ADR-012's onboarding rung has been amended twice, ADR-013's dormant port was obsoleted rather than
implemented — the revision is recorded inside the ADR, because the decision itself still stands.

---

## Reading them by theme

**How the platform is put together**
ADR-001 (modular monolith) → ADR-011 (contributor model) → ADR-016 (extending a module rather than
adding one) → ADR-018 (discovery over lists).

**How modules talk to each other**
ADR-008 (separation by shape) → ADR-007 (the outbox) → ADR-014 (what a contract must carry) →
ADR-013 (who is allowed to notify).

**How data is written and read**
ADR-005 (references, not URLs) → ADR-009 (guarded atomic updates) → ADR-017 (cursor boundaries) →
ADR-004 (one aggregate for the hierarchy).

**When to add a type and when not to**
ADR-016 (extend a module rather than add one) → ADR-021 (an occurrence is an event) → ADR-020
(retire rather than delete).

**Who is allowed to do what**
ADR-002 (Identity/Trust) → ADR-003 (token rotation) → ADR-012 (tiers vs roles).

**How the rules stay true**
ADR-010 (four enforcement layers), which most of the others depend on.

---

## Writing a new one

An ADR is warranted when a decision **constrains what a future phase may do**, or when the same
question would otherwise be re-asked by the next module. A choice with no consequence for anybody
else belongs in a code comment.

Use the shape ADR-006 established and the rest follow:

```markdown
# ADR-0NN — One-sentence decision, in the present tense

**Status:** Proposed | Accepted | Superseded by ADR-0NN · **Date:** · **Phase:**

## Context
What forced the decision. Include the failure, if there was one, with what it actually cost.

## Decision
What was decided. Present tense, as a rule somebody can follow.

## Consequences
### Good
### Costs, accepted        ← name them honestly; an ADR with no costs is a advertisement
### Rejected alternatives  ← the most valuable section. Say why, not just what.

## Related
```

**The rejected-alternatives section is the one future readers need most.** Six of the eighteen ADRs
here exist mainly because somebody would otherwise have taken the obvious option — a thin adapter
over B13's dispatcher, a comment table per module, a keyset predicate over a typed id — and each was
rejected for a reason that is not obvious until it is written down.

---

## See also

- [Engineering Handbook](../platform/engineering-handbook.md) — the lessons behind these decisions
- [Recommendation Register](../platform/psr-1-recommendation-register.md)
- [Technical Debt Ledger](../platform/psr-1-technical-debt-ledger.md)
- [Architecture Guide](../architecture.md)
