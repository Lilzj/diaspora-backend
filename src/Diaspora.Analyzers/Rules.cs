using Microsoft.CodeAnalysis;

namespace Diaspora.Analyzers;

/// <summary>
/// The descriptors, in one place, because the catalogue is a document as much as it is code.
/// <para>
/// <b>Every message names the fix.</b> A diagnostic that says "this is wrong" costs the reader
/// the same investigation the rule was written to save them; one that says "use
/// <c>CursorBoundary</c> instead" ends the matter. Each description below therefore states the
/// failure the rule prevents, because in every case the failure is one this platform has
/// already paid for once.
/// </para>
/// <para>
/// <b>Severity is chosen against a build that promotes warnings to errors in Release.</b> A rule
/// is a warning only when a violation is a defect with no legitimate reading. Where the platform
/// has a standing convention but existing published contracts predate it —
/// <see cref="PermissionVocabulary"/> is the only one — the rule ships as a suggestion, because
/// turning it up would demand a breaking change to permission strings the frontend already holds.
/// </para>
/// </summary>
internal static class Rules
{
    private const string DomainEvents = "Diaspora.DomainEvents";
    private const string Messaging = "Diaspora.Messaging";
    private const string Persistence = "Diaspora.Persistence";
    private const string Cqrs = "Diaspora.Cqrs";
    private const string Contributors = "Diaspora.Contributors";
    private const string Readiness = "Diaspora.Readiness";

    private const string CatalogUrl =
        "https://github.com/diaspora/backend/blob/main/docs/platform/b1.4-analyzer-catalog.md";

    // ── Domain event safety ──────────────────────────────────────────────

    public static readonly DiagnosticDescriptor HandlerDependsOnHandler = Rule(
        DiagnosticIds.HandlerDependsOnHandler,
        "A handler must not depend on another handler",
        "Handler '{0}' takes '{1}'. Extract the shared behaviour into a service and inject that instead.",
        DomainEvents,
        DiagnosticSeverity.Warning,
        "A handler that calls another handler makes settlement order implicit and untestable, and " +
        "it hides which of the two owns the consequence. B7 found the shape and extracted a shared " +
        "service; this keeps it extracted. Injecting IMediator, ISender or IPublisher into a handler " +
        "is the same defect with an indirection in front of it.");

    public static readonly DiagnosticDescriptor DuplicateDomainEventSettlement = Rule(
        DiagnosticIds.DuplicateDomainEventSettlement,
        "One domain event is settled by more than one handler in this module",
        "Domain event '{0}' is settled by both '{1}' and '{2}'. Exactly one handler owns persistence, " +
        "state transitions and publication for a consequence.",
        DomainEvents,
        DiagnosticSeverity.Warning,
        "Multiple handlers for one event is legitimate; multiple handlers *settling* one event is not. " +
        "When two of them save, transition state or publish, the business outcome depends on the order " +
        "MediatR happens to resolve them in — which is registration order, which is assembly scan order. " +
        "Give one handler the settlement and leave the others their isolated responsibility.");

    public static readonly DiagnosticDescriptor DuplicateIntegrationEventSettlement = Rule(
        DiagnosticIds.DuplicateIntegrationEventSettlement,
        "One integration event is settled by more than one consumer in this module",
        "Integration event '{0}' is settled by both '{1}' and '{2}' in this module. One module, one owner.",
        DomainEvents,
        DiagnosticSeverity.Warning,
        "Fan-out across modules is the point of a published contract. Fan-out *inside* one module is " +
        "ownership ambiguity: delivery is at-least-once, so two settling consumers means two chances to " +
        "apply the same consequence twice, in an order nothing guarantees.");

    public static readonly DiagnosticDescriptor HandlerMixesPipelines = Rule(
        DiagnosticIds.HandlerMixesPipelines,
        "A handler must not serve both the domain and the integration pipeline",
        "'{0}' handles both a domain event and an integration event. Split it: the two pipelines make " +
        "opposite guarantees.",
        DomainEvents,
        DiagnosticSeverity.Warning,
        "A domain event handler runs inline, inside the transaction that raised the event, exactly once, " +
        "and may read uncommitted state. An integration event consumer runs out of band, after commit, " +
        "at least once, in its own scope. A type that does both is written against whichever assumption " +
        "its author had in mind at the time, and the other half is wrong.");

