using System.Globalization;
using System.Threading.RateLimiting;
using Diaspora.Platform.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Diaspora.Api.RateLimiting;

/// <summary>
/// Per-IP throttling, on every endpoint this host composes.
/// <para>
/// B2 had none. Its only defences were a per-account lockout and a per-user OTP budget,
/// which between them stop one attacker guessing one password and nothing else: password
/// spraying at one attempt per account never trips a lockout, and an unregistered phone
/// number has no account to charge an OTP budget to. This closes the transport half of that
/// gap; <c>ISubjectRateLimiter</c>, applied inside the handlers, closes the other.
/// </para>
/// <para>
/// <b>This summary used to begin "the unauthenticated attack surface", and that sentence was
/// quoted back as the root cause of BSEC-002.</b> It was accurate about intent and wrong about
/// consequence: rate limiting was built for credential exchange, four module route groups out of
/// twenty-one were later pointed at the authenticated budget by hand, and roughly nine hundred
/// endpoints were left with no bound at all — every financial endpoint, every media upload, both
/// payment webhooks, all anonymous discovery and search, and ten of the host's twelve
/// controllers. Nothing was broken; nobody had been asked. Coverage is now applied where
/// endpoints are composed rather than module by module, so an endpoint is throttled by omission
/// — see <c>RateLimitCoverage</c>, and <c>RateLimitCoverageTests</c> for the rule that keeps it
/// that way.
/// </para>
/// <para>
/// In-memory partitions, which means each instance enforces the limit independently: behind three
/// replicas the effective rate is three times the configured one. That is no longer a quiet
/// weakening — <c>DeploymentTopologyValidator</c> refuses to start a host that declares more than
/// one instance, naming this limiter as the reason (SV8003) — and the swap to a distributed store
/// is a limiter registration rather than a redesign.
/// </para>
/// </summary>
public static class RateLimitingRegistration
{
    // Aliases. The names themselves live in Diaspora.Platform, where a module can see them —
    // see RateLimitPolicyNames for why that move was not cosmetic. These stay because the
    // identity surface names its budgets through attributes on controller actions, and an
    // attribute argument must be a constant the host can reach.
    public const string SignInPolicy = RateLimitPolicyNames.SignIn;
    public const string RegistrationPolicy = RateLimitPolicyNames.Registration;
    public const string OneTimeCodePolicy = RateLimitPolicyNames.OneTimeCode;
    public const string RecoveryPolicy = RateLimitPolicyNames.Recovery;
    public const string RefreshPolicy = RateLimitPolicyNames.Refresh;

    /// <summary>
    /// The budget for authenticated application traffic, and the reason it exists.
    /// <para>
    /// Three module groups — Trust, Conversations and Bookmarks — were mapped with
    /// <c>RequireRateLimiting(SignInPolicy)</c>, one of them commented "reuse the
    /// authenticated default bucket". There was no such bucket. <see cref="SignInPolicy"/>
    /// is credential exchange: ten requests a minute, per IP, and the partition key is the
    /// policy name — so <c>/auth/login</c>, <c>/trust/profile</c>,
    /// <c>/conversations</c> and <c>/bookmarks</c> all drew on one ten-request budget.
    /// </para>
    /// <para>
    /// Measured, not inferred: the conversation list answered eight reads and then 429ed,
    /// and <c>/bookmarks</c> was refused on its <em>first</em> request because the
    /// conversation reads had already spent the shared budget. A chat screen and the
    /// eligibility checklist that every create screen now fetches both exceed ten requests
    /// a minute in ordinary use, so this was a broken journey rather than a tight limit.
    /// </para>
    /// <para>
    /// Sized for an application rather than for a credential guess, and still a bound: an
    /// authenticated client that issues three hundred requests in a minute is not a screen.
    /// It stays per-IP, because the limiter runs <em>before</em> authentication on purpose —
    /// refusing a flood must not cost a signature validation and two database reads — and
    /// <c>HttpContext.User</c> is therefore empty here. That makes the forwarded-header
    /// configuration load-bearing rather than optional: with an empty
    /// <c>ForwardedHeaders:KnownProxies</c> behind an ingress, every member shares the
    /// proxy's address and therefore this budget.
    /// </para>
    /// </summary>
    public const string AuthenticatedPolicy = RateLimitPolicyNames.Authenticated;

