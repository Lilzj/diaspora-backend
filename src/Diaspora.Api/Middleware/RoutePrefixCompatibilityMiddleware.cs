using System.Diagnostics.Metrics;
using Diaspora.Platform.Modules.Contributors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Diaspora.Api.Middleware;

/// <summary>
/// Serves the client's version-only base URL from the platform's canonical route space.
/// <para>
/// <b>The one question this closes.</b> Sixteen modules mint their prefix as
/// <c>/api/v1/{module}</c> and an architecture test asserts every one. The React Native client
/// resolves its base URL to <c>https://api.diaspora.app/v1</c>, so every request it makes arrives as
/// <c>/v1/conversations</c>, <c>/v1/notifications</c>, <c>/v1/marketplace/listings</c> — one segment
/// short of anything this host serves. B13 recorded it as a gateway concern, B13.2 restated it as
/// V-1, and nothing in this repository owned it: <c>docker/docker-compose.yml</c> runs the API, a
/// database, a cache and a tracer, and no reverse proxy at all.
/// </para>
/// <para>
/// <b>Why a rewrite and not a second set of routes.</b> Mapping every contributor twice would double
/// the OpenAPI document, double the endpoint metadata a rate-limit policy or an authorisation
/// requirement is attached to, and create a second public surface with its own lifetime — and the
/// first time somebody changed one prefix and not the other, the platform would serve two different
/// answers at two different paths. Rewriting the path once, before routing, means the alias reaches
/// <em>the same endpoint object</em>: same policy, same permissions, same telemetry, same
/// documentation entry. There is exactly one API here, addressable two ways.
/// </para>
/// <para>
/// <b>Why it must run before routing, and what that cost.</b> <c>WebApplication</c> inserts
/// <c>UseRouting</c> ahead of user middleware unless the host calls it explicitly, and endpoint
/// selection has already happened by the time any <c>app.Use(...)</c> runs — a path rewritten after
/// that point changes nothing, because the endpoint was chosen from the path as it arrived. So
/// <c>Program.cs</c> calls <c>UseRouting()</c> itself, immediately after this middleware. That also
/// moves routing to the position the framework documents as canonical — after the exception handler,
/// before CORS, authentication and the rate limiter — where every one of those reads the endpoint
/// metadata routing produced.
/// </para>
/// <para>
/// <b>It cannot manufacture a route.</b> The rewrite only prepends <c>/api</c>; if no module serves
/// the resulting path the caller gets the same 404 they would have got, from the same router. A
/// deployment that puts a real gateway in front turns this off with one setting and loses nothing,
/// because a gateway that rewrites means the aliased path never reaches the host.
/// </para>
/// </summary>
public sealed class RoutePrefixCompatibilityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly PathString _root;
    private readonly PathString[] _segments;

    public RoutePrefixCompatibilityMiddleware(
        RequestDelegate next, IOptions<ApiRoutingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _next = next;

        var value = options.Value;

        _root = new PathString(value.RootPrefix);

        // Distinct, because the configuration binder *appends* to an array property rather than
        // replacing it: a deployment that spells the shipped default in appsettings.json gets
        // ["/v1", "/v1"] and one that means to switch to "/v2" gets both. Neither is a fault worth
        // failing a boot over, and neither should reach the request path twice.
        _segments = value.EnablePrefixCompatibility
            ? [.. value.VersionSegments
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .Select(segment => segment.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(segment => new PathString(segment))]
            : [];
    }

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The common case on a deployment behind a gateway that already rewrites, and on every
        // request from a client that uses the canonical prefix: one array length check.
        if (_segments.Length == 0)
        {
            return _next(context);
        }

        var path = context.Request.Path;

        foreach (var segment in _segments)
        {
            // Segment-aware by contract: "/v1" matches "/v1" and "/v1/conversations", and never
            // "/v1x" — which a raw StartsWith would have claimed, turning one 404 into a different
            // one for a reason nobody could reconstruct from the logs.
            if (!path.StartsWithSegments(segment, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Prepend rather than rebuild, so the rest of the path — including its casing, its
            // escaping and any trailing slash — reaches the router exactly as it arrived.
            context.Request.Path = _root.Add(path);

            ApiRoutingTelemetry.CompatibilityRewrites.Add(
                1, new KeyValuePair<string, object?>("version", segment.Value));

            break;
        }

        return _next(context);
    }
}

/// <summary>
/// What the compatibility layer measures.
/// <para>
/// One counter, and it exists to answer a question nothing else can: <em>may this be switched
/// off?</em> The alias is a compatibility measure with no natural end, and a platform that cannot
/// see whether anybody still uses it will keep it forever. A deployment whose rewrite count has been
/// zero across a full release cycle has its answer.
/// </para>
/// <para>
/// Tagged with the version segment and nothing else. The path is not a dimension — it is unbounded,
/// and it would put member and entity identifiers into a metrics backend.
/// </para>
/// </summary>
public static class ApiRoutingTelemetry
{
    public const string SourceName = "Diaspora.Api";

    private static readonly Meter Meter = new(SourceName);

    /// <summary>Requests that arrived on a compatibility prefix and were rewritten.</summary>
    public static readonly Counter<long> CompatibilityRewrites =
        Meter.CreateCounter<long>("diaspora.api.compatibility_rewrites");
}

/// <summary>
/// Subscribes the host's metrics pipeline to the compatibility counter.
/// <para>
/// Attributed to <c>Platform.Routing</c> rather than to the host assembly's name. The kernel's
/// startup validator recognises three reserved prefixes for contributions that are the platform
/// rather than a module — <c>Platform</c>, <c>Application</c>, <c>Infrastructure</c> — and reports
/// anything else as a contributor attributed to a module nobody registered. Route normalisation is
/// platform infrastructure, and this puts it beside <c>Platform.Outbox</c> and
/// <c>Platform.Transactions</c> in the manifest, where a reader will look for it.
/// </para>
/// </summary>
public sealed class ApiRoutingObservabilityContributor : IObservabilityContributor
{
    public string Module => "Platform.Routing";

    public IEnumerable<string> Meters => [ApiRoutingTelemetry.SourceName];
}
