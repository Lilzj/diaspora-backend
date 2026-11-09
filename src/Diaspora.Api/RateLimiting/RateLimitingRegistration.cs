using System.Globalization;
using System.Threading.RateLimiting;
using Diaspora.Platform.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Diaspora.Api.RateLimiting;

/// <summary>
/// Per-IP throttling on the unauthenticated attack surface.
/// <para>
/// B2 had none. Its only defences were a per-account lockout and a per-user OTP budget,
/// which between them stop one attacker guessing one password and nothing else: password
/// spraying at one attempt per account never trips a lockout, and an unregistered phone
/// number has no account to charge an OTP budget to. This closes the transport half of that
/// gap; <c>ISubjectRateLimiter</c>, applied inside the handlers, closes the other.
/// </para>
/// <para>
/// In-memory partitions, which means each instance enforces the limit independently. Stated
/// plainly rather than implied: behind three replicas the effective rate is three times the
/// configured one. That is still a bound where there was none, and the swap to a distributed
/// store is a limiter registration rather than a redesign.
/// </para>
/// </summary>
public static class RateLimitingRegistration
{
    public const string SignInPolicy = "diaspora-signin";
    public const string RegistrationPolicy = "diaspora-registration";
    public const string OneTimeCodePolicy = "diaspora-otp";
    public const string RecoveryPolicy = "diaspora-recovery";
    public const string RefreshPolicy = "diaspora-refresh";

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
    public const string AuthenticatedPolicy = "diaspora-authenticated";

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
