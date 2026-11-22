# ADR-017 — Cursor paging excludes by identity rather than comparing keys

**Status:** Accepted · **Date:** 2026-08-01 · **Phase:** B1.3, primitive lifted in B10

---

## Context

Every listing surface on the platform pages by a timestamp. A timestamp-only cursor **silently drops
rows tied on it**: page one returns twenty rows sharing a `CreatedAt`, page two asks for everything
strictly older, and the rest of the tie group is never served.

The ordinary fix — a keyset predicate `(CreatedAt, Id) < (@after, @afterId)` — **cannot be written on
this platform**, because `Id` is a strongly typed identifier. Ordering or stringifying one inside a
query is a runtime failure or a silent degradation, which is what `DSP3003` exists to forbid.

---

## Decision

**The cursor carries the boundary value and the identifiers already served at that boundary.** The
predicate is *strictly beyond the boundary, or at the boundary and not one of these* — so the tie
group is served across as many pages as it takes, and every row is served exactly once.

---

## Consequences

### Good

- No row is dropped and none is repeated, for any tie-group size the cap covers.
- It composes with the platform's typed ids without breaking `DSP3003`.
- One implementation in `Diaspora.Persistence`, used by every paged surface.

### Costs, accepted

- **The cursor grows with the tie group**, capped at 500 identifiers. Past that the boundary degrades
  and rows can reappear on a later page. A tie group that wide needs one unit of work writing 500 rows
  of one aggregate, which nothing on the platform does.
- **The documented degradation was wrong until somebody tested it**, and that is the lesson. The
  helper claimed it degraded to a strict comparison — *lossy but terminating*. The real behaviour was
  **neither**: truncating the identifier list left the rows past the cap unexcluded, so they returned
  on the next page, from an unchanging boundary, **forever**. Not lossy — non-terminating.
- A second documentation error fell out of the same read: an empty exclusion set makes the predicate
  `key <= @after` rather than `key < @after`, so it re-serves the tie group. That behaviour is
  defensible and is now documented as what it is.
- A sort whose leading key is not the cursor key can still interleave across the partition boundary.
  Housing's staff report queue does this deliberately and it is recorded.

### Rejected alternatives

**Offset paging.** Rejected: it is wrong under concurrent writes, and it degrades on every provider
at depth.

**Order by the identifier's `.Value`.** Rejected: `DSP3003` forbids it, for the reasons that rule
records.

**A surrogate integer sort key on every aggregate.** Rejected: it adds a column and an index to every
pageable table to work around a problem the cursor solves in the cursor.

---

## The rule this generates

**When a primitive documents a degradation, test the degradation.** Three phases used `CursorBoundary`
believing a sentence in its summary; the sentence was wrong in a way that made paging never
terminate, and only a test past the cap could have said so.

---

## Related

- ADR-009 — the other B1.3 persistence primitive
- [Cursor Guide](../platform/b1.3-cursor-guide.md)
