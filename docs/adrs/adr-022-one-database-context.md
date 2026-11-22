# ADR-022 — One database context for every module, with the boundary made explicit

**Status:** Accepted · **Date:** 2026-11-21 · **Finding:** BARCH-004 — one `DbContext` for 21 modules

---

## Context

`DiasporaDbContext` is the only `DbContext` on the platform. Twenty-one modules contribute their
entity configurations to it through `IDatabaseContributor`, and each owns a Postgres schema:

```
analytics  automation  bookmarks  community  community_life  community_programme
conversations  creator  financial  guides  housing  identity  integrations  jobs
location  marketplace  media  notifications  providers  recommendations  trust
```

BARCH-004 recorded three consequences of that shape, two deliberate and one not, and asked for the
decision to be written down rather than rediscovered.

---

## Decision

**One context, not one per module. The boundary is enforced rather than structural.**

Splitting the context would cost the cross-module transaction, and that transaction is load-bearing:
`[Transactional]` spanning the CommunityProgramme → Financial boundary is what makes a membership
settlement atomic. A context per module would replace it with a distributed-commit problem the
platform has no need to own while it deploys as one process.

The context is split again only when a module is genuinely extracted into its own deployment, at
which point the schema it already owns is the thing that moves.

---

## Consequences

### 1. A transaction spans modules — deliberate, and the reason for the decision

This is the modular monolith's central trade. It is a feature here, not an accident.

### 2. One model cache, one migration history, one connection pool — deliberate

**The model cache has bitten this platform twice and will again.** EF caches one model per
`DbContext` *CLR type*. A test host that builds `DiasporaDbContext` from its own contributor set
shares that cache with every other host, and whichever runs first decides what the others see.
Adding B12.1's two persistence test classes broke `SoftDeleteCascadeTests`, which had passed since
B1.0.1; neither class had a defect — they were looking at each other's model. **It only fails when
the whole suite runs**, so running one class in isolation reports that everything is fine.

The fix, wherever a test builds the context with a non-default contributor set, is
`ReplaceService<IModelCacheKeyFactory, …>` with a key distinct to that host. `PlatformModel`,
`CommunityLifeModelCacheKeyFactory` and `ModuleSchemaOwnershipTests` each do this.

### 3. Any module holding the context could read another module's schema — not deliberate

This is the consequence the finding asked to be closed, and it has two halves.

**The write half was already enforced.** `DiasporaDbContext.ApplyModuleConfigurations` compares
every entity a contributor adds against the schema that contributor declares, and throws at startup
if one landed elsewhere. Because the host is constructed during the build, a module mapping a table
into another module's schema fails the build rather than production. As of this ADR it also has a
test that watches it refuse — `ModuleSchemaOwnershipTests` — which it did not before.

**The read half is enforced by `DatabaseBoundaryArchitectureTests`.** Every `Set<T>` outside
`Diaspora.Persistence` must name an entity the calling project declares. Two premises in the
finding had expired by the time it was acted on, and one hole was sharper than it described:

- BARCH-001 closed on 21 November 2026 with an empty exemption list, so no module can name another
  module's types at all; the "107 internal imports" the finding relied on are gone.
- Raw SQL, the other way across a schema boundary, appears in four files and all four are in
  `Diaspora.Persistence`.
- **Seven identity aggregates — `User`, `UserSession`, `UserDevice`, `OtpChallenge`,
  `SecurityEvent`, `StepUpGrant`, `AccountActionProposal` — live in `Diaspora.Domain`, not in a
  module.** Every module may import the kernel, so a module writing `Set<UserSession>()` would read
  the identity schema without crossing any module boundary a rule was watching. That is the route
  the new rule exists for.

`Diaspora.Api` is the one exception, and only for the kernel's aggregates: the composition root
counts members for the platform overview, and identity is not a module.

### 4. Sixteen files hold the context outside a repository

The finding expected one exemption. There are sixteen, in five shapes: the host's two health checks
and two audience sources, three privacy providers answering an erasure request, four area filing
sources answering B4, two extensibility directories, two analytics sources, and one defaults
seeder. Each is legitimate; each is a place the boundary is kept by convention rather than by
structure, which is why they are recorded exactly rather than counted loosely.

---

## A note on where this file lives

When this ADR was written, `docs/` was gitignored and **none of the platform's 22 ADRs were tracked
by git** — invisible to CI, to a new machine, and to anybody who was not the author. Writing this
decision into an ADR would have produced a twenty-third invisible file, and a rule asserting it
existed would have passed by skipping.

**The register is now tracked** (`.gitignore`, 21 November 2026). The carve-out is narrow: `docs/`
is un-excluded, `docs/*` re-excludes everything under it by name, and only `docs/adrs/` is let
back in, so the audit reports, phase write-ups and release-readiness notes stay exactly as local as
they were. The nineteen documentation rules key on `docs/platform`, which is still absent from a
clean checkout, so their skip behaviour is unchanged.

The enforceable half of this decision is still not in this file, and that is the right division. It
lives in `DatabaseBoundaryArchitectureTests`, in `ModuleSchemaOwnershipTests`, and in
`DiasporaDbContext`'s own summary — all run on every build. **This document is the narrative; those
are the guarantee.** What tracking buys is that the narrative now travels with the code it
explains.