    /// <summary>
    /// Every policy this host registers, which is what makes an endpoint's named policy
    /// checkable.
    /// <para>
    /// A name no policy answers to is not a compile error and not a startup error. ASP.NET Core
    /// raises it on the request that reaches the endpoint — so a typo ships, and the endpoint
    /// it was meant to protect is the one that fails. This list is here so a guardrail can ask
    /// the question at build time instead, in the same shape as the authorisation catalogue's
    /// <c>Every_policy_an_endpoint_names_is_declared_by_a_module</c>.
    /// </para>
    /// </summary>
    public static IReadOnlySet<string> Declared { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        RateLimitPolicyNames.SignIn,
        RateLimitPolicyNames.Registration,
        RateLimitPolicyNames.OneTimeCode,
        RateLimitPolicyNames.Recovery,
        RateLimitPolicyNames.Refresh,
        RateLimitPolicyNames.Authenticated,
        RateLimitPolicyNames.Anonymous,
        RateLimitPolicyNames.Search,
        RateLimitPolicyNames.Webhook,
        RateLimitPolicyNames.Upload
    };

    public static IServiceCollection AddDiasporaRateLimiting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.OnRejected = async (context, cancellationToken) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var window)
                    ? (int)window.TotalSeconds
                    : 60;

                context.HttpContext.Response.Headers.RetryAfter =
                    retryAfter.ToString(CultureInfo.InvariantCulture);

                // Same envelope as every other error the platform emits. A 429 that does not
                // look like a ProblemDetails is a 429 the client has to special-case.
                await PlatformProblem.WriteAsync(
                    context.HttpContext,
                    StatusCodes.Status429TooManyRequests,
                    PlatformErrorCodes.RateLimited,
                    "Too many requests",
                    "You have made too many requests. Please wait before trying again.",
                    extensions: new Dictionary<string, object?> { ["retryAfter"] = retryAfter },
                    cancellationToken);
            };

            AddFixedWindow(limiter, SignInPolicy, options => options.SignIn);
            AddFixedWindow(limiter, RegistrationPolicy, options => options.Registration);
            AddFixedWindow(limiter, OneTimeCodePolicy, options => options.OneTimeCode);
            AddFixedWindow(limiter, RecoveryPolicy, options => options.Recovery);
            AddFixedWindow(limiter, RefreshPolicy, options => options.Refresh);
            AddFixedWindow(limiter, AuthenticatedPolicy, options => options.Authenticated);

            // The two tiers every composed endpoint falls into, and the three deviations a route
            // may ask for. See RateLimitCoverage for how an endpoint acquires one of the tiers
            // without naming it.
            AddFixedWindow(limiter, RateLimitPolicyNames.Anonymous, options => options.Anonymous);
            AddFixedWindow(limiter, RateLimitPolicyNames.Search, options => options.Search);
            AddFixedWindow(limiter, RateLimitPolicyNames.Webhook, options => options.Webhook);
            AddFixedWindow(limiter, RateLimitPolicyNames.Upload, options => options.Upload);
        });

        return services;
    }

    private static void AddFixedWindow(
        RateLimiterOptions limiter,
        string policyName,
        Func<RateLimitingOptions, RateLimitPolicyOptions> select)
    {
        limiter.AddPolicy(policyName, context =>
        {
            var options = context.RequestServices
                .GetRequiredService<IOptionsMonitor<RateLimitingOptions>>()
                .CurrentValue;

            if (!options.Enabled)
            {
                return RateLimitPartition.GetNoLimiter("disabled");
            }

            var policy = select(options);

            return RateLimitPartition.GetSlidingWindowLimiter(
                PartitionKey(context, policyName),
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = policy.PermitLimit,
                    Window = policy.Window,
                    // Six segments turns the window from a cliff into a slope: without it,
                    // a fixed window permits a double-rate burst across its boundary.
                    SegmentsPerWindow = 6,
                    QueueLimit = policy.QueueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true
                });
        });
    }

    /// <summary>
    /// Partitions on the caller's address, scoped to the policy so a sign-in attempt and a
    /// recovery request do not share a budget.
    /// <para>
    /// Reads <c>Connection.RemoteIpAddress</c>, which is the real client only because the
    /// host runs forwarded-header processing ahead of this middleware. Without that this
    /// would partition every request behind a load balancer into one bucket and throttle the
    /// entire user base as though it were one client — which is why the two are wired
    /// together in <c>Program.cs</c>.
    /// </para>
    /// <para>
    /// The clause that used to close that sentence — that the forwarded-header configuration
    /// "refuses to trust an unbounded proxy list" — was false when it was written. Both
    /// address collections were empty, and empty makes ASP.NET Core trust <i>every</i> proxy
    /// rather than none, so this partition key was forgeable once per request and every budget
    /// below was decorative. It is true now, and <c>TrustedProxyOptionsValidator</c> is what
    /// makes it true: a deployed host that names no ingress does not start.
    /// </para>
    /// </summary>
    private static string PartitionKey(HttpContext context, string policyName)
    {
        var address = context.Connection.RemoteIpAddress;

        var client = address is null
            ? "unknown"
            : address.IsIPv4MappedToIPv6
                ? address.MapToIPv4().ToString()
                : address.ToString();

        return $"{policyName}:{client}";
    }
}
