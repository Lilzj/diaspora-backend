using System.Reflection;
using Diaspora.IntegrationTests.Common;
using Diaspora.Platform.Authorization;
using Diaspora.Platform.Modules;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// The composed route table, read from the host that composed it.
/// <para>
/// <b>These are the only guardrails in the phase that cannot be a build-time rule, and the reason
/// is the reason they matter.</b> The route table is assembled at runtime from fifteen modules'
/// endpoint contributors and one project's controllers; no compilation sees more than a fraction of
/// it, so "do two modules claim the same URL" is a question only a booted host can answer.
/// </para>
/// <para>
/// <b>And ASP.NET Core answers it late.</b> An ambiguous match is not a startup failure — it is an
/// <c>AmbiguousMatchException</c> on the request that hit it, in production, on whichever endpoint
/// the two modules disagreed about. A duplicate endpoint name is worse still: link generation picks
/// one silently, so the wrong URL is handed to a client with nothing logged anywhere.
/// </para>
/// </summary>
public sealed class ApiGuardrailTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly IReadOnlyList<RouteEndpoint> _endpoints;

    public ApiGuardrailTests(CustomWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;

        using var client = factory.CreateClient();

        _endpoints = [.. factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()];
    }

    /// <summary>
    /// The sanity check the rest rest on: a route table the scan cannot read is a suite that passes
    /// by finding nothing.
    /// </summary>
    [Fact]
    public void The_route_table_is_populated()
    {
        _endpoints.Should().HaveCountGreaterThan(
            100, "fifteen modules and seven controllers map several hundred routes between them");
    }

    /// <summary>
    /// <b>No two endpoints answer the same method and pattern.</b> Two modules mapping one route is
    /// an ambiguous match, and the platform finds out about it on the request rather than at boot.
    /// </summary>
    [Fact]
    public void No_two_endpoints_claim_the_same_method_and_route()
    {
        var duplicates = _endpoints
            .SelectMany(endpoint => Methods(endpoint)
                .Select(method => (Method: method, Route: endpoint.RoutePattern.RawText ?? string.Empty, endpoint.DisplayName)))
            .GroupBy(entry => (entry.Method, entry.Route), StringTupleComparer.Instance)
            .Where(group => group.Count() > 1)
            .Select(group =>
                $"{group.Key.Method} {group.Key.Route} — " +
                string.Join(" | ", group.Select(entry => entry.DisplayName)))
            .ToList();

        duplicates.Should().BeEmpty(
            "an ambiguous match is a 500 on the request that finds it:\n" +
            string.Join("\n", duplicates));
    }

    /// <summary>
    /// <b>No two endpoints share a name.</b> Link generation resolves a name to whichever endpoint
    /// it reaches first, so a collision hands clients a URL for a different resource with nothing
    /// logged.
    /// </summary>
    [Fact]
    public void No_two_endpoints_share_a_name()
    {
        var duplicates = _endpoints
            .Select(endpoint => (
                Name: endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName,
                endpoint.DisplayName))
            .Where(entry => !string.IsNullOrEmpty(entry.Name))
            .GroupBy(entry => entry.Name!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} — " + string.Join(" | ", group.Select(entry => entry.DisplayName)))
            .ToList();

        duplicates.Should().BeEmpty(
            "link generation picks one and says nothing:\n" + string.Join("\n", duplicates));
    }

    /// <summary>
    /// <b>Every module's routes live under its own versioned prefix.</b> The version is in the URL
    /// because a breaking change to a module's contracts is a new prefix rather than a negotiation,
    /// and a route outside every declared prefix belongs to a module that has quietly taken a URL
    /// space nobody registered.
    /// </summary>
    [Fact]
    public void Every_module_route_is_versioned()
    {
        var unversioned = _endpoints
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .Where(route => route.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
            .Where(route => !route.StartsWith("api/v", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        unversioned.Should().BeEmpty(
            "an unversioned API route cannot be changed without breaking whoever is already " +
            "calling it:\n" + string.Join("\n", unversioned));
    }

    /// <summary>
    /// <b>Every URL space a module declares is one it actually serves.</b> B1.4 recorded F6 —
    /// Bookmarks routed under <c>/api/v1/bookmarks</c> while declaring no prefix constant, so the
    /// module's URL space was outside every rule about prefixes. B1.4.1 added the constant, and the
    /// way that fix stops being real is the constant drifting away from the routes: renamed in the
    /// metadata, still literal in the contributor, and nothing anywhere notices.
    /// <para>
    /// Read from the composed route table, so it is the served URL being compared rather than the
    /// same constant twice.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_declared_route_prefix_actually_serves_routes()
    {
        // Minimal APIs keep the leading slash their group was given; controller conventions strip
        // it. Both are in this table, so normalise before comparing against a declared prefix.
        var routes = _endpoints
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .Select(route => route.StartsWith('/') ? route : "/" + route)
            .ToList();

        var unserved = DeclaredRoutePrefixes()
            .Where(entry => !routes.Exists(route =>
                route.StartsWith(entry.Prefix + "/", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(route.TrimEnd('/'), entry.Prefix, StringComparison.OrdinalIgnoreCase)))
            .Select(entry => $"{entry.Owner} — {entry.Prefix}")
            .ToList();

        unserved.Should().BeEmpty(
            "a declared prefix nothing serves is a constant that has drifted from its routes:\n" +
            string.Join("\n", unserved));
    }

    /// <summary>
    /// <b>Every policy an endpoint names is one some module declared.</b> A typo in a policy name
    /// does not fail anything: ASP.NET Core denies the request, so the endpoint answers 403 to
    /// correctly authorised callers forever, and the failing path is the safe one nobody tests.
    /// <para>
    /// This is the runtime half of <c>SV3006</c>. The validator asks whether every declared policy
    /// was registered; this asks whether every policy an endpoint <em>asks for</em> was declared.
    /// Between them there is no gap for a name to fall through.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_policy_an_endpoint_names_is_declared_by_a_module()
    {
        var catalogue = _factory.Services.GetRequiredService<IPolicyCatalog>();

        var unknown = _endpoints
            .SelectMany(endpoint => endpoint.Metadata
                .GetOrderedMetadata<IAuthorizeData>()
                .Select(data => (Policy: data.Policy, endpoint.DisplayName)))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Policy))
            .Where(entry => catalogue.Find(entry.Policy!) is null)
            .Select(entry => $"{entry.Policy} — {entry.DisplayName}")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        unknown.Should().BeEmpty(
            "an endpoint asking for a policy nobody declared denies every caller, silently:\n" +
            string.Join("\n", unknown));
    }

    /// <summary>
    /// <b>Where write authorisation lives, stated once so the two places cannot drift apart.</b>
    /// The platform enforces a mutation in one of two ways: <c>RequireAuthorization</c> on the route,
    /// or <c>[Authorize]</c> on the command, which <c>AuthorizationBehavior</c> applies in the CQRS
    /// pipeline. Both are real and both are used — around a third of the platform's write endpoints
    /// take the second route — so a rule demanding the first would have reported seventy-eight
    /// correctly protected endpoints.
    /// <para>
    /// What is worth asserting is that the first route is still load-bearing rather than
    /// decorative: if endpoint-level authorisation quietly disappeared, every module that relies on
    /// it would be open and nothing else here would notice.
    /// </para>
    /// </summary>
    [Fact]
    public void Route_level_authorisation_is_still_in_use()
    {
        var authorised = _endpoints
            .Count(endpoint => endpoint.Metadata.GetMetadata<IAuthorizeData>() is not null);

        authorised.Should().BeGreaterThan(
            100, "most of the platform's endpoints declare their policy on the route");
    }

    /// <summary>
    /// Every module's route prefix constants, found the way the host finds its modules. Every
    /// constant ending in <c>RoutePrefix</c>, because Guides owns two — its own and B8.1's.
    /// </summary>
    private static IReadOnlyList<(string Owner, string Prefix)> DeclaredRoutePrefixes() =>
        [.. ModuleAssemblyScanner.FindPlatformAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Name.EndsWith("ModuleMetadata", StringComparison.Ordinal))
            .SelectMany(type => type
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field =>
                    field.IsLiteral &&
                    field.FieldType == typeof(string) &&
                    field.Name.EndsWith("RoutePrefix", StringComparison.Ordinal))
                .Select(field => (
                    Owner: $"{type.Name}.{field.Name}",
                    Prefix: (string)field.GetRawConstantValue()!)))];

    private static IReadOnlyList<string> Methods(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];

    /// <summary>Ordinal comparison for the (method, route) grouping key.</summary>
    private sealed class StringTupleComparer : IEqualityComparer<(string Method, string Route)>
    {
        public static StringTupleComparer Instance { get; } = new();

        public bool Equals((string Method, string Route) x, (string Method, string Route) y) =>
            string.Equals(x.Method, y.Method, StringComparison.Ordinal) &&
            string.Equals(x.Route, y.Route, StringComparison.Ordinal);

        public int GetHashCode((string Method, string Route) obj) =>
            HashCode.Combine(obj.Method, obj.Route);
    }
}
