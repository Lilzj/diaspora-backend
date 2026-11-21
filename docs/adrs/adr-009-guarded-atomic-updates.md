# ADR-009 — Contended counters are arbitrated by the database, behind a platform port

**Status:** Accepted · **Date:** 2026-08-01 · **Phase:** B1.3

---

## Context

Nothing stopped two writers overselling. An aggregate decides from what it loaded and cannot see a
concurrent commit, so two members reserving the last hostel bed both succeeded.

**Marketplace needed this for stock and Housing for rooms**, and each was about to invent a different
half of the same mechanism. B10 recommended the primitive; B11 was instructed to keep its reservation
workflow behind an abstraction and carry the dependency forward rather than invent one — which it
did, correctly, making it the second vertical to route around a missing platform primitive.

---

## Decision

**`IAtomicReservationStore` over `IAtomicUpdateExecutor`, both in `Diaspora.Persistence`.**

Two gates, and each catches what the other cannot:

- **The aggregate** decides from what it loaded — its rules, its error codes, no round trip.
- **The database** decides whether the arithmetic is still true, with the row locked:
  `UPDATE … SET reserved = reserved + @n WHERE total - reserved - occupied >= @n`.

**Only taking can fail.** Reserve and consume-directly may refuse; release, consume and restore are
clamped in SQL and always apply.

---

## Consequences

### Good

- One implementation for beds, stock, seats and appointment slots. The persistence layer never learns
  which it is arbitrating — `AtomicCapacity` names only `Total`, `Held` and `Consumed`.
- Provider-neutral by construction, and asserted to name no provider.
- A release can never be refused, so a room is never held forever.

### Costs, accepted

- **The primitive owes the change tracker a correction, and this is the subtle part.** Issuing the
  guarded `UPDATE` and returning whether it applied is *worse than no atomic update at all*: the
  aggregate's owned counters are still marked modified from the in-memory decision, so the unit of
  work writes the stale value over the value the database just arbitrated — undoing the guarantee
  microseconds after establishing it. The correction belongs to the primitive; a caller who has to
  remember it will forget.
- The reservation guard **retries rather than waits**. Correct for a hostel bed, wrong for a stadium
  ticket, which is exactly why the behaviour sits behind a port a deployment can replace.
- A set-based write bypasses the change tracker, so `ExecuteDeleteAsync` and friends are forbidden to
  modules. B14's retention sweeps were rewritten to go through the tracker in bounded batches; the
  cost is real and it is the right trade against thirteen modules each with their own idea of a bulk
  delete.

### Rejected alternatives

**Pessimistic locking on the aggregate.** Rejected: it serialises every reader of a popular listing.

**Optimistic concurrency alone.** Rejected: it turns contention into a retry storm and cannot express
"there is capacity" as a condition.

**A module-local mechanism per vertical.** Rejected: stock in B10, rooms in B11, seats next. Each
module inventing a different half means the halves do not agree.

**`UPDATE … RETURNING`.** Rejected: PostgreSQL and SQLite have it; SQL Server spells it `OUTPUT`.
Provider-specific, and the platform's compatibility claim is provider-neutrality by construction.

---

## Related

- ADR-017 — cursor boundaries, the other B1.3 persistence primitive
- [Guarded Update Design](../platform/b1.3-guarded-update-design.md)
