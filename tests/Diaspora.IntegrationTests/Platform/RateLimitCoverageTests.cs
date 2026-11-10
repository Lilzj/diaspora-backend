using Diaspora.Api.RateLimiting;
using Diaspora.IntegrationTests.Common;
using Diaspora.Platform.Http;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// Transport rate-limit coverage over the composed route table. BSEC-002's guardrail.
/// <para>
/// <b>The rule that was missing is the finding.</b> Rate limiting arrived as a targeted fix for
/// credential exchange; four module route groups out of twenty-one were later pointed at the
/// authenticated budget by hand, to repair a different mistake. Nothing ever asked about the
/// rest. Around nine hundred endpoints had no bound at all — every financial endpoint, every
/// media upload, both payment webhooks, all anonymous discovery and search, and ten of the host's
/// twelve controllers — and no test could have noticed, because the question had never been put
/// to the route table. Each new module shipped unthrottled by omission.
/// </para>
/// <para>
/// <b>Why this cannot be a build-time rule.</b> The policy an endpoint ends up with is assembled
/// at runtime from a group convention, the route's own declaration and, for controllers, an
/// attribute on the action — three mechanisms in two different parts of ASP.NET Core. No
/// compilation sees the result. A booted host does.
/// </para>
/// <para>
/// <b>What this does not cover, said rather than left to be inferred from a green suite.</b> The
/// host maps two documentation endpoints — <c>MapOpenApi</c> and the Scalar reference — only when
/// the environment is Development, so they are absent from this host's route table and from every
/// deployed one, and they are deliberately left without a budget: the reference page loads dozens
/// of assets in a second and would throttle itself. The claim below is therefore "every endpoint a
/// non-Development host composes", which is the surface that matters and the only one a test here
/// can see.
/// </para>
/// <para>
/// Shares the Platform collection's application: these read the route table and write nothing.
/// </para>
/// </summary>
[Collection(PlatformCollection.Name)]
public sealed class RateLimitCoverageTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly IReadOnlyList<RouteEndpoint> _endpoints;

    public RateLimitCoverageTests(CustomWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;

        // Creating a client is what forces the host to build; the route table does not exist
        // until something has asked the application to start.
        using var client = factory.CreateClient();

        _endpoints = [.. factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()];
    }

    /// <summary>
    /// The sanity check the rest rest on. A route table the scan cannot read is a suite that
    /// passes by finding nothing — and "every endpoint is throttled" is exactly the kind of claim
    /// an empty collection satisfies.
    /// </summary>
    [Fact]
    public void The_route_table_is_populated()
    {
        _endpoints.Should().HaveCountGreaterThan(
            500, "twenty-one modules and twelve controllers map several hundred routes");
    }

    /// <summary>
    /// <b>Every endpoint carries a transport budget.</b> Coverage now comes from the composition
    /// root rather than from each module, so what this asserts is not "did twenty-one modules
    /// remember" but "did the default reach everything this host composes". It fails the moment a
    /// route is mapped outside the two builders that carry the default — which, now that the
    /// default exists, is the only way an endpoint can escape one.
    /// </summary>
    [Fact]
    public void Every_endpoint_carries_a_transport_budget()
    {
        var unbounded = _endpoints
            .Where(endpoint => !endpoint.Metadata.OfType<EnableRateLimitingAttribute>().Any())
            .Where(endpoint => !endpoint.Metadata.OfType<DisableRateLimitingAttribute>().Any())
            .Select(Describe)
            .OrderBy(description => description, StringComparer.Ordinal)
            .ToList();

        unbounded.Should().BeEmpty(
            "an endpoint with no rate-limit policy is unbounded, and nothing about serving it "
            + "says so. Map it through a builder carrying WithPlatformRateLimitDefault, name a "
            + "policy on the route, or exempt it with DisableRateLimiting and name it in "
            + "Every_exemption_from_the_transport_budget_is_named:\n"
            + string.Join("\n", unbounded));
    }

    /// <summary>
    /// <b>Every exemption is named here.</b> An exemption is the one remaining way an endpoint can
    /// be unbounded, so adding one must be an edit a reviewer sees rather than a line inside a
    /// module.
    /// <para>
    /// Both of these are health probes, and they are exempt because of a failure the limit would
    /// <i>cause</i>. The limiter partitions on the caller's address, and the orchestrator's probe
    /// arrives through the same ingress as everything else — so a throttled probe stops answering
    /// during exactly the traffic spike it exists to survive. The machine is declared unhealthy
    /// and replaced, and the replacement meets the same spike: a load event turned into a restart
    /// loop by the control meant to contain it. Neither probe reads the database or reflects
    /// anything from the request.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_exemption_from_the_transport_budget_is_named()
    {
        string[] exempt = ["/health/live", "/health/ready"];

        var actual = _endpoints
            .Where(endpoint => endpoint.Metadata.OfType<DisableRateLimitingAttribute>().Any())
            .Select(endpoint => "/" + (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/'))
            .OrderBy(route => route, StringComparer.Ordinal)
            .ToList();

        actual.Should().BeEquivalentTo(
            exempt,
            "an endpoint exempt from rate limiting may be hit without bound by an anonymous "
            + "caller; each one has to be a decision somebody took deliberately");
    }

    /// <summary>
    /// <b>Every policy an endpoint names is one the host registered.</b> The rate-limit analogue
    /// of the authorisation catalogue's <c>Every_policy_an_endpoint_names_is_declared_by_a_module</c>,
    /// and it exists for the same reason: a name nobody answers to is neither a compile error nor
    /// a startup error. ASP.NET Core raises it on the request that reaches the endpoint, so the
    /// typo ships and the endpoint it was meant to protect is the one that fails.
    /// <para>
    /// That was not hypothetical here. Three module groups wrote <c>"diaspora-authenticated"</c>
    /// as a string literal, because the constants lived in the host and a module does not
    /// reference the host. They now live in <see cref="RateLimitPolicyNames"/>, in an assembly
    /// every module already references, so the compiler asks first and this asks second.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_rate_limit_policy_an_endpoint_names_is_registered()
    {
        var unknown = _endpoints
            .SelectMany(endpoint => endpoint.Metadata
                .OfType<EnableRateLimitingAttribute>()
                .Select(attribute => (attribute.PolicyName, Endpoint: Describe(endpoint))))
            .Where(entry => entry.PolicyName is not null)
            .Where(entry => !RateLimitingRegistration.Declared.Contains(entry.PolicyName!))
            .Select(entry => $"{entry.PolicyName} — {entry.Endpoint}")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        unknown.Should().BeEmpty(
            "an endpoint naming a policy the host never registered throws on the request that "
            + "reaches it:\n" + string.Join("\n", unknown));
    }

    /// <summary>
    /// <b>No endpoint ended up with two budgets.</b> The dangerous failure of applying a default
    /// everywhere is not that it misses something — the coverage rule catches that — but that it
    /// <i>wins</i>. The middleware reads the last policy in an endpoint's metadata, so a default
    /// appended after a route's own declaration would silently replace it: credential exchange is
    /// ten requests a minute and one-time codes are five in five minutes, against a default of
    /// three hundred. Every existing test would still have passed, because none of them asserts
    /// which policy an endpoint ended up with.
    /// </summary>
    [Fact]
    public void No_endpoint_carries_more_than_one_policy()
    {
        var doubled = _endpoints
            .Select(endpoint => (
                Endpoint: Describe(endpoint),
                Policies: endpoint.Metadata.OfType<EnableRateLimitingAttribute>()
                    .Select(attribute => attribute.PolicyName ?? "(unnamed)")
                    .ToList()))
            .Where(entry => entry.Policies.Count > 1)
            .Select(entry => $"{entry.Endpoint} — {string.Join(", ", entry.Policies)}")
            .OrderBy(description => description, StringComparer.Ordinal)
            .ToList();

        doubled.Should().BeEmpty(
            "two policies on one endpoint means the platform default was added alongside a "
            + "deliberate choice, and the middleware reads whichever was added last:\n"
            + string.Join("\n", doubled));
    }

    /// <summary>
    /// <b>A route that declared its own budget kept it.</b> Asserted by name against the budgets
    /// that matter most, and across both mapping mechanisms: <c>api/v1/auth/login</c> is an MVC
    /// action carrying an attribute, which is a different metadata path in ASP.NET Core from a
    /// minimal-API route's convention — and only one of the two was obvious.
    /// </summary>
    [Theory]
    [InlineData("POST", "api/v{version:apiVersion}/auth/login", RateLimitPolicyNames.SignIn)]
    [InlineData("POST", "api/v{version:apiVersion}/auth/register", RateLimitPolicyNames.Registration)]
    [InlineData("POST", "api/v{version:apiVersion}/auth/refresh", RateLimitPolicyNames.Refresh)]
    [InlineData("POST", "api/v{version:apiVersion}/auth/resend-otp", RateLimitPolicyNames.OneTimeCode)]
    public void A_route_that_declared_its_own_budget_kept_it(
        string method, string route, string expected) =>
        Policy(method, route).Should().Be(
            expected,
            "the route asked for this budget before the platform had a default, and the default "
            + "must not have taken it");

    /// <summary>
    /// <b>The surfaces BSEC-002 named are bounded, and bounded at the tier chosen for them.</b>
    /// The coverage rule would pass just as happily if every one of these had landed on the
    /// loosest budget the platform has. These are the endpoints the finding singled out: an
    /// anonymous database-backed search, a provider callback that reads a megabyte and runs HMAC
    /// over it before it can refuse it, a chunked upload, and a withdrawal.
    /// <para>
    /// <b>The withdrawal expects the anonymous tier, and that is not a mistake.</b> It carries no
    /// route-level authorisation — like 521 of the platform's 1,100 endpoints, it is authorised in
    /// the CQRS pipeline by <c>[Authorize(Permission = FinancialPermissions.PaymentsManage)]</c> on
    /// the command — so the transport, which decides the budget before authentication has run,
    /// cannot see that a token is required. Asserted rather than left implicit, because the tier an
    /// endpoint lands in is a consequence of where its authorisation is declared, and someone will
    /// one day move a declaration without expecting the budget to move with it.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("POST", "/api/v1/jobs/search", RateLimitPolicyNames.Search)]
    [InlineData("GET", "/api/v1/guides/", RateLimitPolicyNames.Search)]
    [InlineData("POST", "/api/v1/guides/", RateLimitPolicyNames.Authenticated)]
    [InlineData("GET", "/api/v1/knowledge/search", RateLimitPolicyNames.Search)]
    [InlineData("POST", "/api/v1/financial/webhooks/{provider}", RateLimitPolicyNames.Webhook)]
    [InlineData("PUT", "/api/v1/media/uploads/{sessionId:guid}/chunks/{chunkNumber:int}", RateLimitPolicyNames.Upload)]
    [InlineData("POST", "/api/v1/financial/wallets/me/withdrawals", RateLimitPolicyNames.Anonymous)]
    public void The_surfaces_the_finding_named_are_bounded_at_their_chosen_tier(
        string method, string route, string expected) =>
        Policy(method, route).Should().Be(expected, "BSEC-002 names this route");

    /// <summary>
    /// <b>The anonymous surface is metered more tightly than the authenticated one.</b> Not an
    /// assertion about two numbers in a settings file but about which way round they are. An
    /// anonymous request is attributable only to an address and no authorisation decision stands
    /// behind it; several hundred endpoints now draw on these budgets without naming them, so the
    /// relationship between them is part of the contract rather than a configuration detail.
    /// </summary>
    [Fact]
    public void The_tiers_are_ordered_the_way_the_design_claims()
    {
        var options = _factory.Services
            .GetRequiredService<IOptions<RateLimitingOptions>>().Value;

        options.Search.PermitLimit.Should().BeLessThan(
            options.Anonymous.PermitLimit,
            "a search takes caller-supplied filters and answers them from the database; one "
            + "request costs more than the read of one row the anonymous tier is sized for");

        options.Anonymous.PermitLimit.Should().BeLessThan(
            options.Authenticated.PermitLimit,
            "an unauthenticated caller must not be given the budget of one who proved who they are");

        options.Upload.PermitLimit.Should().BeGreaterThan(
            options.Authenticated.PermitLimit,
            "a chunked upload is legitimately many requests — the point of its own bucket is "
            + "that it is looser and separate, not that it is tighter");
    }

    /// <summary>
    /// The policy one endpoint carries, found by method and route.
    /// <para>
    /// By method as well as route, because a path is not a tier. <c>/api/v1/guides/</c> answers an
    /// anonymous search on GET and creates a guide on POST, and those belong on different budgets
    /// — which is itself worth asserting, since it is the clearest demonstration that the tier is
    /// read from each endpoint rather than guessed from its URL.
    /// </para>
    /// </summary>
    private string? Policy(string method, string route)
    {
        var endpoint = _endpoints.SingleOrDefault(candidate =>
            string.Equals(candidate.RoutePattern.RawText, route, StringComparison.OrdinalIgnoreCase)
            && (candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Contains(method, StringComparer.OrdinalIgnoreCase));

        endpoint.Should().NotBeNull(
            $"'{method} {route}' is the subject of this assertion — if it was renamed, this test "
            + "has to be renamed with it rather than quietly stop checking anything");

        return endpoint!.Metadata
            .OfType<EnableRateLimitingAttribute>()
            .LastOrDefault()?.PolicyName;
    }

    /// <summary>
    /// How an endpoint is named when it has to be reported. The display name alone is a lambda's
    /// signature for a minimal-API route, which identifies nothing.
    /// </summary>
    private static string Describe(RouteEndpoint endpoint) =>
        $"{string.Join("|", endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])} "
        + $"/{endpoint.RoutePattern.RawText}  ({endpoint.DisplayName})";
}
