using Diaspora.Platform.Operations.Policies;

namespace Diaspora.Operations.Policies;

/// <summary>
/// The platform's default retry, breaker, limit and queue policies.
/// <para>
/// Every number here is a default a deployment is expected to override, and every one of them is
/// justified in the [Resilience Guide](../../docs/platform/b17-resilience-guide.md). They are
/// version 1; a deployment that disagrees ships version 2 of the same name and the catalogue
/// prefers it without this file changing.
/// </para>
/// <para>
/// <b>These are not tuned for a load nobody has measured.</b> B17 establishes the baselines
/// (Part 13) and these numbers are the starting point that produced them. A default's job is to be
/// safe and legible, not optimal — an optimal default that nobody understands is a default nobody
/// will change when the load does.
/// </para>
/// </summary>
public sealed class PlatformResiliencePolicyContributor : IOperationalPolicyContributor
{
    public string Module => "Platform";

    public IEnumerable<OperationalPolicy> Policies =>
    [
        // ─── Retry ────────────────────────────────────────────────────────────────────────
        new RetryPolicy(OperationalPolicyNames.Retry.Default)
        {
            Description = "The fallback for any operation that does not name its own retry policy.",
            MaxAttempts = 3,
            InitialDelay = TimeSpan.FromMilliseconds(200),
            MaxDelay = TimeSpan.FromSeconds(5),
            OverallTimeout = TimeSpan.FromSeconds(15)
        },

        new RetryPolicy(OperationalPolicyNames.Retry.OutboxDispatch)
        {
            // Deliberately the most patient policy on the platform. The outbox's retry survives a
            // process restart, so an hour-long backoff costs nothing but a delayed event; giving up
            // costs a published fact nobody downstream ever hears.
            Description = "Outbox delivery. Patient, because the retry survives restarts.",
            MaxAttempts = 8,
            InitialDelay = TimeSpan.FromSeconds(5),
            MaxDelay = TimeSpan.FromMinutes(30),
            JitterRatio = 0.25,
            PoisonThreshold = 10
        },

        new RetryPolicy(OperationalPolicyNames.Retry.ExternalProvider)
        {
            Description = "Any call leaving the process to a third party.",
            MaxAttempts = 3,
            InitialDelay = TimeSpan.FromMilliseconds(500),
            MaxDelay = TimeSpan.FromSeconds(8),
            JitterRatio = 0.3,
            OverallTimeout = TimeSpan.FromSeconds(20)
        },

        // ─── The zero-retry policies every governed HTTP client must have ─────────────────
        //
        // Zero, and it is the most important number in this file.
        //
        // ResilientHttpHandler sends one HttpRequestMessage. The executor's retry re-invokes the
        // operation, which here re-sends that same message — a request whose content has been
        // consumed is not resendable, so the second attempt fails for a reason that has nothing to
        // do with the provider. And the absence of a policy is not neutral: RetryFor falls back to
        // Retry.Default, which is three attempts, so a client routed through the handler without a
        // line here would quietly gain two retries on every call. For a payment that is the
        // duplicate charge the breaker below exists to prevent.
        //
        // Named for the dependency rather than sitting under Retry.*, because the executor resolves
        // both policies from the one dependency name, and a retry policy filed somewhere else is a
        // retry policy the executor never finds.
        //
        // Retry is not forbidden here, only automatic retry at the transport. A caller that knows
        // an operation is idempotent — a status read, a webhook replay — retries above this layer
        // where it can build a fresh request and decide. The handler refuses to be constructed for
        // a dependency missing one of these, and an architecture rule asserts the same thing
        // against the composed catalogue at build time.

        new RetryPolicy(OperationalPolicyNames.CircuitBreaker.StripePayments)
        {
            Description = "Stripe. Never retried at the transport: a charge is not idempotent.",
            MaxAttempts = 0,
            OverallTimeout = TimeSpan.FromSeconds(25)
        },

        new RetryPolicy(OperationalPolicyNames.CircuitBreaker.PayPalPayments)
        {
            Description = "PayPal. Never retried at the transport: a charge is not idempotent.",
            MaxAttempts = 0,
            OverallTimeout = TimeSpan.FromSeconds(25)
        },

        new RetryPolicy(OperationalPolicyNames.CircuitBreaker.FlutterwavePayments)
        {
            Description = "Flutterwave. Never retried at the transport: a charge is not idempotent.",
            MaxAttempts = 0,
            OverallTimeout = TimeSpan.FromSeconds(25)
        },

        new RetryPolicy(OperationalPolicyNames.CircuitBreaker.FirebasePush)
        {
            // A resent FCM message is a second notification on somebody's lock screen. The channel
            // above decides whether a failed send is worth repeating, and it has the device token
            // and the dead-token rules to decide with.
            Description = "Firebase Cloud Messaging. A resent push is a duplicate notification.",
            MaxAttempts = 0,
            OverallTimeout = TimeSpan.FromSeconds(20)
        },

        // The four below name their client rather than a constant, because for these the client and
        // the dependency are the same thing and the dashboard should say so. The literal is held to
        // the constant by OutboundResilienceArchitectureTests, which resolves every governed
        // client's dependency name and fails when one has no policy here.

        new RetryPolicy("maps.google.routes")
        {
            // A GET, so a retry would be safe — and it would also spend a second unit of the
            // monthly Routes allowance that platform.provider_usage meters before the call. The
            // meter is above this layer and would not see the second call, so the allowance and
            // the bill would disagree.
            Description = "Google Routes. Metered above the transport, so a transport retry is unbilled.",
            MaxAttempts = 0,
            OverallTimeout = TimeSpan.FromSeconds(20)
        },

        new RetryPolicy("maps.google.places")
        {
            Description = "Google Places. Metered above the transport, as Routes is.",
            MaxAttempts = 0,
            OverallTimeout = TimeSpan.FromSeconds(20)
        },

        new RetryPolicy("police-uk")
        {
            Description = "Police UK crime data. A background refresh; the next run is the retry.",
            MaxAttempts = 0,
            OverallTimeout = TimeSpan.FromSeconds(30)
        },

        new RetryPolicy("trust-decisions")
        {
            // Outbound to somebody else's webhook endpoint. Whether a decision may be delivered
            // twice is their invariant, not this platform's, so the transport does not decide it.
            Description = "Trust decision webhooks. Redelivery is the receiver's invariant.",
            MaxAttempts = 0,
            OverallTimeout = TimeSpan.FromSeconds(20)
        },

        new RetryPolicy(OperationalPolicyNames.Retry.DatabaseTransient)
        {
            // Matches what the EF execution strategy already does, expressed here so the number is
            // visible on the dashboard beside every other retry rather than buried in a provider
            // configuration call.
            Description = "Transient database faults: deadlock, connection reset, failover.",
            MaxAttempts = 5,
            InitialDelay = TimeSpan.FromMilliseconds(100),
            MaxDelay = TimeSpan.FromSeconds(5),
            JitterRatio = 0.2
        },

        new RetryPolicy(OperationalPolicyNames.Retry.DistributedCache)
        {
            // One retry, 50ms. A cache read exists to be faster than the source; a retry budget
            // that exceeds the cost of the query it avoids has made the cache a liability.
            Description = "The shared cache. One quick retry, then fall through to the source.",
            MaxAttempts = 1,
            InitialDelay = TimeSpan.FromMilliseconds(50),
            MaxDelay = TimeSpan.FromMilliseconds(100),
            OverallTimeout = TimeSpan.FromMilliseconds(250)
        },

        // ─── Circuit breakers ─────────────────────────────────────────────────────────────
        //
        // The three payment breakers are tuned identically and are three policies rather than one,
        // because the executor keys breaker STATE by dependency name. Under one name, Stripe's
        // failures would open the circuit on PayPal and Flutterwave — and the registry picks a
        // gateway by currency and country, so a degraded African corridor would refuse a European
        // card that was never going to touch it. Splitting them costs sensitivity: each breaker now
        // needs MinimumThroughput calls of its own inside the sampling window before it can trip,
        // so a low-volume provider is slower to break. That is the right way round. A breaker that
        // trips late protects the pool a little later; a breaker shared between alternatives turns
        // one provider's outage into an outage of the whole payment surface.

        new CircuitBreakerPolicy(OperationalPolicyNames.CircuitBreaker.StripePayments)
        {
            // The strictest breaker on the platform, and the longest break. A payment gateway that
            // is failing must not be hammered: duplicate charges are created by retries against a
            // provider that is timing out after it has already taken the money.
            Description = "Stripe. Strictest breaker on the platform.",
            FailureRatio = 0.3,
            SamplingDuration = TimeSpan.FromSeconds(60),
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromSeconds(60),
            CallTimeout = TimeSpan.FromSeconds(20)
        },

        new CircuitBreakerPolicy(OperationalPolicyNames.CircuitBreaker.PayPalPayments)
        {
            Description = "PayPal. Tuned as Stripe's; separate so one provider's outage is one.",
            FailureRatio = 0.3,
            SamplingDuration = TimeSpan.FromSeconds(60),
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromSeconds(60),
            CallTimeout = TimeSpan.FromSeconds(20)
        },

        new CircuitBreakerPolicy(OperationalPolicyNames.CircuitBreaker.FlutterwavePayments)
        {
            Description = "Flutterwave. Tuned as Stripe's; separate so one provider's outage is one.",
            FailureRatio = 0.3,
            SamplingDuration = TimeSpan.FromSeconds(60),
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromSeconds(60),
            CallTimeout = TimeSpan.FromSeconds(20)
        },

        new CircuitBreakerPolicy(OperationalPolicyNames.CircuitBreaker.MediaProvider)
        {
            Description = "Object storage and media processing providers.",
            FailureRatio = 0.5,
            BreakDuration = TimeSpan.FromSeconds(20),
            CallTimeout = TimeSpan.FromSeconds(30)
        },

        new CircuitBreakerPolicy(OperationalPolicyNames.CircuitBreaker.FirebasePush)
        {
            // This used to be notifications.provider and covered "email, SMS and push" in its
            // description alone: email and SMS are governed at their call sites under per-provider
            // names of their own, so the only transport it could ever have applied to was push,
            // and push was governed by nothing at all. Named for what it governs.
            Description = "Firebase Cloud Messaging.",
            FailureRatio = 0.5,
            BreakDuration = TimeSpan.FromSeconds(30),
            CallTimeout = TimeSpan.FromSeconds(15)
        },

        new CircuitBreakerPolicy(OperationalPolicyNames.CircuitBreaker.SearchProvider)
        {
            // Tolerant, because the platform ships a database-backed search engine behind every
            // search port. A broken search provider degrades to a slower query, not to an error.
            Description = "Search providers. Tolerant: search degrades to the database engine.",
            FailureRatio = 0.6,
            BreakDuration = TimeSpan.FromSeconds(15),
            CallTimeout = TimeSpan.FromSeconds(5)
        },

        new CircuitBreakerPolicy(OperationalPolicyNames.CircuitBreaker.ExternalApi)
        {
            Description = "Any other external HTTP dependency.",
            FailureRatio = 0.5,
            BreakDuration = TimeSpan.FromSeconds(15)
        },

        new CircuitBreakerPolicy(OperationalPolicyNames.CircuitBreaker.DistributedCache)
        {
            // Opens fast and stays open briefly. A cache that is down should be skipped within a
            // handful of requests, not discovered afresh by every one of them.
            Description = "The shared cache. Opens fast; a down cache should be skipped, not retried.",
            FailureRatio = 0.4,
            SamplingDuration = TimeSpan.FromSeconds(10),
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromSeconds(10),
            CallTimeout = TimeSpan.FromMilliseconds(250)
        },

        new CircuitBreakerPolicy(OperationalPolicyNames.CircuitBreaker.AiProvider)
        {
            // B16's, declared in B17. An AI provider is slow by nature, so the call timeout is
            // long and the breaker leans on the ratio rather than on latency.
            Description = "Reserved for B16. AI inference providers: slow by nature, so ratio-driven.",
            FailureRatio = 0.4,
            SamplingDuration = TimeSpan.FromSeconds(120),
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromSeconds(45),
            CallTimeout = TimeSpan.FromSeconds(60)
        },

        // ─── Rate limits ──────────────────────────────────────────────────────────────────
        new RateLimitPolicy(OperationalPolicyNames.RateLimit.GlobalApi)
        {
            Description = "The deployment-wide ceiling. The last line before the queues overrun.",
            Dimension = RateLimitDimension.Global,
            Algorithm = RateLimitAlgorithm.SlidingWindow,
            PermitLimit = 20_000,
            Window = TimeSpan.FromMinutes(1)
        },

        new RateLimitPolicy(OperationalPolicyNames.RateLimit.AuthenticatedApi)
        {
            // Token bucket with a burst, because a mobile client returning to the foreground
            // legitimately fires a screen's worth of requests at once and is not an attacker.
            Description = "Per member. Token bucket, because a foregrounding app bursts legitimately.",
            Dimension = RateLimitDimension.User,
            Algorithm = RateLimitAlgorithm.TokenBucket,
            PermitLimit = 300,
            BurstCapacity = 60,
            Window = TimeSpan.FromMinutes(1)
        },

        new RateLimitPolicy(OperationalPolicyNames.RateLimit.AnonymousApi)
        {
            Description = "Per client address, unauthenticated. Refused before a token is validated.",
            Dimension = RateLimitDimension.ClientAddress,
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1)
        },

