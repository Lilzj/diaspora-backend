namespace Diaspora.Platform.Operations.Policies;

/// <summary>
/// The policy names the platform itself ships defaults for.
/// <para>
/// Constants rather than literals because a policy is resolved by name and a typo resolves to the
/// caller's fallback — silently, correctly-looking, and with the deployment's tuning ignored. That
/// failure mode has no error message anywhere, which is exactly the kind this platform has learned
/// to design out rather than to document.
/// </para>
/// <para>
/// A module may name a policy that is not here. These are the platform's, not the world's.
/// </para>
/// </summary>
public static class OperationalPolicyNames
{
    /// <summary>Retry policies.</summary>
    public static class Retry
    {
        /// <summary>The fallback every unnamed retry uses.</summary>
        public const string Default = "platform.default";

        /// <summary>The outbox dispatcher's own backoff. Persisted across restarts, so unusually patient.</summary>
        public const string OutboxDispatch = "outbox.dispatch";

        /// <summary>Any call leaving the process to a third party.</summary>
        public const string ExternalProvider = "external.provider";

        /// <summary>Transient database faults — deadlock, connection reset, failover.</summary>
        public const string DatabaseTransient = "database.transient";

        /// <summary>The shared store. Short and few: a cache retry that outlasts the read it saves is a loss.</summary>
        public const string DistributedCache = "cache.distributed";
    }

    /// <summary>Circuit breakers, one per class of dependency the platform does not control.</summary>
    public static class CircuitBreaker
    {
        public const string PaymentProvider = "financial.payment-provider";
        public const string MediaProvider = "media.provider";
        public const string NotificationProvider = "notifications.provider";
        public const string SearchProvider = "search.provider";
        public const string ExternalApi = "external.api";
        public const string DistributedCache = "cache.distributed";

        /// <summary>
        /// Declared, unused, and deliberately so. B16 introduces AI providers; the breaker that
        /// protects the platform from one is an operational concern that belongs here rather than
        /// in the module that first calls one.
        /// </summary>
        public const string AiProvider = "ai.provider";
    }

    /// <summary>Rate limits.</summary>
    public static class RateLimit
    {
        public const string GlobalApi = "api.global";
        public const string AuthenticatedApi = "api.authenticated";
        public const string AnonymousApi = "api.anonymous";
        public const string MediaDownload = "media.download";
        public const string MediaUpload = "media.upload";
        public const string StaffApi = "api.staff";
    }

    /// <summary>Cache policies.</summary>
    public static class Cache
    {
        public const string Default = "platform.default";

        /// <summary>Reference data: countries, categories, currencies. Rarely changes, read constantly.</summary>
        public const string ReferenceData = "platform.reference-data";

        public const string PersonalizedFeed = "recommendations.personalized-feed";
        public const string CountryFeed = "recommendations.country-feed";
        public const string Trending = "recommendations.trending";
        public const string ProviderDirectory = "providers.directory";
        public const string GuideCatalog = "guides.catalog";
        public const string FinancialCatalog = "financial.catalog";
        public const string CountryIntelligence = "knowledge.country-intelligence";
        public const string FeatureFlags = "platform.feature-flags";
    }

    /// <summary>Health policies, one per health dimension the platform reports on.</summary>
    public static class Health
    {
        public const string Default = "platform.default";
        public const string Storage = "platform.storage";
        public const string Messaging = "platform.messaging";
        public const string Cache = "platform.cache";
        public const string Search = "platform.search";
        public const string ExternalProvider = "platform.external-provider";
        public const string BackgroundWorker = "platform.background-worker";
    }

    /// <summary>Queue policies.</summary>
    public static class Queue
    {
        public const string Default = "platform.default";
        public const string OutboxDispatch = "outbox.dispatch";
        public const string NotificationDelivery = "notifications.delivery";
        public const string RecommendationRefresh = "recommendations.refresh";
        public const string MediaProcessing = "media.processing";
        public const string SearchIndexing = "search.indexing";
    }

    /// <summary>Recovery policies. One per class of data with a distinct tolerance for loss.</summary>
    public static class Recovery
    {
        public const string Default = "platform.default";

        /// <summary>The ledger. The only data on the platform whose acceptable loss is zero.</summary>
        public const string FinancialLedger = "financial.ledger";

        public const string Identity = "identity";
        public const string Media = "media";
        public const string Notifications = "notifications";
        public const string Recommendations = "recommendations";
        public const string Telemetry = "platform.telemetry";
    }

    /// <summary>Feature-flag policies.</summary>
    public static class FeatureFlag
    {
        public const string Default = "platform.default";

        /// <summary>Flags guarding safety controls. The kill switch does not reach these.</summary>
        public const string SafetyControl = "platform.safety-control";
    }
}