    // ── Messaging ────────────────────────────────────────────────────────

    public static readonly DiagnosticDescriptor EventIsBothKinds = Rule(
        DiagnosticIds.EventIsBothKinds,
        "A type must not be both a domain event and an integration event",
        "'{0}' implements both IDomainEvent and IIntegrationEvent",
        Messaging,
        DiagnosticSeverity.Error,
        "B1.2 separated the two hierarchies so that dispatching a published contract through the domain " +
        "pipeline is a compile error rather than a silent bypass of the outbox. A type implementing both " +
        "restores the defect wholesale: the compiler would accept every route the phase closed.");

    public static readonly DiagnosticDescriptor PublicationBypassesOutbox = Rule(
        DiagnosticIds.PublicationBypassesOutbox,
        "An integration event must be published through IIntegrationEventPublisher",
        "'{0}' reaches the transport directly. Publish through IIntegrationEventPublisher so the message " +
        "joins the producer's transaction.",
        Messaging,
        DiagnosticSeverity.Warning,
        "The publisher's only implementation writes an outbox row inside the caller's transaction, which " +
        "is what makes 'the fact was recorded and the fact was announced' one atomic decision. Reaching " +
        "the outbox store, the transport or the mediator directly announces facts that may never have " +
        "committed, and loses the ones that did when the process dies.");

    public static readonly DiagnosticDescriptor NotificationCreatedOutsideCommunication = Rule(
        DiagnosticIds.NotificationCreatedOutsideCommunication,
        "Only the Communication Platform creates notifications",
        "'{0}' creates a notification. Publish an integration event instead and let B13 decide whether, " +
        "how and to whom it is delivered.",
        Messaging,
        DiagnosticSeverity.Warning,
        "A business module that sends its own notification has taken on the member's channel preferences, " +
        "quiet hours, digest batching, localisation and delivery retries — none of which it can see. B13 " +
        "owns all of it, and it learns what happened the same way every other consumer does: from a " +
        "published contract.");

    // ── Persistence ──────────────────────────────────────────────────────

    public static readonly DiagnosticDescriptor RevisionIsNotAConcurrencyToken = Rule(
        DiagnosticIds.RevisionIsNotAConcurrencyToken,
        "A Revision property must be configured as a concurrency token",
        "'{0}' maps Revision without IsConcurrencyToken(). Two writers will then overwrite each other in " +
        "silence.",
        Persistence,
        DiagnosticSeverity.Warning,
        "Revision exists to make a lost update loud. Mapped as an ordinary column it becomes a number that " +
        "increments and protects nothing: the second writer's UPDATE succeeds, matches one row, and " +
        "discards the first writer's change with no error anywhere.");

    public static readonly DiagnosticDescriptor OwnedCollectionHasNoKey = Rule(
        DiagnosticIds.OwnedCollectionHasNoKey,
        "An owned collection must declare its key",
        "OwnsMany('{0}') declares no key. EF invents a store-generated shadow integer inside a composite " +
        "primary key, and the insert fails once a row exists.",
        Persistence,
        DiagnosticSeverity.Warning,
        "Left to convention, an owned collection gets a shadow key EF expects the store to generate — " +
        "inside a composite it cannot generate. The failure arrives on the second write, not the first, " +
        "which is why it survives every test that starts from an empty table.");

    public static readonly DiagnosticDescriptor IdentifierComparedInQuery = Rule(
        DiagnosticIds.IdentifierComparedInQuery,
        "A strongly typed identifier must not be stringified or ordered inside a query",
        "'{0}' cannot be translated to SQL. Exclude identifiers by set membership — see CursorBoundary.",
        Persistence,
        DiagnosticSeverity.Warning,
        "Identifiers here are value-converted records, and no provider translates ToString(), CompareTo(), " +
        "string.Compare() or an ordering comparison on EF.Property<Guid>() of one. Each shape compiles, " +
        "passes a suite that reads before it writes, and fails the first time a second page is asked for. " +
        "B10 shipped it in five repositories at once; the EF.Property shape shipped later, in the two the " +
        "rule could not see, and answered 'Object must implement IConvertible'.");

