using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// Where every composed endpoint's authorization is declared (BSEC-003's second verification
/// criterion).
/// <para>
/// <b>The rule the platform was promised and never got.</b> A comment in
/// <c>IdentityAuthenticationRegistration</c> justified <c>FallbackPolicy = null</c> on the grounds
/// that "an architecture test is what stops a new endpoint from being accidentally public — a check
/// that names the offending action beats a runtime default that has to be opted out of correctly
/// eleven times". That test was never written. What occupied its place counted the endpoints that
/// <em>do</em> carry authorization and asserted the count was over a hundred, which is true of a
/// platform with nine hundred open routes.
/// </para>
/// <para>
/// <b>Why it is a pinned set rather than a universal rule.</b> The platform enforces a request in
/// one of two places: on the route, or on the request type in the CQRS pipeline. 478 of 1,100
/// endpoints take the second — Financial's 112 and Community's 100 among them — so a rule demanding
/// route-level declaration everywhere would report hundreds of correctly protected endpoints, which
/// is precisely the reasoning that produced the weak rule in the first place.
/// </para>
/// <para>
/// Pinning the set instead gives the property the criterion actually asks for: <b>remove
/// <c>.RequireAuthorization</c> from any one endpoint and this fails, naming that endpoint.</b> It
/// joins the set, the set no longer matches, and the message says which route it was.
/// </para>
/// <para>
/// <b>It is safe to pin only because the other place is now enforced too.</b> Before BSEC-003,
/// "declares nothing at the route" and "declares nothing anywhere" were indistinguishable here.
/// <c>AuthorizationBehavior</c> now refuses a request type carrying neither <c>[Authorize]</c> nor
/// <c>[AllowAnonymous]</c>, and <c>RequestAuthorizationArchitectureTests</c> rejects one at build
/// time, so an endpoint on this list is delegating to a declaration that certainly exists.
/// </para>
/// </summary>
[Collection(PlatformCollection.Name)]
public sealed class RouteAuthorizationCoverageTests
{
    private const string BaselinePath =
        "tests/Diaspora.ArchitectureTests/EndpointsWithoutRouteAuthorization.txt";

    private readonly List<RouteEndpoint> _endpoints;

    public RouteAuthorizationCoverageTests(CustomWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        factory.EnsureDatabaseCreated();

        using var scope = factory.Services.CreateScope();

        _endpoints = [.. scope.ServiceProvider
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()];
    }

    /// <summary>
    /// The sanity check the rest rest on: a route table this test cannot see is a test that passes
    /// by finding nothing.
    /// </summary>
    [Fact]
    public void The_route_table_is_populated()
    {
        _endpoints.Should().HaveCountGreaterThan(
            500, "the host composes around 1,100 endpoints across 21 modules");
    }

    /// <summary>
    /// <b>Every endpoint that declares no authorization on its route is one we already knew about.</b>
    /// <para>
    /// An endpoint appearing here that is not on the baseline has lost a declaration it used to
    /// have, and the failure names it. That is the criterion.
    /// </para>
    /// </summary>
    [Fact]
    public void No_endpoint_quietly_stops_declaring_its_authorization()
    {
        var undeclared = Undeclared().ToHashSet(StringComparer.Ordinal);
        var baseline = Baseline().ToHashSet(StringComparer.Ordinal);

        undeclared.Except(baseline)
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .Should().BeEmpty(
                "this endpoint declares neither RequireAuthorization nor AllowAnonymous on its "
                + "route and is not in {0}. Either it lost a declaration it had, or it is new and "
                + "should be declaring one — a new route does not get to join the baseline",
                BaselinePath);
    }

    /// <summary>
    /// The baseline only shrinks, and every line on it is still true.
    /// </summary>
    /// <remarks>
    /// The second half is what stops the file becoming a standing exemption: a route that has since
    /// gained <c>RequireAuthorization</c> would otherwise keep its line, and losing the declaration
    /// again would be silently permitted — the list would grant back exactly what it was written to
    /// take away. The same trap the request review list was built to avoid.
    /// </remarks>
    [Fact]
    public void The_baseline_only_shrinks_and_names_nothing_that_now_declares()
    {
        var undeclared = Undeclared().ToHashSet(StringComparer.Ordinal);
        var baseline = Baseline();

        baseline.Should().OnlyHaveUniqueItems();

        baseline.Where(entry => !undeclared.Contains(entry))
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .Should().BeEmpty(
                "this route declares its authorization now, so its line in {0} is an exemption "
                + "nobody needs. Delete it — that is how the burn-down is recorded",
                BaselinePath);
    }

    /// <summary>
    /// <b>The two endpoints on the baseline that reach no handler are the health probes, by name.</b>
    /// <para>
    /// Every other line delegates to a request type whose attribute the pipeline enforces. These
    /// two have no request type at all, so nothing downstream would refuse them — which is the
    /// point: an orchestrator calls them with no credential, and BSEC-002 exempted them from the
    /// rate limiter for the same reason. A third endpoint in that position is a decision somebody
    /// has to write down here.
    /// </para>
    /// </summary>
    [Fact]
    public void The_only_endpoints_outside_the_pipeline_are_the_health_probes()
    {
        string[] expected = ["ANY /health/live", "ANY /health/ready"];

        Baseline().Where(entry => entry.StartsWith("ANY ", StringComparison.Ordinal))
            .Should().BeEquivalentTo(
                expected,
                "an endpoint mapped with no HTTP method constraint is not a module route; these "
                + "two are the orchestrator's probes and a third would need explaining");
    }

    private IEnumerable<string> Undeclared() =>
        _endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAuthorizeData>() is null
                               && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Select(Describe);

    private static string Describe(RouteEndpoint endpoint) =>
        string.Join(
            ",",
            (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["ANY"])
                .OrderBy(method => method, StringComparer.Ordinal))
        + " " + (endpoint.RoutePattern.RawText ?? "(null)");

    private static List<string> Baseline()
    {
        var path = Path.Combine(RepositoryRoot.Current.FullName, BaselinePath);

        File.Exists(path).Should().BeTrue(
            "{0} is tracked precisely so that it is here on every checkout", BaselinePath);

        return [.. File.ReadAllLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))];
    }
}
