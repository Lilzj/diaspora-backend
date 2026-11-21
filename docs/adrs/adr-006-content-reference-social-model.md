# ADR-006 — Social records address a content reference, not a post

**Status:** Accepted · **Date:** 2026-07-28 · **Phase:** B6

---

## Context

Nine capabilities — comments, reactions, shares, bookmarks, reports, moderation, mentions,
hashtags, visibility — are needed by every content-bearing module the platform will ship:
Community Updates, Guides, Community Life, Marketplace, Housing, Jobs, and whatever follows.

Built per module, that is nine features × N modules: N comment tables, N definitions of
"resolved", N report modals, and N chances for a private post to leak because one of them
forgot a visibility check.

The Expo client already shows the shape of the problem. `CommentContentType`,
`ReactionContentType`, `ModerationContentType` and `SavedContentType` are four overlapping
declarations of the same idea across four of its files.

---

## Decision

**Every social record stores a `ContentReference` — a content type and an identifier — instead of
a foreign key to a post.**

```csharp
public sealed class ContentReference : ValueObject
{
    public CommunityContentType Type { get; }
    public Guid Id { get; }
    public string Key => $"{(int)Type}:{Id:N}";
}
```

A module makes its content social by registering one `ICommunityContentSource` answering four
questions: does this exist, who wrote it, what is it called, who may see it.

Content types are an **enum with a reserved `Other`**, not a free-form string. A type nobody has
claimed is **refused**, not accepted.

---

## Consequences

### Good

- A content module adopts nine capabilities with one four-method class.
- One implementation of threading, tombstoning, the reason taxonomy, the auto-hide threshold and
  audience evaluation — so a fix reaches every module at once.
- Comments are their own aggregate, so a ten-thousand-comment thread is never loaded to add one.
- Cross-content features become possible: a saved screen spanning guides, jobs, events and posts
  is one query and one page shape.

### Costs, accepted

- **No database-level referential integrity.** A comment can point at content that no longer
  exists. Mitigated by `Exists` on the descriptor and by treating a missing descriptor as "gone".
  A foreign key would buy integrity at the price of the whole design.
- **An index cannot span an owned type's columns and the aggregate's own** (the constraint B5
  recorded). Reactions, bookmarks, reports and follows each carry a denormalised composite key so
  their uniqueness rules are real indexes rather than handler checks that lose races.
- **Adding a vertical adds an enum member.** Additive, and the only line B6 expects a later phase
  to touch. Deliberately preferred to a string, which lets any module invent a value that then
  appears in indexes, moderation queues and audit records with no way to enumerate what exists.
- **One module is on the critical path for every content module's social features.** Mitigated by
  the ten extension seams and by an architecture test asserting no vertical is named in the
  assembly — the day one is, the foundation has become a bottleneck.

### Rejected alternatives

**A comment table per module.** Rejected: it is the problem.

**A shared base class or generic `Comment<TContent>`.** Rejected: it produces a table per closed
type, which is the same thing with more ceremony, and it cannot express a saved list spanning
four types.

**A polymorphic string content type.** Rejected: unenumerable values in indexes, queues and audit
records, and no way to answer "which types exist" — the reasoning B5 records for its own owner
type enum.

**Events only, with each module keeping its own social tables.** Rejected: it moves the
duplication rather than removing it, and eventual consistency on a comment count is a worse
trade than a synchronous call inside one process.

---

## Related

- ADR-001 — modular monolith (why an in-process call is acceptable here)
- ADR-002 — Identity/Trust separation (why B6 consumes `ITrustService` and never scores)
- ADR-005 — media references, not URLs (the same shape of decision, one layer down)
