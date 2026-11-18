using Diaspora.IntegrationTests.Common;
using Diaspora.Platform.Http;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// BSEC-004. The writes a stranger may perform, and what bounds them.
/// <para>
/// Seven routes across four commands let an unauthenticated caller change a number the platform
/// shows: a guide view, a guide share, a notice activity signal, and the four country knowledge
/// signals. Each is deliberately anonymous — a figure that counted only members would understate
/// the one number measuring whether the knowledge base works — and each feeds trending, discovery
/// and the country intelligence the Analytics module projects.
/// </para>
/// <para>
/// <b>The finding called them unthrottled, and that stopped being true at BSEC-002</b>, which gave
/// every endpoint a tier by default. What was still true is that they fell to the anonymous tier
/// at 240 a minute — 345,600 counter increments a day from one address — which is a bound in the
/// sense that it is not infinite and in no other sense.
/// </para>
/// <para>
/// Shares the Platform collection's application: the route-table test writes nothing, and the
/// throttling test builds hosts of its own.
/// </para>
/// </summary>
[Collection(PlatformCollection.Name)]
public sealed class EngagementWriteTests
{
    /// <summary>
    /// The routes, by the pattern the route table holds them under. Pinned by name rather than
    /// discovered, because the property under test is "these and no others": a route joining this
    /// set is a new way for a stranger to move a number, which is a decision somebody should make
    /// here rather than discover in production.
    /// </summary>
    private static readonly string[] Engagement =
    [
        "POST /api/v1/guides/{guideId:guid}/share",
        "POST /api/v1/guides/{guideId:guid}/view",
        "POST /api/v1/intelligence/notices/{noticeId:guid}/activity",
        "POST /api/v1/knowledge/countries/{countryCode}/activity/completed",
        "POST /api/v1/knowledge/countries/{countryCode}/activity/discovered",
        "POST /api/v1/knowledge/countries/{countryCode}/activity/path-completed",
        "POST /api/v1/knowledge/countries/{countryCode}/activity/path-started",
    ];

    private readonly IReadOnlyList<RouteEndpoint> _endpoints;

    public EngagementWriteTests(CustomWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        using var client = factory.CreateClient();

        _endpoints = [.. factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()];
    }

    /// <summary>
    /// The sanity check the rest rest on: a scan that reads an empty route table satisfies every
    /// claim below by finding nothing.
    /// </summary>
    [Fact]
    public void The_route_table_is_populated()
    {
        _endpoints.Should().HaveCountGreaterThan(500);

        // Both kinds of endpoint, because the host composes two and a scan that sees one of them
        // passes every claim below for the wrong reason. The modules map minimal APIs; Identity
        // and the other controllers route through Asp.Versioning under a template rather than a
        // literal version. This caught a filter that was dropping all of the latter.
        var login = _endpoints
            .Where(endpoint => Describe(endpoint).Contains("/auth/login", StringComparison.Ordinal))
            .ToList();

        login.Should().NotBeEmpty("a controller route must be visible to this scan");
        login.Should().OnlyContain(
            endpoint => endpoint.Metadata.OfType<IAllowAnonymous>().Any()
                && Methods(endpoint).Contains("POST", StringComparer.Ordinal),
            "and it must be an anonymous POST, which is what the scan below selects on");
    }

