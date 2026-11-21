# ADR-021 — A recurring event's occurrence is an event, not a second kind of thing

**Status:** Accepted · **Date:** 2026-08-09 · **Phase:** B12.1 — Events Extension

---

## Context

B12 modelled an event as a single occasion: one start, one end, one guest list. A weekly meetup was
therefore fifty-two unrelated rows, cancelling one had no defined effect on the others, and nothing
on the platform could say "this is the same meeting, next week". The frontend's Phase 12 register
recorded the consequence precisely: *"two occurrences of the same weekly meetup are unrelated rows"*.

Closing that needs two things — a rule that generates dates, and dates that are addressable. The
first is uncontroversial. The second is the decision, because **an occurrence has to be something a
member can register for, be checked in at, and be reminded about tomorrow**, and there are two ways
to give it that identity:

1. A new `EventOccurrence` aggregate, referencing a series, with its own registrations.
2. An ordinary `CommunityEvent` that happens to carry a reference to the series that generated it.

The client's own type comments assumed the first — its `EventOccurrence` is a separate interface
from its `Event`, with a separate `EventRegistration` beside the existing RSVP.

---

## Decision

**A series generates `CommunityEvent`s. There is no second event type, and `occurrenceId` *is*
`eventId`.**

An occurrence carries four nullable columns and nothing else: `SeriesId`, `SeriesSequence`,
`OriginalStartsAt` and `IsSeriesException`. Everything else about it — its guest list, its capacity,
its check-in desk, its analytics row, its slug, its moderation state — is the machinery B12 already
had, unchanged.

The occurrence *status* a client renders (`Scheduled | Rescheduled | Cancelled | Completed`) is
**derived** from `EventStatus` plus "was it moved off its generated slot", never stored.

---

## Consequences

### Good

- **One registration model, one attendance model, one capacity settlement path.** The alternative
  would have duplicated all three, and B12's counters are only reconcilable because there is exactly
  one place a place can move.
- **Everything already built keeps working.** `/events/{id}`, the RSVP route, the attendee list, the
  check-in endpoint, the analytics read and the published contracts all serve a date of a series with
  no new code and no second contract for a consumer to learn.
- **One derived status cannot disagree with another stored one.** A second lifecycle field would have
  drifted from the first the hour the completion sweep moved one and not the other — and nothing
  would have failed.
- The same reasoning the module already applied to "open for RSVP", which is a predicate rather than
  a state for exactly this reason.

### Costs, accepted

- **Occurrences must be materialised, not expanded on read.** A rule expanded at read time produces
  identifiers that live for the length of one response, which is precisely what an RSVP cannot refer
  to tomorrow. So dates are written ahead to a rolling horizon and a sweep moves the horizon
  forward — which means a never-ending series genuinely has more dates than it has rows.
  `MaterialisedThrough` is on the wire so a client cannot state otherwise.
- **A series with five hundred dates is five hundred rows.** Bounded deliberately:
  `MaxSeriesOccurrences` is 500, and the per-organizer series bound (25) is far tighter than the
  event bound (200), because one series request is potentially five hundred writes.
- **The client's `EventOccurrence` and `EventRegistration` types are wider than the server's
  answer.** The server returns an occurrence projection whose identifier resolves as an event, and
  the registration surface the client proposed is served by the existing RSVP routes. That is a
  documented contract narrowing, not an omission.

---

## Alternatives rejected

**A separate `EventOccurrence` aggregate.** Rejected because it duplicates four mechanisms that
already exist and already agree with each other. The strongest argument for it — that a series with
three years of weekly meetings should not load them — is answered without it: occurrences are their
own aggregate roots keyed by series, exactly as RSVPs are keyed by event, and a series never loads
its dates.

**A stored `OccurrenceStatus` beside `EventStatus`.** Rejected because two lifecycle fields on one
row is two things to keep true. Derivation is free and cannot drift.

**Expanding the rule on read.** Rejected outright: the frontend register named this as the reason it
could not implement recurrence itself, and it was right.

**Renumbering sequences when a date is cancelled.** Rejected. An excluded date consumes its ordinal
and the rest keep theirs, which is RFC 5545's own reading of `COUNT` against `EXDATE` and — more
usefully here — what keeps every stored reference to a later occurrence pointing at the same thing.

---

## Related

- [ADR-009 — Guarded atomic updates](adr-009-guarded-atomic-updates.md) — why the capacity
  settlement path this decision preserves is worth preserving
- [ADR-014 — A published event carries enough context to act alone](adr-014-event-contract-enrichment.md)
  — why `SeriesId` was appended to three contracts rather than left for consumers to resolve
- [ADR-017 — Cursor paging excludes by identity](adr-017-cursor-paging-excludes-by-identity.md) —
  the problem the occurrence page sidesteps by paging on a sequence number
- [B12.1 Events Extension report](../community-life/b12.1-events-extension-report.md)
- [B12 architecture guide](../community-life/architecture.md)