    // ── CQRS ─────────────────────────────────────────────────────────────

    public static readonly DiagnosticDescriptor QueryHandlerWrites = Rule(
        DiagnosticIds.QueryHandlerWrites,
        "A query handler must not write",
        "Query handler '{0}' saves changes. Move the write to a command, or declare the side effect with " +
        "[SideEffecting].",
        Cqrs,
        DiagnosticSeverity.Warning,
        "A query is meant to be safe to retry, to cache, to run against a replica and to call from a GET. " +
        "A handler that writes is none of those, and nothing in its name says so. Read-path counters are " +
        "the legitimate exception and [SideEffecting] is how they say it out loud.");

    public static readonly DiagnosticDescriptor SequentialSavesWithoutTransaction = Rule(
        DiagnosticIds.SequentialSavesWithoutTransaction,
        "A handler that saves twice in a row must declare [Transactional] on its command",
        "'{0}' calls SaveChangesAsync twice in the same block, and '{1}' is not [Transactional]. A failure " +
        "between them leaves half the operation committed.",
        Cqrs,
        DiagnosticSeverity.Warning,
        "One save is already atomic — EF wraps it. Two sequential saves are two transactions, and the " +
        "window between them is a real state the system can be found in. [Transactional] joins them, and " +
        "its Reason records why the handler needed two in the first place.");

    public static readonly DiagnosticDescriptor TransactionalOnHandler = Rule(
        DiagnosticIds.TransactionalOnHandler,
        "[Transactional] belongs on the command, not on the handler",
        "'{0}' is marked [Transactional], which does nothing here. Move it to the command type.",
        Cqrs,
        DiagnosticSeverity.Warning,
        "TransactionBehavior reads the attribute off TRequest — the command — because that is the type " +
        "MediatR closes the pipeline over. On a handler the attribute compiles, reads as a guarantee and " +
        "opens nothing, which is the exact failure B2's logging-only transaction behaviour already cost " +
        "this platform once.");

    public static readonly DiagnosticDescriptor RequestDeclaresNoAuthorization = Rule(
        DiagnosticIds.RequestDeclaresNoAuthorization,
        "A request must declare what it requires of its caller",
        "'{0}' declares no authorization requirement. Add [RequirePermission], [RequirePolicy], "
            + "[RequireAuthentication] or [AllowAnonymous].",
        Cqrs,
        DiagnosticSeverity.Warning,
        "AuthorizationBehavior refuses a request that declares nothing, so this is caught at runtime "
            + "either way -- but a 401 on a route somebody has already shipped is a worse place to "
            + "learn it than the editor. Before BSEC-003 the behaviour returned next() instead, and "
            + "247 request types took that path; one route was reachable by anonymous callers for as "
            + "long as it existed. [AllowAnonymous] carries a required reason precisely so that the "
            + "public case is a decision somebody wrote down rather than an attribute nobody added.");

    public static readonly DiagnosticDescriptor CommandHandlerNeverSaves = Rule(
        DiagnosticIds.CommandHandlerNeverSaves,
        "A command handler that stages a write must save it",
        "Command handler '{0}' stages a repository write but never saves. The command returns success and " +
        "changes nothing.",
        Cqrs,
        DiagnosticSeverity.Warning,
        "Add, Update and Remove only mark the change tracker. Without a save the handler returns a " +
        "successful Result, the API answers 201, and the row was never written — which is the failure mode " +
        "that survives an integration test asserting the response.");

    // ── Contributor model ────────────────────────────────────────────────

