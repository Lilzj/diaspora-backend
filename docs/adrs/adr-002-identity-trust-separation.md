# ADR-002 — Identity owns access state; the Trust Engine owns verification

**Status:** Accepted · **Phase:** B2 · **Supersedes:** nothing

---

## Context

The original roadmap put "is this member Diaspora Verified?" inside the identity phase.
Identity would collect verification evidence, evaluate it, and set the member's status.

That conflates two questions with very different rates of change:

- **Who is this, and may they hold a session?** Governed by cryptography and protocol
  correctness. It changes when a token format or an algorithm changes — rarely, and only
  with great care.
- **Do we trust this person enough to let them publish?** Governed by policy: residency
  evidence, reputation, contributor eligibility, and whatever Diaspora learns as it grows.
  This will change often, and should.

Putting them in one subsystem means every trust-policy revision is a change to the code
that issues credentials. That is precisely the code where churn is most expensive and most
dangerous.

## Decision

**Identity (B2) knows about identity and access state. It never decides verification.**

Concretely:

1. `User.RequestVerification()` moves `LimitedAccess → VerificationPending`. This is the
   *only* verification-related decision Identity makes: that a request exists.
2. A `UserVerificationRequested` integration event is published. Identity does not wait for
   a reply and holds no model of what happens next.
3. The Trust & Verification Engine (B3) replies through `POST /trust/decisions` with one of
   `UnderReview`, `Granted`, `Rejected`, `Revoked`.
4. `User.ApplyTrustDecision()` translates that outcome into a position on the access ladder
   and nothing more. It does not store evidence, scores, or reasoning beyond a free-text
   reason string.

## Enforcement

A documented boundary is a boundary until someone is in a hurry. This one is enforced three
independent ways:

**Domain.** `AccessStateTransitions` has no edge from `LimitedAccess` to `Verified`. The
only paths into `Verified` are from `VerificationUnderReview` (a Trust Engine decision) or
from `Suspended`/`Locked` (restoring a member who was already verified).

**Authorisation.** The endpoint requires `identity:trust:apply-decision`, and
`PermissionResolver` grants it to **no role in any access state**. Reaching it requires a
service token — a distinct token type that carries no session and cannot be obtained by
signing in.

**Tests.** Each layer asserts it independently:
- unit — `Identity_cannot_grant_verification_without_the_trust_engine`
- architecture — `Verification_is_reachable_only_through_the_trust_engine`,
  `No_human_role_can_apply_a_trust_decision`
- integration — `Identity_cannot_verify_a_member_on_its_own`

## Consequences

**Good**

- Trust policy is rewritable without touching the authentication subsystem. Residency rules
  can change weekly; token issuance does not notice.
- Identity's test surface stays small and its security properties stay auditable.
- Verification evidence — documents, addresses, references — never enters the identity
  database. That data has different retention and privacy obligations, and keeping it out
  of the credential store is worth doing on its own.
- B3 can be extracted into a separate service later. The contract is already an HTTP call
  and an event.

**Costs**

- Two round trips to verify someone instead of one in-process call.
- Identity must model `VerificationPending` and `VerificationUnderReview` — states it
  cannot itself resolve, which reads as incompleteness until the boundary is understood.
- A machine-to-machine authentication path had to exist in B2 for an endpoint B3 has not
  been built to call yet.

**Rejected alternative: a shared `IVerificationService` inside Identity.** Cheaper today,
and it would have put document handling, reputation scoring and residency rules inside the
credential store — with every future policy change landing in the module that issues tokens.

## Notes

`ITrustService` from Phase B1 remains a *read* contract — other modules ask "what is this
member's trust level". B2 does not implement or write through it. B3 owns it.
