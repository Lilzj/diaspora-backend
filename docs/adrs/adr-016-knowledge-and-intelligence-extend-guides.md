# ADR-016 — Country knowledge and country intelligence extend the Guides module

**Status:** Accepted · **Date:** 2026-08-04 · **Phase:** B8.1, B8.2

---

## Context

Two substantial capabilities arrived after B8 shipped guide content:

- **B8.1 — the Country Knowledge Directory**: 17 tables, 45 routes, 7 aggregates, curating what a
  member needs to read about a country and in what order.
- **B8.2 — Country Intelligence**: official notices, verified issuing authorities, embassy
  directories, emergency information, visa policy and public holidays.

Each was large enough to be a module. Each overlapped with guide content in a way that would have
needed an owner.

---

## Decision

**A phase that extends an existing bounded context registers from inside that module and declares its
own contributors.**

One `IPlatformModule` at order 700, one schema, three layers:

```
Guides module (order 700, schema `guides`)
├── B8    Content     /api/v1/guides         guides, versions, categories, votes
├── B8.1  Curation    /api/v1/knowledge      directories, navigation, learning paths
└── B8.2  Authority   /api/v1/intelligence   notices, authorities, embassies, visas, holidays
```

Three permission contributors, three endpoint contributors, three telemetry sources, three route
prefixes, one database contributor, three reference-data seeders — all on one module.

---

## Consequences

### Good

- **The kernel did not change. Neither did `Program.cs`.** Twice.
- One owner of "what do I need to know about this country", with three lifecycles under it and an
  honest answer to *who said this and when did it stop being true*.
- B2.6's contributor model is proven to compose more than once per module, and the second extension
  cost no more than the first.

### Costs, accepted

- One module is large. That is the trade against two modules that both know what a guide is.
- **The route-prefix rule had to be generalised.** It read the constant named exactly `RoutePrefix`,
  so Guides' second URL space — B8.1's `/api/v1/knowledge` — had never been checked for uniqueness or
  versioning. The rule now reads every constant ending in `RoutePrefix`.
- **One module gets one subscription per contract.** B8.2 wanted its own Trust handlers and could not
  have them; B8's existing handlers now call both projectors, each correlating on its own stored case
  identifier. **Add a projector, not a subscription.**
- Three seeders in one module made ordering a real question for the first time, answered by
  `IReferenceDataSeeder.Order` (ADR-018).

### Rejected alternatives

**A new module for each.** Rejected: each would have needed an order slot, a schema, a database
contributor and its own answer to who owns the overlap with guide content. **Two modules that both
know what a guide is are two modules that will eventually disagree.**

**Fold the capability into B8's existing aggregates.** Rejected: a published official notice and a
member-authored guide have different lifecycles, different verification, and different answers to
whether content freezes at publication.

---

## A rule this generates

**Reference data that decides platform behaviour goes on the row, not in a list inside the code that
reads it.** B8.2's notice categories carry a default severity and an alert family, so a deployment
adding "Cyclone Warnings" with the emergency family gets an emergency contract, a quiet-hours
override in B13 and a red chip in the client — with no release of this module and no code anywhere
naming the key. **A hard-coded list means the deployment finds out during a cyclone.**

---

## Related

- ADR-011 — the contributor model
- ADR-018 — seeder ordering, which Guides is the platform's only real case for