    public static readonly DiagnosticDescriptor FeatureGateName = Rule(
        DiagnosticIds.FeatureGateName,
        "A feature gate name must be module.capability",
        "Feature gate '{0}' is not in module.capability form",
        Contributors,
        DiagnosticSeverity.Warning,
        "Gate names are a client-facing vocabulary: the frontend switches on them and the platform " +
        "attributes conflicts by their first segment. Lower case, dot separated, at least two segments, " +
        "first segment the owning module.");

    public static readonly DiagnosticDescriptor PermissionKeyName = Rule(
        DiagnosticIds.PermissionKeyName,
        "A permission key must be module:resource:action",
        "Permission '{0}' is not in module:resource:action form",
        Contributors,
        DiagnosticSeverity.Warning,
        "Colons for permissions, dots for policies. The two vocabularies look different on purpose so that " +
        "a policy name and the permission behind it can never be confused in an attribute, a log line or a " +
        "token claim.");

    public static readonly DiagnosticDescriptor PolicyName = Rule(
        DiagnosticIds.PolicyName,
        "A policy name must be dotted and namespaced to its module",
        "Policy '{0}' is not in module.resource.action form",
        Contributors,
        DiagnosticSeverity.Warning,
        "A policy name is what an endpoint attribute carries. Dotted, lower case, first segment the owning " +
        "module — so that two modules cannot collide and a reader can tell at a glance which module refuses " +
        "the request.");

    public static readonly DiagnosticDescriptor PermissionVocabulary = Rule(
        DiagnosticIds.PermissionVocabulary,
        "Prefer the platform's permission vocabulary",
        "Permission '{0}' uses the action '{1}'. The platform's word for this is '{2}'.",
        Contributors,
        DiagnosticSeverity.Info,
        "'view', 'browse', 'feed', 'list' and 'get' all mean read, and a catalogue that uses five words for " +
        "one idea cannot be reasoned about by anyone who did not write it. A suggestion rather than a " +
        "warning because permission strings are already held by shipped clients: new keys should follow the " +
        "vocabulary, existing ones change at a major version.");

    public static readonly DiagnosticDescriptor CatalogueNameCasing = Rule(
        DiagnosticIds.CatalogueNameCasing,
        "Catalogue names are lower case",
        "'{0}' is not lower case. The platform's permissions, gates and policies are lower case throughout.",
        Contributors,
        DiagnosticSeverity.Info,
        "A catalogue somebody has to remember the casing of is a catalogue that gets a lookup wrong at " +
        "three in the morning. A suggestion rather than a warning because two modules already ship " +
        "PascalCase policy names and renaming them is a change to authorisation wiring, not to a comment " +
        "— it belongs in a release that says so.");

    public static readonly DiagnosticDescriptor ContributorShape = Rule(
        DiagnosticIds.ContributorShape,
        "A contributor must be sealed and constructible without the container",
        "Contributor '{0}' {1}",
        Contributors,
        DiagnosticSeverity.Warning,
        "Contributors are instantiated by the kernel while the container is still being assembled, so a " +
        "constructor dependency cannot be satisfied and fails the boot. Sealed because a contributor is a " +
        "declaration, not an extension point — the extension point is the interface.");

    // ── AI readiness ─────────────────────────────────────────────────────

    public static readonly DiagnosticDescriptor AiVendorInBusinessModule = Rule(
        DiagnosticIds.AiVendorInBusinessModule,
        "A business module must not name an AI vendor",
        "'{0}' names {1}. AI enters through a platform abstraction and a swappable adapter, never through a " +
        "module.",
        Readiness,
        DiagnosticSeverity.Warning,
        "The platform's ranking, similarity, candidate-source and search seams already have the shape an " +
        "AI implementation needs. A vendor named inside a vertical makes that vertical undeployable without " +
        "an API key, unrunnable in tests without a network, and impossible to swap without touching " +
        "business code.");

    private static DiagnosticDescriptor Rule(
        string id,
        string title,
        string messageFormat,
        string category,
        DiagnosticSeverity severity,
        string description) =>
        new(
            id,
            title,
            messageFormat,
            category,
            severity,
            isEnabledByDefault: true,
            description: description,
            helpLinkUri: CatalogUrl);
}
