# Alert catalogue

The five alerts this platform launches with, and every other instrument its own source says is
worth alerting on.

**Written by hand and held to the source by `AlertCatalogArchitectureTests`**, which fails when
this file names an instrument that does not exist, names a meter the host never subscribes, or
leaves out an instrument whose own documentation discusses alerting. A catalogue that can name a
metric nobody emits is the same failure as a comment that asserts the opposite of the code.

This is BARCH-008 step 3. Steps 1 and 2 — a collector, and somewhere queryable for the logs — are
deployment decisions no commit can make.

## Nothing below works until telemetry leaves the process

Every instrument here is computed whether or not anything collects it. Until
`OpenTelemetry__OtlpEndpoint` names a collector, all of it is discarded, and no alert in this file
can fire. A Production host now refuses to start in that state (`SV9002`), so the order is:
set the endpoint, confirm metrics arrive, then build these five.

## The five for launch

| # | Alert | Meter | Instrument | Filter | Fires when | First thing to do |
|---|---|---|---|---|---|---|
| 1 | Unverified webhook signature | `Diaspora.Financial` | `diaspora.financial.webhooks_received` | `outcome = unverified` | any non-zero over 5 minutes | Check the provider's webhook secret before assuming an attack. A rotated secret stops every payment confirming, silently, and looks identical to someone probing. |
| 2 | Outbox dead-lettering | `Diaspora.Platform.Outbox` | `diaspora.outbox.dead_lettered` | `reason != Skipped` | any non-zero | Read the `reason` tag first — it routes the response. `MaxAttemptsExceeded` is usually transient and replayable; `UnknownEventType` means this deployment no longer knows what the message is and will fail forever until the right assembly ships. |
| 3 | Unexpected errors | `Diaspora.Platform.Http` | `diaspora.http.problem_responses` | `code = Platform.Unexpected` | any non-zero sustained over 10 minutes | An exception no handler mapped. Take the `correlationId` from any one response and read the log line; this is the number report 3's BBL-001 and BBL-002 would have surfaced through. |
| 4 | Server error rate | `Diaspora.Platform.Http` | `diaspora.http.problem_responses` | `status >= 500` | step change against the preceding 24 hours | Compare with alert 3 before escalating. A 503 is deliberate (`Platform.Dependency.Unavailable`) and means a dependency is down; an unmapped 500 is a defect. They need different people. |
| 5 | Rate-limit refusals | `Diaspora.Platform.Http` | `diaspora.http.problem_responses` | `code = Platform.RateLimit.Exceeded` | step change against the preceding 24 hours | Either a budget set below what normal use needs, or credential stuffing. Split the series by route: sign-in and one-time codes moving alone is the second; everything moving together is the first. |

Three of the five read one instrument, because `diaspora.http.problem_responses` carries `code` and
`status` as dimensions and an alert is a filter rather than a counter. That instrument is recorded
in `PlatformProblem`, which is the single factory for the one error shape the platform emits — so a
sixth way to fail cannot appear without being counted. Counting in the exception middleware instead
would have missed alert 5 entirely: the rate limiter refuses in its own rejection callback and never
reaches a handler.

### The two thresholds that cannot be set yet

Alerts 1, 2 and 3 fire on any non-zero value, so they work from the first hour — each counts
something the platform's own source says should be zero.

Alerts 4 and 5 are rates, and a rate needs a baseline. This platform has never collected one,
because of the finding this catalogue closes a third of. Until a week of real traffic exists, both
are set as a step change against the preceding 24 hours, which is the strongest statement available
on day one. Replace them with absolute thresholds once there is a normal to compare against, and
treat the first week's numbers as observation rather than as a target.

## Also documented as worth alerting on

Not part of the launch five, and each one is a sentence its own source already wrote. Add them as
the platform earns the traffic to make them meaningful.

| Meter | Instrument | What a non-zero value means |
|---|---|---|
| `Diaspora.Financial` | `diaspora.financial.unbalanced_postings` | A posting whose legs did not balance. A caller defect: somebody is trying to move money in a way the ledger cannot express. Should always be zero. |
| `Diaspora.Financial` | `diaspora.financial.unresolved_policies` | A price the policy engine could not resolve. The refusal is correct behaviour, and it also means a member could not buy something. |
| `Diaspora.Financial` | `diaspora.financial.reconciliation_mismatches` | The platform's record and a provider's disagree about somebody's money. No automatic resolution is safe, so this one means a person has to look. |
| `Diaspora.Platform.Transactions` | `diaspora.transactions.refused_replays` | A transient database failure landed after a write was accepted. Each one is a failed request rather than a row written twice or lost — and a reason to look at connection health. |
| `Diaspora.Integrations` | `diaspora.integrations.outbound.blocked` | A call the allow-list refused before a socket opened. Either a misconfigured integration or something trying to reach where it should not. |
| `Diaspora.Media` | `diaspora.media.signed_urls_rejected` | Expiry is normal; a rising `invalid` count is somebody probing the signature. |
| `Diaspora.Notifications` | `diaspora.realtime.connections.refused` | A steady trickle is a client stuck in a reconnect loop. A step change is a per-member cap set below what a normal member holds. |
| `Diaspora.Location` | `diaspora.location.nearby_provider_failures` | A vertical is degrading while Location's own latency stays flat — invisible in endpoint metrics alone. |
| `Diaspora.Automation` | `diaspora.automation.history.write_failures` | A run that happened and could not be written down, tagged by the stage that failed. |
| `Diaspora.Automation` | `diaspora.automation.denied` | An action refused because the owner no longer holds the capability. A spike is either a role change that swept a community, or somebody running rules they wrote before they lost the authority to. |

## Documented as deliberately not an alert

| Meter | Instrument | Why it is here |
|---|---|---|
| `Diaspora.Automation` | `diaspora.automation.skipped` | Runs that did nothing because a condition said so. Its own documentation says it is "not a failure and never alerted on as one". Listed so that the question is answered once, rather than re-asked by whoever next sees a counter with "skipped" in the name. |

## Known gaps

**Outbox dead-letter depth has no instrument.** The audit asked for depth; `diaspora.outbox.dead_lettered`
is a counter, so alert 2 fires on the event rather than on the backlog. The difference matters: a
rate alert clears once the failures stop, while the messages stay undelivered. Depth needs an
observable gauge over the outbox table, which means a new `IOutboxStore` query running on every
metric scrape and surviving a cold start before migrations — its own change, not a catalogue entry.

**No alert covers the sweeps.** Every timer-driven sweep — expiry, placement, withdrawal, campaign
settlement, recurring donations — fails by doing nothing, and nothing counts a sweep that did not
run. That is the same shape of invisibility as BARCH-002's, and it is not solved here.
