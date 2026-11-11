# B1.4 — Analyzer Catalog

Every diagnostic `Diaspora.Analyzers` can report. Each entry says what fires it, why the platform
cares, and what to do — because a four-character code at the bottom of a build log is only useful if
it can be looked up.

**Where this file lives, and why it moved.** `src/Diaspora.Analyzers/AnalyzerCatalog.md`, beside
`AnalyzerReleases.Shipped.md` and `AnalyzerReleases.Unshipped.md`, and committed like them. It was
`docs/platform/b1.4-analyzer-catalog.md` until 11 November 2026, where `AnalyzerCatalogTests` read
it — so that test passed on every working tree and threw `DirectoryNotFoundException` on a clean
checkout. Nobody saw it, because the analyzer suite was not in CI either; its first CI run is what
found it. It could not be rescued where it stood, because `docs/` is excluded as a *directory* and
git cannot re-include a file inside one. **A guardrail's input must be in the repository, or the
guardrail is a comment.**

**Where the pack comes from.** `src/Diaspora.Analyzers`, targeting `netstandard2.0` because the
compiler loads an analyzer into its own process. It is handed to every compilation from
`Directory.Build.props` as a `ProjectReference` with `OutputItemType="Analyzer"` and
`ReferenceOutputAssembly="false"` — one wiring, no project can be missed, and nothing in the platform
can call the analyzers.

**Ids are permanent.** A retired rule keeps its number. Reusing one would silence a rule nobody
chose to silence, years after the suppression was written.

