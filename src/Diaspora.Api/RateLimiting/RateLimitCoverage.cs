using Diaspora.Platform.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace Diaspora.Api.RateLimiting;

/// <summary>
/// Gives every endpoint composed through this host a transport budget, whether or not it asked
/// for one.
/// <para>
/// <b>What this replaces.</b> Rate limiting arrived as a targeted fix for credential exchange and
/// was then applied, module by module, to four route groups out of twenty-one. Around nine hundred
/// endpoints had no budget at all — every financial endpoint, every media upload, both webhook
/// callbacks, all anonymous discovery and search, and ten of the host's twelve controllers.
/// Nothing was broken: each module had simply never been told to opt in, and nothing asked. A
/// default that must be opted out of inverts that, so the omission that used to leave a module
/// unthrottled now leaves it throttled.
/// </para>
/// <para>
/// <b>Why a tier is chosen here rather than written at each route.</b> The question "is this
/// endpoint anonymous" already has an exact answer in the endpoint's own metadata — the
/// authorisation middleware reads the same two pieces to decide whether to challenge the caller.
/// Asking the endpoint is therefore not inference; writing the answer out again at nine hundred
/// routes would be, and it would drift the first time a route gained or lost
/// <c>RequireAuthorization</c> without someone remembering to revisit its budget.
/// </para>
/// <para>
/// <b>Why <c>Finally</c>.</b> An ordinary group convention runs <i>before</i> the endpoint's own,
/// so it would see neither the policy the route chose nor the authorisation it declared, and would
/// overwrite the first and misjudge the second. A <c>Finally</c> convention runs after both.
/// <c>RouteGroupBuilder</c> does not re-declare <c>Finally</c> — it inherits the default
/// implementation on <c>IEndpointConventionBuilder</c> — so the call needs the interface.
/// <c>RateLimitConventionTests</c> holds those three framework behaviours to account rather than
/// trusting them.
/// </para>
/// <para>
/// <b>The real-time hub is covered too, and it is the one place where "one request" does not mean
/// what it usually means.</b> <c>MapHub</c> contributes two endpoints — the socket and its
/// negotiate route — and both require authentication, so both take the authenticated budget: a
/// WebSocket client spends two of three hundred per connection, and a client that has fallen back
/// to long polling spends one per poll. Comfortable either way.
/// <para>
/// What matters if anyone changes it: every policy here is a sliding window, whose permits are
/// returned by time rather than by releasing the lease. The middleware holds a lease for as long
/// as the request lives, and a socket's request lives as long as the socket — so under
/// <c>RateLimitAlgorithm.Concurrency</c> the same arrangement would permit exactly
/// <c>PermitLimit</c> connections for the lifetime of the process and look healthy for the first
/// few minutes. A concurrency budget does not belong on a connection-oriented endpoint.
/// </para>
/// </para>
/// </summary>
public static class RateLimitCoverage
{
    /// <summary>
    /// Applies the tier default to every endpoint mapped through <paramref name="builder"/> that
    /// has not already settled the question itself.
    /// <para>
    /// Three outcomes, and the order matters. A route that named a policy keeps it — that is what
    /// makes the identity surface's tight budgets survive a platform-wide default. A route that
    /// opted out keeps its exemption, which is the only honest way to exclude an endpoint and is
    /// the form the coverage guardrail requires an exclusion to take. Everything else is given
    /// <see cref="RateLimitPolicyNames.Anonymous"/> or
    /// <see cref="RateLimitPolicyNames.Authenticated"/> according to whether a caller must prove
    /// who they are to reach it.
    /// </para>
    /// </summary>
    public static TBuilder WithPlatformRateLimitDefault<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Finally(endpoint =>
        {
            if (endpoint.Metadata.OfType<EnableRateLimitingAttribute>().Any()
                || endpoint.Metadata.OfType<DisableRateLimitingAttribute>().Any())
            {
                return;
            }

            endpoint.Metadata.Add(new EnableRateLimitingAttribute(
                RequiresAuthentication(endpoint)
                    ? RateLimitPolicyNames.Authenticated
                    : RateLimitPolicyNames.Anonymous));
        });

        return builder;
    }

    /// <summary>
    /// Whether a caller must prove who they are to reach this endpoint.
    /// <para>
    /// The same two pieces of metadata the authorisation middleware reads, in the same order, so
    /// the budget an endpoint gets cannot disagree with the challenge it issues. Both halves are
    /// load-bearing: a module group that declares no policy at all leaves its endpoints anonymous
    /// — which is how Guides and Jobs are reached without a token — and a route inside an
    /// authorised group can re-open itself with <c>AllowAnonymous</c>, which is how Housing's
    /// search and Marketplace's listings are reached.
    /// </para>
    /// <para>
    /// Unknown resolves to anonymous, and that is the safe direction: the cost of being wrong is
    /// a member metered at the tighter budget, not an anonymous caller metered at the looser one.
    /// </para>
    /// </summary>
    private static bool RequiresAuthentication(EndpointBuilder endpoint) =>
        endpoint.Metadata.OfType<IAuthorizeData>().Any()
        && !endpoint.Metadata.OfType<IAllowAnonymous>().Any();
}
