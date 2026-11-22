# ADR-014 — A published event carries enough context for a consumer to act alone

**Status:** Accepted · **Date:** 2026-08-03 · **Phase:** B13.1

---

## Context

B13 could route on a published fact but could not always address the resulting message, because the
contract named an aggregate rather than a person. Four of its consumers carried comments naming the
enrichment they needed.

Worse, three Jobs contracts declared a **required, non-nullable** candidate member and the relay
wrote `Guid.Empty` into all three — because those contracts derive from domain events raised by
aggregates that hold a candidate *profile*, not a person.

**Nothing failed.** Events published, consumers parsed, rules matched, and every one of those
messages was addressed to an identifier no member holds. The highest-stakes message the platform
sends — an interview being scheduled — went to nobody, for a whole phase, with no error anywhere.

---

## Decision

**A published contract carries what a consumer needs to act without calling back into the producing
module.**

Enrichment is **additive, nullable, immutable and appended** — never inserted, because System.Text.Json
serialises in declaration order and an insertion changes the wire order of everything after it.

**When a relay cannot answer a required field there are exactly three routes:** the aggregate stamps
it at raise time, the handler passes it, or the relay reads it back. **A placeholder is not a fourth
option; it is the defect.**

---

## Consequences

### Good

- 39 properties across 35 contracts in four modules, **zero new project references, zero contract
  version bumps, zero migrations** — and 3,416 pre-existing tests passing afterwards, none modified.
- Eight architecture rules make the shortcut a failing build.
- The rule generalises. B8.2 nearly repeated the placeholder defect with a required `Summary` and
  used the first route instead, which was free.

### Costs, accepted

- **A contract grows over time and its properties are permanent.** Two names now mean the applying
  member (`ApplicantUserId`, `CandidateUserId`) and `GuideCommentAdded` carries `AuthorId` beside
  `AuthorUserId`. Both are documented loudly; renaming either needs a `V2`.
- **Ownership must be decided per fact.** The rules key on the publishing module's namespace, not on
  the property's name — three modules legitimately use `OwnerUserId` for their own concepts.
- Some derived read models exist only to answer a contract: `EmployerProfile.NotifiableOwnerUserId`
  is the current longest-standing owner rather than the member recorded at registration, because after
  a transfer the latter names somebody who may no longer be one.

### Rejected alternatives

**Let the consumer query the producing module.** Rejected: it is the one thing the module graph
forbids, and it would have been the first crack in B14's central claim that it depends on no business
module.

**Version the contract.** Rejected: an additive nullable property is backward compatible by
construction, and historical events still deserialize. A `V2` is reserved for a rename.

**Put the identity in a shared lookup service.** Rejected: it makes every consumer's correctness
depend on a second call that can fail, for a value the publisher already held.

---

## Related

- ADR-008 — messaging separation
- ADR-013 — communication ownership, which generates most of the pressure for enrichment
- [Event Ownership Guide](../platform/b13.1-event-ownership-guide.md)