**Five ids in this space are not analyzer rules.** `DSP2004`, `DSP1005`, `DSP3005`, `DSP3006` and
`DSP4005` are *compiler obsoletions* — the `DiagnosticId` on an `[Obsolete(error: true)]` attribute a
retired type carries — so they appear in no `AnalyzerReleases` file and no analyzer emits them. They
are reserved here under the same permanence rule, because a suppression written against one must
silence the thing it was written for. `DSP2004` is B1.4.1's dormant notification port and its two
model types; the other four cover the **fifteen** types PSR-1 Final Certification retired. See
[DSP2004](#dsp2004--the-dormant-notification-port-reserved) and
[Retirements](#retirements--compiler-obsoletions).

**Severity is chosen against a Release build that promotes warnings to errors.** A rule is a warning
only when a violation is a defect with no legitimate reading. Two rules ship as suggestions because
turning them up would demand a breaking change to strings shipped clients already hold.

---

## DSP1xxx — Domain event safety

### DSP1001 — A handler must not depend on another handler
**Severity:** Warning

Fires when a command, query or event handler takes another handler as a constructor parameter, or
takes `IMediator`, `ISender` or `IPublisher`.

**Why.** A handler that calls another handler makes settlement order implicit — it becomes whatever
the call graph is that day — and hides which of the two owns the consequence. B7 found the shape and
extracted a shared service; this keeps it extracted. The mediator is the same defect with an
indirection in front of it: a handler holding `ISender` can reach every handler in the process.

**Fix.** Extract the shared behaviour into a service and inject that into both handlers.

---

### DSP1002 — One domain event is settled by more than one handler in this module
**Severity:** Warning

Fires when two handlers of the same domain event both *settle* it — commit, stage a repository
write, or publish an integration event.

**Why.** Multiple handlers for one event is a normal, useful shape: one settles, another projects a
read model or emits telemetry. Multiple handlers *settling* one event is not. The business outcome
then depends on the order MediatR resolves them in, which is registration order, which is assembly
scan order.

**Fix.** Give one handler the settlement. Leave the others their isolated responsibility.

**Note.** This is a compilation-end diagnostic — it needs the whole assembly, so an IDE shows it
under full-solution analysis.

---

### DSP1003 — One integration event is settled by more than one consumer in this module
**Severity:** Warning

**Why.** Fan-out *across* modules is the entire point of a published contract. Fan-out *inside* one
module is ownership ambiguity: delivery is at-least-once, so two settling consumers means two
chances to apply one consequence twice, in an order nothing guarantees.

**Fix.** One consumer per module per contract. If two things must happen, they happen in one
consumer, in a defined order.

---

### DSP1004 — A handler must not serve both the domain and the integration pipeline
**Severity:** Warning

Fires when one type implements both `INotificationHandler<DomainEventNotification<…>>` and
`INotificationHandler<IntegrationEventNotification<…>>`.

**Why.** B1.2's base classes take one type argument, so a handler serving several events implements
the interface directly — which Marketplace does, legitimately, seven times. Nothing then stops both
wrappers appearing on one type. A domain event handler runs inline, inside the transaction that
raised the event, exactly once, and may read uncommitted state; an integration consumer runs after
commit, out of band, at least once, in its own scope. A type doing both is written against one set of
assumptions and wrong about the other half of itself.

**Fix.** Split it.

---

## DSP2xxx — Messaging

### DSP2001 — A type must not be both a domain event and an integration event
**Severity:** Error

**Why.** B1.2 separated the two hierarchies so that dispatching a published contract through the
domain pipeline is a compile error rather than a silent bypass of the outbox. A type implementing
both interfaces by hand restores the defect wholesale — the compiler would accept every route the
phase closed.

**Fix.** Pick one. A module's private fact is a `DomainEvent`; a published fact is an
`IntegrationEvent` in `Diaspora.Contracts`, translated deliberately.

---

### DSP2002 — An integration event must be published through `IIntegrationEventPublisher`
**Severity:** Warning

Fires when a business module names `IOutboxWriter`, `IOutboxStore`, `IOutboxTransport`,
`IOutboxDispatcher` or `OutboxMessageFactory`.

**Why.** The publisher's only implementation writes an outbox row inside the caller's transaction,
which is what makes "the fact was recorded" and "the fact was announced" one atomic decision.
Reaching the transport directly announces facts that may never have committed and loses the ones
that did when the process dies.

**Fix.** Inject `IIntegrationEventPublisher`.

**Note.** Matched by name rather than by symbol. No module can reference the outbox today, so a
semantic rule would resolve nothing and see nothing; the point is to catch the reference at the
moment somebody adds it.

---

### DSP2003 — Only the Communication Platform creates notifications
**Severity:** Warning

Fires when a module other than `Diaspora.Modules.Notifications` names `INotificationPublisher`,
`INotificationDispatcher` or `NotificationRequest`.

**Why.** A business module that sends its own notification has taken on the member's channel
preferences, quiet hours, digest batching, localisation and delivery retries — none of which it can
see. B13 owns all of it and learns what happened the way every other consumer does: from a published
contract.

**Fix.** Publish an integration event. If B13 cannot build the notification from the payload, enrich
the contract.

---

### DSP2004 — The dormant notification port (reserved)
**Severity:** Error · **Emitted by:** the C# compiler, not this pack

Not an analyzer rule. `DSP2004` is the `DiagnosticId` on the `[Obsolete(error: true)]` attribute
carried by `INotificationPublisher`, `NotificationRequest` and the kernel's `NotificationChannel`
since B1.4.1. Naming any of the three, from anywhere, is a compile error.

**Why a second id for a rule `DSP2003` already covers.** `DSP2003` is scoped to business modules,
which is correct — the kernel and the Communication Platform implement the thing it protects. That
leaves the two places an implementation could actually appear outside the analyzer's reach. B1.4
recorded the port as finding F5: declared in B1 before there was a Communication Platform, never
implemented, never called, reading as available and not being.

**Why obsoleted rather than implemented or deleted.** Implementing it — even as a thin adapter over
B13's dispatcher — would be a supported, discoverable, kernel-level way for any module to create a
notification directly, which is the one thing B13 exists to prevent. Deleting it would leave an
empty namespace that invites the interface to be declared again by somebody with no way to know it
had been refused. Obsoletion is also the option that preserves binary compatibility: the type is
unchanged in the assembly.

**Fix.** Publish an integration event through `IIntegrationEventPublisher`.

**It cannot be suppressed.** `#pragma warning disable DSP2004` and a `NoWarn` entry both do nothing:
a pragma suppresses warnings and this is an error. The only way past it is deleting the attribute
from the kernel — which is a visible edit in a phase that says so, alongside
`The_dormant_notification_port_stays_dormant` and `DormantPortTests`. That is the point of choosing
`error: true` over a warning: a Release build would have turned a warning into an error anyway, but a
warning can be waved through in one file by one person, and this cannot.

`MessagingBoundaryAnalyzerTests.Naming_the_obsoleted_notification_port_does_not_compile` asserts it
from a business module and from the Communication Platform, which `DSP2003` exempts.

---

## DSP3xxx — Persistence

### DSP3001 — A `Revision` property must be configured as a concurrency token
**Severity:** Warning

Fires when `builder.Property(x => x.Revision)` appears in a fluent chain with no
`IsConcurrencyToken()` anywhere in it.

**Why.** `Revision` exists to make a lost update loud. Mapped as an ordinary column it becomes a
number that increments and protects nothing: the second writer's `UPDATE` succeeds, matches one row,
and discards the first writer's change with no error anywhere.

**Fix.** Add `.IsConcurrencyToken()`. Position in the chain does not matter.

---

### DSP3002 — An owned collection must declare its key
**Severity:** Warning

Fires on `OwnsMany(…)` whose configuration block declares no `HasKey` of its own.

**Why.** Left to convention, EF invents a store-generated shadow integer inside a composite primary
key — which it cannot generate. The insert fails the first time a second row exists, which is why it
survives every test that starts from an empty table. B11 paid for this one.

**Fix.** `HasKey(x => x.Id)` inside the block, and `ValueGeneratedNever()` when the caller supplies
the key.

**Note.** A `HasKey` belonging to a nested `OwnsMany`/`OwnsOne` does not count — otherwise an outer
mapping borrows an inner one's declaration and passes having declared nothing.

---

### DSP3003 — A strongly typed identifier must not be stringified or ordered inside a query
**Severity:** Warning

Fires on `ToString()`, `CompareTo()` or `string.Compare(… .ToString() …)` applied to a
`StronglyTypedId` (or its unwrapped `Value`) inside a lambda handed to a LINQ operator that becomes
SQL.

**Why.** Identifiers here are value-converted records and no provider translates any of those three
shapes. Each compiles, passes a suite that reads before it writes, and throws "could not be
translated" the first time a second page is asked for. B10 shipped it into five repositories in one
phase, which made page two of every feed a 500.

**Fix.** `Persistence.Paging.CursorBoundary` — exclude identifiers by set membership instead of
comparing them.

**Not flagged.** Ordering by a slug (the platform's documented tie-break, translates everywhere) and
stringifying an identifier inside a `Select` projection (which happens after the rows have arrived).

---

## DSP4xxx — CQRS

### DSP4001 — A query handler must not write
**Severity:** Warning · **Escape:** `[SideEffecting]`

**Why.** A query is meant to be safe to retry, to cache, to run against a replica and to reach from
a GET. A handler that writes is none of those and nothing in its name says so.

**Fix.** Move the write to a command — or, if it is a genuine read-path counter, declare it:

```csharp
[SideEffecting(Reason = "Records the view count the seller watches, on the read that caused it.")]
public sealed class GetListingQueryHandler : IQueryHandler<GetListingQuery, ListingResponse>
```

Four handlers on this platform record the view their read caused, deliberately. The attribute is
what makes an exception somebody chose distinguishable from one nobody noticed. It is accepted on
the handler or on the query.

---

### DSP4002 — A handler that saves twice in a row must declare `[Transactional]` on its command
**Severity:** Warning

Fires when a command handler contains two or more `SaveChangesAsync` calls as *unconditionally
sequential statements in one block*, and the command type is not `[Transactional]`.

**Why.** One save is already atomic — EF wraps it. Two sequential saves are two transactions, and
the window between them is a real state the system can be found in.

**Fix.** Mark the **command**:

```csharp
[Transactional(Reason = "Writes the endorsement row and the insight count together.")]
public sealed record EndorseNeighborhoodInsightCommand : ICommand<NeighborhoodInsightResponse>;
```

**Note.** Only unconditionally sequential saves count. A save inside an `if`, a `try`, a loop or an
early-returning branch is a save on *a* path, not on *this* path — counting those is what a textual
grep does, and it reports twenty-two handlers on this platform that commit exactly once per
execution.

---

### DSP4003 — A command handler that stages a write must save it
**Severity:** Warning

Fires when a command handler calls a repository's `Add`/`Update`/`Remove` and contains no
`SaveChangesAsync` anywhere.

**Why.** Those methods only mark the change tracker. Without a save the handler returns a successful
`Result`, the API answers 201, and the row was never written — the failure mode that survives an
integration test asserting the response.

**Fix.** Save, or hand the work to a service that does (in which case the handler should not be
touching the repository directly).

---

### DSP4004 — `[Transactional]` belongs on the command, not on the handler
**Severity:** Warning

**Why.** `TransactionBehavior` reads the attribute off `TRequest` — the command — because that is
the type MediatR closes the pipeline over. On a handler the attribute compiles, reads as a
guarantee, and opens nothing.

**This rule found a live defect.** B4's `DeclareCountryExperienceCommandHandler` carried
`[Transactional(Reason = "Writes the experience and its expertise claims together.")]` with a class
comment asserting a transaction, and no transaction had ever been opened for it. That is B2's
logging-only transaction behaviour again, in one class, four phases after it was fixed.

**Fix.** Move it to the command — or delete it, if the handler saves once and does not need one.

---

### DSP4006 — A request must declare what it requires of its caller
**Severity:** Warning

**Why.** `AuthorizationBehavior` fails closed: a request carrying none of
`[RequirePermission]`, `[RequirePolicy]`, `[RequireAuthentication]` or `[AllowAnonymous]` is
refused at runtime. A refusal is the safe answer but a late one — the author learns from a 401 in
a test, or from a support ticket. This rule moves it to the build, where the author is still
looking at the file.

**Note the number.** This rule was introduced as `DSP4005` by BSOLID-001 and renumbered on
11 November 2026. `DSP4005` had been reserved a phase earlier for the seven retired `SharedKernel`
primitives, which carry it as an `[Obsolete(error: true)]` `DiagnosticId` — see
[Retirements](#retirements--compiler-obsoletions). The two mechanisms live in different files and
neither can see the other, so the collision shipped. It is now enforced by
`DiagnosticIdArchitectureTests`, which reads both sides from source.

**Fix.** Name what the request requires of its caller, or mark it `[AllowAnonymous("why")]`. There
is no fifth option: "I have not decided" is the state the rule exists to remove.

---

## DSP5xxx — The contributor model

### DSP5001 — A feature gate name must be `module.capability`
**Severity:** Warning

Lower case, dot separated, at least two segments. Gate names are a client-facing vocabulary: the
frontend switches on them and the platform attributes conflicts by their first segment.

### DSP5002 — A permission key must be `module:resource:action`
**Severity:** Warning

Lower case, colon separated, at least two segments. Colons for permissions, dots for policies — the
two vocabularies look different on purpose, so a policy name and the permission behind it can never
be confused in an attribute, a log line or a token claim.

**Two segments, not three.** `PermissionDefinition`'s doc comment says three and the shipped
catalogue disagrees with it seventy-six times: `bookmarks:read`, `community-life:rsvp` and
`content:publish` are all real and all reasonable — the resource segment adds nothing when the module
has one. The rule enforces what the platform does. The divergence is recorded as a readiness finding.

### DSP5003 — A policy name must be dotted and namespaced to its module
**Severity:** Warning

The shape is load-bearing (a policy name must not collide and must not be confusable with a
permission); the casing is not, and is `DSP5006` instead.

### DSP5004 — Prefer the platform's permission vocabulary
**Severity:** Info

`view`, `browse`, `feed`, `list`, `get`, `fetch` and `retrieve` all mean **read**. `add` and `new`
mean **create**. `destroy` and `erase` mean **delete**. A catalogue that uses five words for one idea
cannot be reasoned about by anyone who did not write it.

A suggestion rather than a warning because permission strings are already minted into access tokens
and held in the frontend's capability map: new keys should follow the vocabulary, existing ones
change at a versioned authorisation release. The platform has five `…:view` keys today, listed in
the readiness report.

A qualifier is not a synonym — `read-any` is a different authority from `read` and is not flagged.

### DSP5005 — A contributor must be sealed and constructible without the container
**Severity:** Warning

Sealed because a contributor is a declaration, not an extension point — the extension point is the
interface. Parameterless **only** for the six seams the kernel builds itself with
`Activator.CreateInstance` before the container exists: `IPermissionContributor`,
`IFeatureContributor`, `IPolicyContributor`, `IObservabilityContributor`, `IEndpointContributor`,
`IDatabaseContributor`. The two dozen reference-data contributors a vertical declares are resolved
from DI like anything else, and Guides ships one that legitimately takes a repository.

Test assemblies are exempt: the kernel refuses to scan them, so a rule about what the kernel can
construct has nothing to say about assemblies it never looks at.

### DSP5006 — Catalogue names are lower case
**Severity:** Info

B13 and B14 both shipped PascalCase policy names (`Notifications.Recipient`,
`Recommendations.Curator`). Renaming them was a change to live authorisation wiring, not to a
comment, so this reports the inconsistency at the severity that says *this is inconsistent* rather
than *this is broken*.

**Closed in B1.4.1.** All six were renamed to lower case, each keeping its old name as a registered
alias that authorises identically — policy names are backend-only, which is what made this the one
naming finding closeable in a patch release. The rule reports nothing on the platform today, and
`Every_policy_name_is_lower_case` asserts that against the *composed* catalogue rather than against
the declarations the analyzer can resolve, which is a wider net. See the
[Authorization Guide](authorization-guide.md#4-renaming-a-policy-the-alias-mechanism).

The rule stays, at Info, for the next module.

### DSP5007 — A catalogue name must be resolvable at the declaration (reserved)

**Not implemented. The id is reserved here under the permanence rule**, exactly as `DSP2004` is.

`DSP5002`, `DSP5004` and `DSP5006` all resolve a name through the semantic model at the `new` site.
When the first argument is a *parameter* rather than a constant, `GetConstantValue` returns null and
all three **skip the declaration silently** — so a module that builds its definitions through a
factory helper is exempt from three naming rules without anybody deciding it should be.

B14 did exactly that, and B1.4.1 recorded it as F8. **PSR-1 closed the consequence rather than the
rule:** B14's contributor now constructs every definition inline — which produced thirteen genuine
`DSP5002` errors that had never been reported in the module's life — and
`No_catalogue_definition_hides_its_name_behind_a_factory` fails the next factory helper.

What `DSP5007` would add is the report *as the code is typed*, one step earlier than the architecture
rule. It needs following a parameter back to its call sites, which is a compilation-level action
rather than a syntax-node one. See the [Analyzer Backlog](psr-1-analyzer-backlog.md).

---

## DSP6xxx — AI readiness

### DSP6001 — A business module must not name an AI vendor
**Severity:** Warning

Fires on a `using` of `OpenAI`, `Anthropic`, `Azure.AI`, `Microsoft.SemanticKernel`, `Microsoft.ML`,
`Google.Cloud.AIPlatform`, `GenerativeAI`, `Mistral`, `Cohere`, `Ollama`, `LLama`, `LangChain`,
`Pinecone`, `Qdrant`, `Weaviate`, `Milvus` or `HuggingFace` inside a module, the application layer or
the domain.

**Why.** B14 shaped `IRankingStrategy`, `ISimilarityProvider`, `IRecommendationCandidateSource` and
`ISearchSignalProvider` so an embedding model, a vector index or an LLM ranker could replace an
implementation without a line of business code changing. A vendor named inside a vertical makes that
vertical undeployable without an API key, unrunnable in tests without a network, and
non-deterministic in a module explicitly built to be explainable. None of that shows up in a review
of the pull request that adds the `using`.

**Fix.** Implement the seam in an adapter assembly and register it after the module. Infrastructure
assemblies are not flagged — that is where a model client belongs.

---

## Retirements — compiler obsoletions

*Added by PSR-1 Final Certification.*

Four ids reserved for `[Obsolete(error: true)]` attributes rather than analyzer rules, covering the
**fifteen** framework types the certification retired — one under `DSP1005`, three under `DSP3005`,
four under `DSP3006` and seven under `DSP4005`. **No analyzer emits them**; the compiler does, at the
moment somebody reaches for a type the platform has considered and refused.

**Why retire rather than delete**, in one line each time it comes up: *an empty namespace is an
invitation to declare the interface again, and the next author would have no way to know it had been
considered and refused.* B1.4.1 established that for `INotificationPublisher`; PSR-1.2 named it as
the intended treatment for DEBT-072; this phase applied it.

### DSP1005 — The superseded domain event handler marker
**Type:** `Diaspora.Application.Abstractions.IDomainEventHandler<TEvent>`

A B1-era marker over `INotificationHandler<T>`. B1.2 replaced markers with base classes because
**the choice of base class states which pipeline a handler belongs to and the compiler holds it
there** — `DomainEventHandler<T>` is in-process, inside the transaction, before the outbox;
`IntegrationEventConsumer<T>` is out-of-process, after commit. A handler implementing the marker
joins the domain pipeline without saying so, which is the shape ADR-008 and `DSP1004` exist to
prevent.

**Fix.** Derive from `DomainEventHandler<T>` or `IntegrationEventConsumer<T>`.

### DSP3005 — The Guid-keyed generic repository
**Types:** `Diaspora.Domain.Abstractions.IReadRepository<T>`,
`Diaspora.Domain.Abstractions.IRepository<T>`, `Diaspora.Persistence.RepositoryBase<T>`

All three key on a bare `Guid`, which `DSP3001` reports as an error — so a repository written
against them takes exactly the parameter type the platform refuses. `IReadRepository<T>`'s
unfiltered `ListAsync()` also predates ADR-017's cursor model, and `RepositoryBase<T>.Delete` issues
a hard delete on aggregates the platform tombstones.

**The finding worth keeping.** PSR-1.2's seam audit enumerated interfaces with no implementing type;
`IRepository<T>` had `RepositoryBase<T>`, so it counted as live. What a count cannot see is that
`RepositoryBase<T>` is abstract with **zero subclasses** — a complete, compiling, fully-implemented
abstraction that nothing uses. Same lesson as `IEligibilityService`: *a stub is worse than an
absence, because every rule written to catch an absence passes over it.*

**Fix.** Declare a repository in the owning module, keyed on the aggregate's strongly-typed
identifier, taking a `CursorBoundary` where it returns a collection.

### DSP3006 — The unadopted specification pattern
**Types:** `Diaspora.SharedKernel.Specifications.Specification<T>` and its three combinators
`AndSpecification<T>`, `OrSpecification<T>`, `NotSpecification<T>`

Declared in B1, adopted by nothing. Every module since composes its queries directly against EF, and
B1.3's `CursorBoundary` with ADR-017 settled how this platform reads collections.

**The trap no reviewer would see at the call site.** `IsSatisfiedBy` calls
`ToExpression().Compile()` — building and JIT-compiling a delegate from an expression tree — on
*every invocation*. Used the obvious way, inside a loop over a page of results, that is one
compilation per row.

**Fix.** Compose the query directly and page it with `CursorBoundary`.

### DSP4005 also covers the parallel result types
**Types:** `Diaspora.SharedKernel.Results.OperationResult`, `GenericOperationResult<T>`,
`ValidationResult`, `GenericValidationResult<T>`

Four result types with no consumer, competing with `Result`, `Result<T>` and `Error`, which every
handler, endpoint and aggregate method on the platform returns.

**`ValidationResult` carries a defect as well as a duplication.** It derives from `Result` and passes
only `errors[0]` to the base, so any consumer holding it as the `Result` it claims to be sees one
error and silently loses the rest.

**Where all nine of these came from.** B2.5's production-readiness audit, finding **DDD-02**, which
listed the SharedKernel's dead machinery and named the risk precisely: *"Six result types where one
plus `Error` suffices … New module authors will copy whichever they find first."* The finding was
recorded, never closed, and **never reached the Technical Debt Ledger** — which is how it survived
fifteen phases as debt nobody was counting. Closed in PSR-1 Final Certification.

### DSP4005 — The exception-based business rule primitive
**Types:** `Diaspora.SharedKernel.Primitives.IBusinessRule`,
`Diaspora.SharedKernel.Primitives.BusinessRuleExtensions`,
`Diaspora.SharedKernel.Primitives.BusinessRuleViolationException`

A rule object checked through `CheckRule()`, which throws. **No domain rule on the platform ever
implemented it**; every module reached for `Result<T>` and `Error`, which is the better answer
because *a broken business rule is a member's mistake, not a bug*. An exception unwinds past every
handler between the rule and the middleware, so only a global `catch` that has lost all the context
can answer it.

It also produced a name collision: two types called `BusinessRuleViolationException`, both mapped to
the same 422, each named by full namespace in `GlobalExceptionHandlingMiddleware` because neither
could be written unqualified. `Diaspora.Application.Exceptions.BusinessRuleViolationException`
remains the supported escape hatch and is **not** obsolete.

**Fix.** Return `Result.Failure(Error.Validation(code, message))`.

---

## Suppressing a diagnostic

See the [Suppression Guidelines](b1.4-suppression-guidelines.md). Short version: prefer fixing;
where an existing contract makes that impossible, scope the suppression to one file in
`.editorconfig`, write the reason and the condition that would remove it, and expect an architecture
test to check that you did.
