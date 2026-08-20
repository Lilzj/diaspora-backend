using Diaspora.Platform.Operations.Policies;

namespace Diaspora.Platform.Operations.RateLimiting;

/// <summary>
/// Everything the limiter can partition on for one call.
/// <para>
/// A bag of optional values rather than a resolved key, because the policy decides which dimension
/// matters and the caller cannot know which policy will be applied. A caller that resolved its own
/// key would have to be changed every time a policy's dimension changed — which is precisely the
/// coupling that made rate limiting non-composable before B17 (DEBT-028).
/// </para>
/// </summary>
/// <param name="UserId">The authenticated member, when there is one.</param>
/// <param name="TenantId">The tenant. Single-tenant today.</param>
/// <param name="Endpoint">Route template, never the resolved path — a path carries identifiers.</param>
/// <param name="Module">The owning module.</param>
/// <param name="CountryCode">ISO country, where the request carries one.</param>
/// <param name="Role">The caller's role.</param>
/// <param name="ClientAddress">The client's address, already resolved through forwarded headers.</param>
/// <param name="Subject">
/// What is being protected, when that is not the caller — a phone number being flooded with codes,
/// an account being sprayed. Namespace it by purpose so a sign-in and a recovery do not share a budget.
/// </param>
public readonly record struct RateLimitRequest(
    string? UserId = null,
    string? TenantId = null,
    string? Endpoint = null,
    string? Module = null,
    string? CountryCode = null,
    string? Role = null,
    string? ClientAddress = null,
    string? Subject = null)
{
    /// <summary>
    /// The value this request contributes for a given dimension.
    /// <para>
    /// A missing value returns <c>"~"</c> rather than null, and every unattributable call therefore
    /// shares one bucket. That is a deliberate choice: the alternative — no limit when the dimension
    /// cannot be resolved — means an attacker who strips the header that carries it is unlimited.
    /// </para>
    /// </summary>
    public string Value(RateLimitDimension dimension) => dimension switch
    {
        RateLimitDimension.Global => "*",
        RateLimitDimension.User => UserId ?? "~",
        RateLimitDimension.Tenant => TenantId ?? "~",
        RateLimitDimension.Endpoint => Endpoint ?? "~",
        RateLimitDimension.Module => Module ?? "~",
        RateLimitDimension.Country => CountryCode ?? "~",
        RateLimitDimension.Role => Role ?? "~",
        RateLimitDimension.ClientAddress => ClientAddress ?? "~",
        RateLimitDimension.Subject => Subject ?? "~",
        _ => "~"
    };
}

/// <summary>The limiter's answer.</summary>
/// <param name="IsAllowed">Whether the call may proceed.</param>
/// <param name="RetryAfter">How long until the budget frees up. Zero when allowed.</param>
/// <param name="Remaining">Calls left in the current window.</param>
/// <param name="PolicyName">Which policy decided. Returned so the refusal can say which limit was hit.</param>
/// <param name="Dimension">Which dimension the budget belonged to.</param>
public readonly record struct RateLimitDecision(
    bool IsAllowed,
    TimeSpan RetryAfter,
    int Remaining,
    string PolicyName,
    RateLimitDimension Dimension)
{
    public static RateLimitDecision Allow(int remaining, string policyName, RateLimitDimension dimension) =>
        new(true, TimeSpan.Zero, remaining, policyName, dimension);

    public static RateLimitDecision Deny(TimeSpan retryAfter, string policyName, RateLimitDimension dimension) =>
        new(false, retryAfter, 0, policyName, dimension);

    /// <summary>The answer when no policy applies. Always allowed, and says so.</summary>
    public static RateLimitDecision NoLimit { get; } =
        new(true, TimeSpan.Zero, int.MaxValue, "none", RateLimitDimension.Global);
}

/// <summary>
/// The platform's rate limiter.
/// <para>
/// <b>One implementation for every limit on the platform</b>, driven entirely by
/// <see cref="RateLimitPolicy"/>. A module that wants a limit declares a policy and calls this; it
/// does not write a limiter, and it does not ask for the host's middleware to be edited. That is
/// DEBT-028's closure.
/// </para>
/// <para>
/// <b>The in-memory implementation is exact per instance and approximate across several.</b> Behind
/// three replicas, three independent budgets permit up to three times the configured rate. That is a
/// real weakening, it is stated here rather than in a footnote, and it is the whole reason this is an
/// interface: a shared-store implementation is a registration swap and nothing above this contract
/// changes.
/// </para>
/// </summary>
public interface IPlatformRateLimiter
{
    /// <summary>
    /// Consumes one unit of the named policy's budget, or refuses.
    /// <para>
    /// An unknown policy name allows the call. A limiter that refused everything on a typo would
    /// take the platform down over a misspelling, and the failure would look like an outage rather
    /// than like a configuration error.
    /// </para>
    /// </summary>
    ValueTask<RateLimitDecision> AcquireAsync(
        string policyName,
        RateLimitRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a slot taken under <see cref="RateLimitAlgorithm.Concurrency"/>.
    /// <para>
    /// A no-op for every other algorithm. A concurrency limit that is never released is a limit that
    /// permits exactly <c>PermitLimit</c> calls for the lifetime of the process, which is a failure
    /// that looks like a working limiter for the first few minutes.
    /// </para>
    /// </summary>
    ValueTask ReleaseAsync(
        string policyName,
        RateLimitRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears a budget. Called after a successful sign-in so a member who mistyped their password
    /// four times is not still throttled afterwards.
    /// </summary>
    ValueTask ResetAsync(
        string policyName,
        RateLimitRequest request,
        CancellationToken cancellationToken = default);
}
