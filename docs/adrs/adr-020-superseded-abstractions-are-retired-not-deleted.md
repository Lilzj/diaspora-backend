# ADR-020 — A superseded abstraction is retired, never deleted

**Status:** Accepted · **Date:** 2026-08-06 · **Phase:** PSR-1 Final Certification

---

## Context

A platform that runs for seventeen phases accumulates abstractions that were reasonable when they
were written and are now contradicted by a rule the platform made later. They compile. They are
public. They have zero consumers. And they are the first thing a new author finds.

**The platform has now paid for this four times, and each time the cost was measured.**

- **B1.4.1 — `INotificationPublisher`.** A kernel-level way for any vertical to create a notification
  directly, which is the single thing B13 exists to prevent. Never implemented, never called.
- **B2.5 — DDD-02.** A production-readiness audit listed fourteen dead SharedKernel types by name, in eleven entries, and
  stated the risk in one sentence: *"New module authors will copy whichever they find first."*
  **The finding was never closed and never reached the Technical Debt Ledger**, so it survived
  fifteen phases invisible to every count of what the platform owed.
- **PSR-1.2 — DEBT-072.** A purpose-built audit of unimplemented seams rediscovered one of B2.5's
  fourteen, four years later, and could not see the rest: `IRepository<T>` and `RepositoryBase<T>`
  implement each other, so an audit that counts implementations reads them as live.
- **PSR-1 Final Certification.** Fifteen types retired. **Nothing in twenty-six production projects
  referenced one**, which the Release build proved by passing with zero warnings.

The recurring shape is not that these types are used. It is that **nothing stops them being used**,
and nothing tells the next author they were considered and refused.

---

## Decision

**When an abstraction is superseded, keep the type and make reaching for it a compiler error.**

```csharp
[Obsolete(
    "<what it was, what superseded it, what to use instead, and where that is documented>",
    error: true,
    DiagnosticId = "DSPnnnn")]
```

Four requirements, all of them load-bearing:

1. **`error: true`.** A warning is a thing people turn off, and a Release build that promotes
   warnings to errors makes the distinction moot for this repository but not for a consumer of the
   assembly.
2. **A `DiagnosticId` reserved in the [Analyzer Catalog](../platform/b1.4-analyzer-catalog.md)**,
   under the same permanence rule analyzer ids obey. The id is not an analyzer rule and no analyzer
   emits it — the compiler does — but a suppression written against it must silence the thing it was
   written for, forever.
3. **The reason and the replacement in the message**, not in a document the reader has to find. The
   message is what appears in the IDE at the moment somebody types the name.
4. **An architecture rule asserting the attribute is still there.** Removing it is a visible edit in
   a phase that says so; the rule is what makes it visible.

**And record the closure in the [Technical Debt Ledger](../platform/psr-1-technical-debt-ledger.md),
not only in the phase's own report.** This is the half B2.5 missed, and it cost more than all the
retirements combined.

---

## Consequences

### Good

- **The compiler enforces it, for free.** No analyzer to write, test, ship and maintain. It fires in
  the IDE before a build, and it is immune to the resolution blind spots that make `DSP5007`
  necessary for real analyzer rules.
- **The refusal is discoverable at the point of temptation.** The next author meets the reason, not
  an empty namespace.
- **Binary compatibility is preserved.** Obsoletion is a compiler diagnostic, not a change to the
  assembly's surface, so anything already compiled against the type still binds.
- **The retirement is auditable.** Fifteen types, four ids, three architecture rules, one list.

### Costs, accepted

- **Dead code stays in the repository.** Fifteen types nobody may use, carried forever, appearing in
  file counts and IntelliSense. Accepted: the alternative has been tried by omission four times and
  the types came back each time as a fresh discovery.
- **A retired type's own tests must go.** `SpecificationTests.cs` was deleted with
  `Specification<T>`, because a passing test for a type nothing may use protects nothing. That is a
  real, if small, reduction in test count and it should be stated rather than absorbed.
- **Ids are consumed permanently.** `DSP1005`, `DSP3005`, `DSP3006` and `DSP4005` can never mean
  anything else.
- **Judgement is still required about what counts as superseded.** This ADR does not decide that; it
  decides what happens once somebody has.

### Rejected alternatives

**Delete the type.** The obvious option, and refused four times now. B1.4.1 stated it best and
nothing since has improved on it: *"an empty namespace is an invitation to declare the interface
again, and the next author would have no way to know it had been considered and refused."* B2.5's
DDD-02 is the empirical case — the types were not deleted, but they were not marked either, and the
result was that two separate later audits rediscovered fragments of the same finding.

**Leave it and document it.** This is what happened between B2.5 and this phase. Documentation that
nothing enforces is a description of a hope. Fourteen types, fifteen phases, one partial rediscovery.

**Write an analyzer rule instead.** Strictly worse on every axis: it must be written, tested and
maintained; it can only report what its own resolution can see; and it puts the reason in a
catalogue rather than in the message. The obsoletion is the cheaper and stronger tool, and the
platform should reach for an analyzer only when there is no type to hang an attribute on.

**Mark it `[Obsolete]` as a warning.** Rejected because the platform's own history says warnings are
negotiable. `TreatWarningsAsErrors` is set for Release here, so the difference is invisible inside
this repository and decisive outside it.

---

## Related

- [ADR-008 — Messaging separation](adr-008-messaging-separation.md) — what `IDomainEventHandler<T>`
  contradicted
- [ADR-017 — Cursor paging excludes by identity](adr-017-cursor-paging-excludes-by-identity.md) —
  what `IReadRepository<T>` and `Specification<T>` predate
- [ADR-010 — Engineering guardrails](adr-010-engineering-guardrails.md) — the analyzer pack and its
  permanence rule
- [Analyzer Catalog § Retirements](../platform/b1.4-analyzer-catalog.md) — the reserved ids
- [Final Engineering Certification Report](../platform/certification-report.md) — the fifteen types
- [Documentation Consistency Report](../platform/certification-documentation-consistency-report.md) —
  why DDD-02 survived