    /// <summary>
    /// The finding's second verification criterion, widened from the three endpoints it names to
    /// the seven that exist.
    /// <para>
    /// <b>Why this is not covered by the route-authorization baseline.</b> That guardrail asserts
    /// each route declares <em>either</em> <c>RequireAuthorization</c> <em>or</em>
    /// <c>AllowAnonymous</c>, so replacing one with the other keeps it green. For a route that is
    /// anonymous on purpose, that swap is a silent product change — the counter stops counting the
    /// readers it exists to count. This asserts the specific metadata.
    /// </para>
    /// </summary>
    [Fact]
    public void A_stranger_may_write_to_exactly_these_counters()
    {
        var anonymousWrites = _endpoints
            .Where(endpoint => Methods(endpoint).Contains("POST", StringComparer.Ordinal))
            .Where(endpoint => endpoint.Metadata.OfType<IAllowAnonymous>().Any())
            .Select(Describe)
            // "/api/" and not "/api/v1/", which is not a detail. The modules map minimal-API
            // routes under a literal "/api/v1" prefix, while the controllers route through
            // Asp.Versioning and hold the template "api/v{version:apiVersion}/..." — so a filter
            // on the literal version segment drops every controller route, Identity's whole
            // surface among them. It fails open: fewer rows means fewer things to object to, so
            // the guardrail stays green while covering half of what it claims.
            .Where(route => route.Contains("/api/", StringComparison.Ordinal))
            .ToList();

        anonymousWrites.Should().Contain(
            Engagement,
            "each of these declares its anonymity on the route, not only on the command");

        // The rest of the anonymous POST surface, which must stay what it is: credential exchange
        // and signature-verified callbacks. Anything else appearing here is a third kind of
        // anonymous write, and the point of listing them is that nobody adds one by accident.
        var rest = anonymousWrites.Except(Engagement).ToList();

        // An OnlyContain over a set that turned out to be empty — or to have lost a whole family —
        // passes by finding nothing. These two are the families the clause below exists to permit,
        // so if either stops being found the clause has quietly become decorative.
        rest.Should().Contain(route => route.Contains("/auth/login", StringComparison.Ordinal))
            .And.Contain(route => route.Contains("/webhooks/", StringComparison.Ordinal));

        rest.Should().OnlyContain(
            route => route.Contains("/auth/", StringComparison.Ordinal)
                || route.Contains("/webhooks/", StringComparison.Ordinal)
                || route.Contains("/notifications/unsubscribe", StringComparison.Ordinal)
                || route.Contains("/marketplace/search", StringComparison.Ordinal),
            "a stranger may exchange a credential, deliver a signed callback, redeem an " +
            "unsubscribe token, search, or move one of the engagement counters — and nothing else");
    }

    /// <summary>
    /// And each of them names the engagement budget rather than inheriting the anonymous one.
    /// <para>
    /// Asserted on <see cref="EnableRateLimitingAttribute"/> rather than on the framework's
    /// <c>IRateLimiterMetadata</c>, which is internal.
    /// </para>
    /// </summary>
    [Fact]
    public void Each_of_them_names_the_engagement_budget()
    {
        var policies = _endpoints
            .Where(endpoint => Engagement.Contains(Describe(endpoint), StringComparer.Ordinal))
            .Select(endpoint => (
                Route: Describe(endpoint),
                Policy: endpoint.Metadata.OfType<EnableRateLimitingAttribute>()
                    .Select(attribute => attribute.PolicyName)
                    .FirstOrDefault()))
            .ToList();

        policies.Should().HaveCount(
            Engagement.Length, "every pinned route must exist, or this test is checking nothing");

        policies.Should().OnlyContain(
            entry => entry.Policy == RateLimitPolicyNames.Engagement,
            "inheriting the anonymous tier is what the finding is about: 240 a minute is " +
            "345,600 counter increments a day from one address. Found: {0}",
            string.Join(", ", policies.Select(entry => $"{entry.Route} -> {entry.Policy ?? "(none)"}")));
    }

    private static IReadOnlyList<string> Methods(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];

    // Normalised to exactly one leading slash, which matters because the two kinds of endpoint on
    // this host disagree: a minimal-API route's RawText starts with "/" and a controller route's
    // (from [Route("api/v1/auth")]) does not. RateLimitCoverageTests.Describe prepends one
    // unconditionally and so reports "POST //api/v1/..." for half the table — harmless there,
    // because nothing compares it against a literal, and the whole assertion here.
    private static string Describe(RouteEndpoint endpoint) =>
        $"{string.Join("|", Methods(endpoint))} /{(endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/')}";
}
