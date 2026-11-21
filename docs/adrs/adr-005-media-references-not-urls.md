# ADR-005: Business modules store media references, never URLs

**Status:** Accepted
**Date:** 2026-07-28
**Phase:** B5 — Media & Digital Asset Management Engine

---

## Context

The Expo client stores media as raw URLs. The Phase 0 analysis counted **31 media-bearing fields
across 17 types, using 14 different names for two concepts** — `avatarUrl`, `avatar`,
`profilePhoto`, `authorAvatar`, `senderAvatarUrl` and `recipientAvatarUrl` are one thing;
`coverImage`, `coverImageUrl`, `imageUrl`, `thumbnail`, `thumbnailUrl`, `logo` and
`landmarkImageUrl` are another.

Every one of those fields holds a `https://images.unsplash.com/...?w=600` string.

Twelve more modules are on the roadmap — Community, Providers, Guides, Community Updates, Jobs,
Housing, Marketplace, Organizations, Messaging, AI, Search, Notifications — and every one of them
will need to reference images. Without a rule, each will invent its own field name and store
whatever the storage layer happened to return that year.

Two existing modules already show the pattern the platform drifts toward when nobody decides:
Identity stores `AvatarStorageKey`, and Trust stores `VerificationDocument.StorageKey`. Both are
better than a URL and both are still a storage detail sitting in a business table.

## Decision

**A business module stores a media reference — an identifier, an owner and a role — and nothing
else.**

```csharp
public sealed class HousingListing : AggregateRoot<HousingListingId>
{
    public MediaId? CoverMediaId { get; private set; }
    public MediaCollectionId? GalleryId { get; private set; }
}
```

Specifically:

1. **No URL** in any business module's table, DTO or domain type.
2. **No storage key** either. A key is a storage decision; a reference is not.
3. **No bucket, container, provider name or CDN host.**
4. Every URL is minted by Media at the read boundary and points at Media's own routes.
5. `MediaReferenceResponse` carries `mediaId`, `ownerType`, `ownerId`, `role`, `position` — and
   is frozen with **no URL field and no key field**. That absence is the contract.

Naming is unified: `<role>MediaId` for a single asset, `<role>CollectionId` for an ordered set.
Roles are lower-case slugs from `MediaReferenceRoles` — `avatar`, `cover`, `logo`, `gallery`,
`attachment`, `document`, `selfie`, `banner`, `floor-plan`, `inline`.

## Consequences

### Good

- **Provider migration becomes invisible.** `StorageLocation` records which provider wrote each
  object, so a move from MinIO to S3 is a configuration flip plus a background copy, reversible
  at any point. Under the URL model it is a rewrite of every row in every module's tables — and
  no rewrite fixes the copies clients already cached.
- **Access control becomes possible.** A URL in a table is a permanent, unauthenticated grant. A
  reference resolves through a guard that can refuse, expire, and stop working the moment an
  asset is quarantined.
- **One vocabulary.** Fourteen names collapse to one rule, so Housing and Marketplace cannot
  invent three spellings of `cover` independently.
- **Deduplication and fraud detection become possible.** Media publishes a SHA-256 of the bytes;
  the client's URL-string reuse check is defeated by any re-upload.
- **Renditions become a server concern.** A consumer asks for a named size instead of appending
  `?w=600` and hoping the host honours it.
- **Orphans become detectable.** An asset nothing references is visible and collectable. Under
  the URL model there is no such concept.

### Costs

- **One extra hop.** Rendering a listing needs the listing and its media. Mitigated by
  `GetManyAsync`, which batches a whole gallery into one query, and by collections, which return
  their assets inline.
- **A migration for existing data.** The client's URLs are mock data, so today the cost is a
  client change rather than a data migration. That will not be true in a year — which is a reason
  to decide now rather than later.
- **A new enum member per vertical.** `MediaOwnerType` gains a value when a module ships.
  Deliberate: a free-form string would let any module invent an owner type, and the value would
  appear in indexes, audit records and access rules with no way to enumerate what exists. Every
  module named in the B5 brief already has its value.

## Alternatives considered

**Store the URL and rewrite on migration.** What the client does today. Rejected: it does not
survive a provider change, it makes access control impossible, and it puts a storage decision in
a dozen business tables.

**Store the storage key.** What Identity and Trust do. Better — no host, no scheme — but still a
storage detail in a business table, and it forces every consumer to know the key format and to
call something to make it readable. A reference is one identifier with a documented resolver.

**Store a signed URL with a long expiry.** Rejected outright. A long-lived signed URL is a
permanent grant with extra steps, and a database column full of live credentials to immigration
documents is the specific outcome B5 exists to prevent.

## Enforcement

- `MediaReferenceResponse` has no URL or key field. Asserted by
  `MediaFrontendContractTests.A_media_reference_carries_no_url`.
- Media references no vertical, so no vertical can smuggle a storage type back through it.
  Asserted by seven architecture theory cases.
- No storage SDK appears anywhere in the module. Five more.
- Recorded as intentional deviation **D1** in the
  [Frontend Contract Report](../media/frontend-contract-report.md).

## Related

- [ADR-001: Modular monolith](adr-001-modular-monolith.md)
- [ADR-002: Identity/Trust separation](adr-002-identity-trust-separation.md)
- [Media Architecture Overview](../media/architecture.md)
- [Contract Freeze](../media/contract-freeze.md)
