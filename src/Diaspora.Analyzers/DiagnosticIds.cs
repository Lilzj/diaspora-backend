namespace Diaspora.Analyzers;

/// <summary>
/// Every diagnostic this pack can raise.
/// <para>
/// <b>The numbering is a filing system, not a severity.</b> The leading digit says which
/// guardrail family a rule belongs to, so a suppression in an <c>.editorconfig</c> reads as a
/// decision about an area rather than about a number: 1 domain events, 2 messaging,
/// 3 persistence, 4 CQRS, 5 the contributor model, 6 AI readiness.
/// </para>
/// <para>
/// Ids are permanent. A rule that stops being useful is removed from
/// <c>AnalyzerReleases.Shipped.md</c> and its id retired — never reused for something else,
/// because a suppression written years ago would then silence a rule nobody chose to silence.
/// </para>
/// </summary>
internal static class DiagnosticIds
{
    // ── Domain event safety ──────────────────────────────────────────────
    public const string HandlerDependsOnHandler = "DSP1001";
    public const string DuplicateDomainEventSettlement = "DSP1002";
    public const string DuplicateIntegrationEventSettlement = "DSP1003";
    public const string HandlerMixesPipelines = "DSP1004";

    // ── Messaging ────────────────────────────────────────────────────────
    public const string EventIsBothKinds = "DSP2001";
    public const string PublicationBypassesOutbox = "DSP2002";
    public const string NotificationCreatedOutsideCommunication = "DSP2003";

    // ── Persistence ──────────────────────────────────────────────────────
    public const string RevisionIsNotAConcurrencyToken = "DSP3001";
    public const string OwnedCollectionHasNoKey = "DSP3002";
    public const string IdentifierComparedInQuery = "DSP3003";

    // ── CQRS ─────────────────────────────────────────────────────────────
    public const string QueryHandlerWrites = "DSP4001";
    public const string SequentialSavesWithoutTransaction = "DSP4002";
    public const string CommandHandlerNeverSaves = "DSP4003";
    public const string TransactionalOnHandler = "DSP4004";
    public const string RequestDeclaresNoAuthorization = "DSP4005";

    // ── Contributor model ────────────────────────────────────────────────
    public const string FeatureGateName = "DSP5001";
    public const string PermissionKeyName = "DSP5002";
    public const string PolicyName = "DSP5003";
    public const string PermissionVocabulary = "DSP5004";
    public const string ContributorShape = "DSP5005";
    public const string CatalogueNameCasing = "DSP5006";

    // ── AI readiness ─────────────────────────────────────────────────────
    public const string AiVendorInBusinessModule = "DSP6001";
}
