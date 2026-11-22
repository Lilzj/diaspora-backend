# ADR-018 — Reference-data seeders are discovered by interface, never listed

**Status:** Accepted · **Date:** 2026-08-04 · **Phase:** PSR-1

---

## Context

Ten modules ship a reference-data catalogue — categories, amenities, notification templates, ranking
strategies — and none of them can serve a request without it.

Every seeder was found in two ways, and both were fragile:

- **The integration harness named them one at a time**, twelve hand-written blocks each behind a
  `using` on a module's internal hosting namespace. Forgetting one did not fail a test; it failed
  *every* test in the suite at host start, with an error naming neither the module nor the seeder.
  **B8 recorded it. B8.1 paid it again.** B9, B10, B11 and B8.2 each restated the registration
  contract from memory in their evolution reports, because nothing checked it.
- **The startup validator found them by name** — a `BackgroundService` called `…Seeder`. Location's
  `LocationCatalogSeedRunner`, the first reference-data seeder the platform ever shipped, is neither.
  **It had never once been checked by the rule written to protect it.**

---

## Decision

**`IReferenceDataSeeder`, discovered by assembly scan.**

- `Module` names the owning module. **The runner orders seeders by that module's registered order**,
  then by the seeder's own `Order`, then by name.
- `AddReferenceDataSeeder<T>()` registers the concrete type once and forwards both `IHostedService`
  and `IReferenceDataSeeder` to **one instance**.
- `ReferenceDataSeedRunner` runs them in order, isolating failures, and is what the integration
  harness and a separately-migrated deployment both call.
- Five startup validations: unregistered (`SV5001`, error), no public entry point (`SV5002`),
  duplicate name (`SV5003`, error), unknown module (`SV5004`), and not discoverable (`SV5005`).

---

## Consequences

### Good

- **A new module's catalogue reaches the integration suite with no edit to the harness at all.** The
  twelve blocks became one call.
- Ordering is derived from the module registry, so a seeder never repeats a number that already
  exists somewhere else.
- The validator now finds a seeder because it *is* one, which closed a hole it had since B4.

### Costs, accepted

- **Twelve seeders and ten module registrations changed** — one interface declaration and one property
  each, no behaviour. That is the price of a marker interface arriving after the shape it marks, which
  is exactly the objection the pre-PSR-1 validator's comment recorded.
- `Order` is a tie-break *within* a module, and a three-figure value fails a test — because a number
  that size is almost always a module order copied onto a seeder, which is one fact in two places.
- A seeder naming a module the registry does not know still seeds; it sorts **last**, and `SV5004`
  says so. Dropping the catalogue would turn a typo into a module that cannot serve a request.

### Rejected alternatives

**Keep finding seeders by name.** Rejected, with evidence: it had missed the platform's first seeder
for eleven phases.

**Auto-register every discovered seeder into DI.** Rejected: the module's own registration line is
where lifetime and any configuration gate belong, and auto-registration would double-register
anything already declared. **The manual step that actually failed was never the module's own
declaration — it was the twelve copies of it that lived somewhere else.**

**Register the hosted service and the seeder separately.** Rejected: two registrations build two
instances, so the catalogue is seeded twice and the count in the log belongs to neither.

**Restate the module's order on each seeder.** Rejected: one fact in two places, free to drift — the
failure the architecture suite already records for hard-coded module lists.

---

## Related

- ADR-011 — the contributor model, whose discovery-not-listing reasoning this extends
- [Seeder Discovery Guide](../platform/seeder-discovery-guide.md)
- [Engineering Handbook §10 and §11](../platform/engineering-handbook.md)
