# ADR-019 — The ledger is the truth, the wallet is a projection, and only B15 owns money

**Status:** Accepted · **Phase:** B15 — Financial Platform · **Date:** 2026-08-05

---

## Context

Thirteen modules had shipped before any of them could take a payment. Marketplace held prices,
Housing held rents, Jobs held salaries and Community Life held ticket prices — all of them *display*
figures, none of them money that had moved. Three modules had an `IsFeatured` flag a moderator set for
free, and reputation granted a fourth route to being featured.

B15 had to introduce money without any of those becoming a place money lives.

Three decisions were available and only one of them survives contact with a second module:

1. **Each vertical owns its own monetisation.** Marketplace charges for boosts, Jobs charges for
   featured postings, each holding a rate and a payment path. This is what happens by default.
2. **A shared billing library.** Every vertical references it and calls it.
3. **A platform that owns all financial state, which verticals reach through published contracts and
   a catalogue.**

---

## Decision

**The third, with four structural commitments.**

### 1. The ledger is append-only and is the canonical record

`LedgerEntry` has one factory and no public mutator. It implements neither `ISoftDeletable` nor
`IAuditableEntity`, because both exist to record that something changed and nothing about an entry
ever does. Correcting the ledger means appending a compensating entry.

Every transaction balances per currency. The platform's **clearing wallet** is the counterparty to
everything arriving from outside, and is the only account on the platform allowed to run negative.

### 2. The wallet is a projection

No member of `Wallet` sets a balance. The only one that moves one takes a `LedgerEntry`.
`RebuildFrom` replays from zero and produces the same numbers.

### 3. No business module owns financial state

`Diaspora.Modules.Financial` references no vertical at all. A purchase is about a
`SubjectReference` — a type and a GUID — which the module never resolves, joins to, or validates.

### 4. Purchased placement is separate from editorial featuring

`isFeatured` stays with the module that renders it. A `Placement` is what was bought, under which
policy version, for how long, by whom, for how much.

---

## Consequences

**Buying a boost for a listing that does not exist succeeds.** Accepted. The alternative is a
synchronous call into Marketplace on the payment path, which makes a marketplace outage a payment
outage. The vertical validates the subject when it consumes the published contract; an unclaimed
placement expires on its own.

**Two flags describe "featured".** Accepted, and it is the point. Keeping them apart is what makes
*"did this rank because it is good or because somebody paid"* answerable. A consumer that wants only
paid placement reads `PlacementActivatedIntegrationEvent`; one that wants editorial featuring reads
the vertical's own field.

**A vertical cannot compute its own commission.** It contributes an `IFinancialPolicyContributor` and
an `IMonetizationProductContributor` and holds no rate. Correct: a rate held in two places is a rate
that will differ in one of them.

**Correcting a mistake costs two rows.** Accepted. The wrong number stays visible for ever, which is
the entire value of the record.

**One nominal account runs negative.** Accepted, and unavoidable — in a double-entry system one
account must. Making it the platform's own keeps every member-facing balance provably non-negative.

---

## Alternatives rejected

**Per-vertical monetisation.** Six verticals would each hold a rate, a payment path and a refund
policy, and they would diverge within two phases. The platform has this failure on record for
narrower things than money — B8's guide resolvers, four workarounds for one missing query.

**A shared billing library.** Every vertical would reference it, which makes it a dependency of six
modules rather than a consumer of none, and inverts the module graph the platform spent B13 and B14
establishing. It would also put payment code inside a marketplace transaction.

**A mutable balance with an audit log beside it.** The common shape, and the one that fails silently:
the balance and the log drift, and there is no way to tell which is right. A projection derived from
an append-only record has one answer by construction.

**Storing money as `decimal`.** Exact in .NET; not exact once it has been through a JSON parser, a
JavaScript client, an analytics pipeline or a column somebody declared with the wrong scale.

**One published contract per placement kind and subject type.** Sixty contracts. Marketplace settled
this in B10 with one contract and a discriminator.

---

## Enforcement

| Claim | Rule |
|---|---|
| The ledger is append-only | `A_ledger_entry_cannot_be_changed_after_it_is_appended` |
| The wallet is a projection | `A_wallet_has_no_way_to_set_a_balance` |
| No business module owns financial state | `The_module_references_no_other_module` |
| No payment provider leaks in | `No_payment_vendor_is_named_in_this_assembly` |
| One door into the ledger | `Only_one_type_posts_to_the_ledger` |
| Conversion and settlement stay unimplemented | `Conversion_and_settlement_remain_declared_and_unimplemented` |
| B15 creates no notification | `The_module_never_creates_a_notification` |

---

## See also

- [Financial Platform Architecture Guide](../financial/architecture.md)
- [Ledger Design Guide](../financial/ledger.md) · [Premium Features Guide](../financial/premium-features.md)
- [ADR-013 — Communication ownership](adr-013-communication-ownership.md) — the same shape, for notifications
