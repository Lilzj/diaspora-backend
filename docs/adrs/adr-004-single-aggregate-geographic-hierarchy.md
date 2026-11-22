# ADR-004: One aggregate for the geographic hierarchy

**Status:** Accepted · **Date:** 2026-07-27 · **Phase:** B4

## Context

The B4 brief names State/Province, Region, District, City and Neighborhood as domain
concepts, alongside `AdministrativeArea`, and anticipates a Street level "in future". A
literal reading models each as its own aggregate with its own table.

Real administrative geography does not cooperate with that reading:

- The rungs are not uniform. England has counties where France has departments and Mauritius
  has districts with no intervening state at all. A hierarchy requiring every level to be
  populated is wrong in most countries.
- The depth is not fixed. The brief itself anticipates an eighth level that does not exist yet.
- The operations are level-agnostic. "Everything inside the United Kingdom", "the breadcrumb
  for this node", "the nearest place to this coordinate" — none of them care which rung the
  answer sits on.

## Decision

**One aggregate root, `AdministrativeArea`, carrying an ordered `AdministrativeLevel` and a
materialised `HierarchyPath`.** The named concepts survive as typed *views* — `Region`,
`StateProvince`, `District`, `City`, `Neighborhood`, `PostalArea` — sealed classes that wrap
an area at a given level, enforce that level at construction, and carry the behaviour specific
to it.

`Country` remains a separate aggregate. It is addressed by an external ISO code rather than by
an identifier the platform invented, it carries currency, calling code, locale and timezone
that no other rung has, and it gates whether the platform operates somewhere at all.

## Consequences

**What this buys.**

- "Everything inside X" is a prefix match on one indexed column, at any depth, rather than a
  five-way union.
- Adding Street is a row, not a migration.
- Skipped rungs are free: a city hanging directly off its country is as valid as one under a
  region.
- One repository, one set of query shapes, one place where the invariant lives.

**What it costs.**

- Moving a subtree means rewriting the paths beneath it. Administrative boundaries are redrawn
  on a scale of decades, and the rewrite is a bounded batch operation
  (`IAdministrativeAreaRepository.RewriteDescendantPathsAsync`). The alternative — a recursive
  CTE on every discovery query — costs on the common path instead of the rare one.
- The path column duplicates information the parent link already carries. That duplication is
  the point; it is what removes the recursion.
- One table holds nodes of six kinds, so a level filter appears in most queries. It is indexed.

**Rejected alternatives.**

| Alternative | Why not |
|---|---|
| Six aggregates, six tables | Five-way unions for containment; a schema migration to add Street; every consumer has to know which of six types it holds |
| One aggregate, adjacency list only (parent id, no path) | Containment becomes a recursive query on the hottest path in the module |
| One aggregate, nested sets | Faster reads than a path, but every insert renumbers a large slice of the table — and geography is imported in bulk |
| Closure table | Correct and general, but one row per ancestor-descendant pair for a gazetteer is an order of magnitude more storage for a depth that never exceeds eight |

## Verification

`GeographicHierarchyTests` asserts the level invariant, the skipped-rung cases for Mauritius
and for a city under its country, containment across a shared prefix, and the relocation
rewrite. `LocationArchitectureTests` asserts the typed views refuse a node at the wrong level.
