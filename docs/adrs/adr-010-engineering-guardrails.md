# ADR-010 — Platform rules are enforced at four layers, and each has a permanent catalogue

**Status:** Accepted · **Date:** 2026-08-02 · **Phase:** B1.4

---

## Context

The platform knew its rules and enforced them by remembering. Twenty owned collections declared
`ValueGeneratedNever()`; eight did not, across four modules, and EF silently emitted `UPDATE` where
`INSERT` was meant. B9 recorded the `[AsParameters]` binding defect with a comment explaining it; B11
repeated it anyway. `[Transactional]` sat on a handler for five phases doing nothing, under a class
comment asserting the opposite.

Three separate recommendations had been made three or more times each without being built.

---

## Decision

**Four enforcement layers, each with a permanent id and a catalogue.**

| Layer | Mechanism | Catalogue |
|---|---|---|
| Compiler | type signatures, `[Obsolete]` | — |
| Analyzer | `DSPxxxx` — 21 diagnostics | [Analyzer Catalog](../platform/b1.4-analyzer-catalog.md) |
| Architecture tests | reflection, the EF model, Roslyn syntax | [Architecture Rule Catalog](../platform/b1.4-architecture-rule-catalog.md) |
| Startup validation | `SVxxxx` — refuses to boot on an error | [Startup Validation Guide](../platform/b1.4-startup-validation-guide.md) |

**Release builds promote every analyzer warning to an error.** A clean Debug build is not the check.

**A diagnostic id is permanent from the moment it is written down**, including for rules that are only
reserved — because an id in a suppression, a build log or an old commit message must still mean what
it meant.

---

## Consequences

### Good

- Three recommendations that had been made three times each stopped being made.
- The rules found real defects immediately: `DSP4004` found a live B4 defect; the owned-key rule found
  eight; B8.2 hit two more before a single row was written.
- A suppression is scoped to one file, carries its reason and its exit condition, and is governed by
  the [Suppression Guidelines](../platform/b1.4-suppression-guidelines.md).

### Costs, accepted

- **Seven of the first rules were wrong about the platform**, each having passed its own unit tests.
  Every one was narrowed after running against the real codebase, before shipping. That work is not
  optional and it is most of the cost of a guardrail phase.
- **An analyzer cannot see a name it cannot resolve.** B14's keys were exempt from three rules for a
  whole phase because they were built through a factory — recorded as F8 and closed in PSR-1.
- A rule that reports a shipped public contract is a build failure nobody may fix yet. Two such rules
  are suppressed, in exactly two reviewed places, each with an exit condition.

### Rejected alternatives

**Better documentation.** Rejected: proven not to survive contact with a new author, three times.

**Code review.** Rejected: every one of the defects above passed review and compiled.

**One layer.** Rejected: each layer sees something the others cannot. An analyzer sees a lambda's
shape; only the EF model knows whether a backing field was bound; only a booted container knows
whether a reflective registration exists.

---

## Related

- ADR-011 — the contributor model these rules mostly protect
- ADR-018 — seeder discovery, which added three `SVxxxx` codes
- [Engineering Handbook §0 and §23](../platform/engineering-handbook.md)