        new RateLimitPolicy(OperationalPolicyNames.RateLimit.StaffApi)
        {
            // A separate budget so a staff tool sweeping a moderation queue cannot consume the
            // member budget, and so a compromised staff token is bounded independently.
            Description = "Staff tooling. A separate budget from member traffic, in both directions.",
            Dimension = RateLimitDimension.Role,
            PermitLimit = 1_200,
            Window = TimeSpan.FromMinutes(1)
        },

        new RateLimitPolicy(OperationalPolicyNames.RateLimit.MediaUpload)
        {
            Description = "Uploads per member. Concurrency, not rate: the cost is bandwidth in flight.",
            Dimension = RateLimitDimension.User,
            Algorithm = RateLimitAlgorithm.Concurrency,
            PermitLimit = 3
        },

        new RateLimitPolicy(OperationalPolicyNames.RateLimit.MediaDownload)
        {
            // DEBT-019's second half. Declared here so the limit exists as a policy the media
            // pipeline can enforce; wiring it to the download endpoint is Media's next visit.
            Description = "Downloads per member. Closes the policy half of DEBT-019.",
            Dimension = RateLimitDimension.User,
            PermitLimit = 240,
            Window = TimeSpan.FromMinutes(1)
        },

        // ─── Queues ───────────────────────────────────────────────────────────────────────
        new QueuePolicy(OperationalPolicyNames.Queue.Default)
        {
            Description = "Any queue that does not name its own policy.",
            Concurrency = 1,
            MaxConcurrency = 4,
            BatchSize = 50,
            DepthWarningThreshold = 1_000,
            DepthCriticalThreshold = 10_000,
            MaxOldestAge = TimeSpan.FromMinutes(15)
        },

