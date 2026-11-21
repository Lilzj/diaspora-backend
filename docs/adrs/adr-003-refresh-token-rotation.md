# ADR-003 — Rotating opaque refresh tokens with replay detection

**Status:** Accepted · **Phase:** B2

---

## Context

Access tokens are JWTs carrying a permissions snapshot, so ordinary requests authorise with
no database round trip. That snapshot is stale from the moment it is signed, which forces a
short lifetime — and a short lifetime forces a refresh mechanism.

A refresh token is a long-lived credential sitting on a mobile device. It is the most
valuable thing this system hands out, and the thing most likely to leak.

## Decision

**Refresh tokens are opaque random secrets, rotated on every use, stored only as hashes,
with replay treated as theft.**

### Opaque, not JWT

A self-describing token cannot be revoked without a denylist, and revocability is the whole
point: rotation, per-session termination and replay detection all depend on the server being
the authority on whether a given token is still live. So: 32 bytes from a CSPRNG,
base64url-encoded.

### Hashed at rest

Only a SHA-256 hash is stored. A database disclosure yields nothing redeemable.

The hash is deliberately *not* a password hash. It must be deterministic to serve as an
index key, and the input already carries 256 bits of entropy — there is nothing for a slow
KDF to defend against. (One-time codes are the opposite case: a six-digit code enumerates
instantly, so those use a **keyed** HMAC.)

### Rotated on every use

Each exchange marks the presented token `Rotated` and issues a replacement. Spent links are
**kept, not deleted** — detection depends on the record still being there.

### Replay ⇒ revoke the session

A well-behaved client never presents the same refresh token twice. If one is replayed, the
safe reading is that the chain has leaked, and the aggregate revokes the entire session:
every token in it, including the one the genuine client is holding.

That signs out the legitimate member. It is the correct trade — the alternative is leaving
an intruder with a live session — and it produces a `Critical` security event so someone can
act on it.

### Absolute session ceiling

Rotation never extends a session past `AbsoluteSessionLifetimeDays`. A new token's expiry is
`min(now + refreshLifetime, session.AbsoluteExpiresAt)`. Without this, a stolen device could
keep a session alive forever by refreshing.

### Account state re-checked on every rotation

A session that predates a suspension must not outlive it by quietly refreshing.

## Consequences

**Good**

- A leaked refresh token has a bounded window and leaves evidence.
- Revocation is real: a revoked session's tokens stop working on the next request.
- Storage compromise does not yield usable credentials.

**Costs**

- The rotation chain grows with every refresh and needs periodic pruning (expired sessions
  can be swept; the retention window must outlive the refresh lifetime or detection breaks).
- A client that loses a response mid-rotation is signed out. Mitigation is client-side:
  persist the new token before acting on the response.
- Replay detection punishes the victim as well as the attacker. Accepted deliberately.

## Alternatives rejected

**Non-rotating long-lived refresh tokens.** Simpler, no chain to store — and no way to
detect that a token has been copied.

**Rotation without keeping spent tokens.** Half the benefit. A replayed token would look
like an unknown token, indistinguishable from a typo, and theft would go unnoticed.

**Sliding expiry with no absolute ceiling.** Friendlier, but a session that can be renewed
indefinitely is a permanent credential wearing a short-lived costume.