        new QueuePolicy(OperationalPolicyNames.Queue.OutboxDispatch)
        {
            Description = "The outbox. Adaptive, because its depth is bursty by construction.",
            Concurrency = 1,
            MaxConcurrency = 4,
            BatchSize = 100,
            AdaptiveThroughput = true,
            DepthWarningThreshold = 500,
            DepthCriticalThreshold = 5_000,
            MaxOldestAge = TimeSpan.FromMinutes(5),
            ConsumerGroup = "outbox"
        },

        new QueuePolicy(OperationalPolicyNames.Queue.NotificationDelivery)
        {
            Description = "Notification delivery. A delayed notification is worth less each minute.",
            Concurrency = 2,
            MaxConcurrency = 8,
            BatchSize = 50,
            AdaptiveThroughput = true,
            DepthWarningThreshold = 2_000,
            DepthCriticalThreshold = 20_000,
            MaxOldestAge = TimeSpan.FromMinutes(10),
            ConsumerGroup = "notifications"
        },

        new QueuePolicy(OperationalPolicyNames.Queue.RecommendationRefresh)
        {
            // The one queue where shedding is defensible: a dropped refresh means a feed is
            // computed from slightly older signals, and the next refresh corrects it.
            Description = "Feed refresh. The one queue where shedding the newest work is defensible.",
            Concurrency = 1,
            MaxConcurrency = 4,
            BatchSize = 200,
            Backpressure = BackpressureMode.ShedNewest,
            DepthWarningThreshold = 5_000,
            DepthCriticalThreshold = 50_000,
            MaxOldestAge = TimeSpan.FromHours(2),
            ConsumerGroup = "recommendations"
        },

        new QueuePolicy(OperationalPolicyNames.Queue.MediaProcessing)
        {
            Description = "Media processing. Bounded hard: each item costs CPU and disk, not a row.",
            Concurrency = 2,
            MaxConcurrency = 4,
            BatchSize = 10,
            Backpressure = BackpressureMode.RejectEnqueue,
            DepthWarningThreshold = 200,
            DepthCriticalThreshold = 2_000,
            MaxOldestAge = TimeSpan.FromMinutes(30),
            ConsumerGroup = "media"
        },

        new QueuePolicy(OperationalPolicyNames.Queue.SearchIndexing)
        {
            Description = "Background search indexing. Lag is acceptable; loss is not.",
            Concurrency = 1,
            MaxConcurrency = 4,
            BatchSize = 250,
            AdaptiveThroughput = true,
            DepthWarningThreshold = 10_000,
            DepthCriticalThreshold = 100_000,
            MaxOldestAge = TimeSpan.FromHours(1),
            ConsumerGroup = "search"
        }
    ];
}
