using System.Reflection;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Financial.Integration;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
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

    private readonly IServiceProvider _services;

    public RouteAuthorizationCoverageTests(CustomWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        factory.EnsureDatabaseCreated();

        _services = factory.Services;

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

    /// <summary>
    /// <b>Everything else on the baseline is a module route, which is what makes the file safe to
    /// hold.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The claim above it — "every other line delegates to a request type whose attribute the
    /// pipeline enforces" — was prose, and the rule beside it reads only the entries with no HTTP
    /// method constraint. Three entries were neither: <c>GET /</c> and the two Stripe checkout
    /// return pages, mapped by the host itself with no authorization and no request type, so
    /// nothing enforced anything. They were safe and meant to be public, and they sat here for a
    /// week because the sentence that would have caught them was not a test. BSEC-011 found them by
    /// asking what the published contract should say about every operation.
    /// </para>
    /// <para>
    /// They now declare <c>.AllowAnonymous()</c> and have left the file. This asserts the property
    /// that was being assumed: a line here is either one of the two probes or a route under
    /// <c>/api/v1/</c>, and a module route reaches its module through <c>ISender</c>, so the
    /// pipeline does run. An endpoint the host maps itself cannot join this list again without
    /// failing here.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_baseline_is_module_routes_and_the_two_probes()
    {
        var baseline = Baseline();

        baseline.Should().HaveCountGreaterThan(
            400, "the set is around 475 entries; an empty file would satisfy the filter below");

        baseline
            .Where(entry => !entry.StartsWith("ANY /health/", StringComparison.Ordinal))
            .Where(entry => !entry.Contains(" /api/v1/", StringComparison.Ordinal))
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .Should().BeEmpty(
                "an endpoint outside /api/v1/ is one this host maps itself, so there is no request "
                + "type behind it and nothing in the CQRS pipeline would refuse it. It is "
                + "unprotected unless its route says otherwise — declare .AllowAnonymous() if that "
                + "is intended, which also tells the published contract, or RequireAuthorization "
                + "if it is not");
    }

    /// <summary>
    /// <b>Every module route on the baseline reaches its module through <c>ISender</c>, checked rather
    /// than assumed.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rule above admits any undeclared route under <c>/api/v1/</c> on the strength of a
    /// sentence: "a module route reaches its module through <c>ISender</c>, so the pipeline does
    /// run." Two did not. <c>GET /notifications/realtime/configuration</c> and
    /// <c>GET /notifications/realtime/presence</c> read bound options and the presence store
    /// directly, bypassing MediatR on purpose, so nothing refused an anonymous caller while the
    /// published contract told every client a token was required (BSEC-009, BBL-007). It is the gap
    /// the rule above was written to close for three host routes, one level down: the premise was
    /// prose.
    /// </para>
    /// <para>
    /// A handler that takes no <c>ISender</c> has no request type for the pipeline to enforce, so its
    /// route is the only place its authorization can be stated. This reads the handler's own
    /// signature: a minimal-API delegate's parameters, or a controller's action and constructor.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_module_route_on_the_baseline_reaches_the_pipeline()
    {
        var moduleRoutes = _endpoints
            .Where(DeclaresNothing)
            .Where(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>() is not null)
            .ToList();

        moduleRoutes.Should().HaveCountGreaterThan(
            400, "the baseline holds over four hundred module routes; a filter that found none would "
                 + "pass by finding nothing");

        // Materialised and joined, because BeEmpty names only the first item it finds, and a guard
        // that reports one offender at a time turns a sweep into a loop.
        var outside = moduleRoutes
            .Where(endpoint => !ReachesTheSender(endpoint))
            .Select(Describe)
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToList();

        outside.Should().BeEmpty(
            "each of these declares no authorization, which delegates it to the request type the "
            + "pipeline enforces, and its handler takes no ISender: there is no request type, so "
            + "nothing refuses an anonymous caller. Declare RequireAuthorization on the route, or "
            + "AllowAnonymous if a stranger is meant to reach it: {0}",
            string.Join(", ", outside));
    }

    /// <summary>
    /// <b>A seam excused from the rule above sends through <c>ISender</c> itself, checked.</b>
    /// Without this the list below would be the prose the rule replaced, one name long.
    /// </summary>
    [Fact]
    public void Every_seam_a_route_takes_instead_of_the_sender_sends_through_it()
    {
        using var scope = _services.CreateScope();

        foreach (var seam in SeamsThatSend)
        {
            var implementation = scope.ServiceProvider.GetRequiredService(seam).GetType();

            implementation.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => parameter.ParameterType)
                .Should().Contain(
                    typeof(ISender),
                    "{0} stands in for ISender on a route only because {1} sends every call through it",
                    seam.Name,
                    implementation.Name);
        }
    }

    /// <summary>
    /// <b>Seams a module route may take in place of <c>ISender</c>, by name.</b>
    /// <para>
    /// CommunityProgramme's nineteen campaign routes reach the pipeline one hop away: they take
    /// Financial's <see cref="ICampaigns"/>, whose implementation sends every call through
    /// <c>ISender</c> so that every pipeline behaviour still runs. The rule's first version read only
    /// the delegate's own parameters and reported all nineteen, beside the two realtime routes it was
    /// written for. A seam joins this list as a decision, and the fact above checks it.
    /// </para>
    /// </summary>
    private static readonly Type[] SeamsThatSend = [typeof(ICampaigns)];

    private static bool DeclaresNothing(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IAuthorizeData>() is null
        && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null;

    /// <summary>
    /// Whether the handler behind a route can reach the CQRS pipeline at all: a minimal-API delegate
    /// that takes <c>ISender</c>, or a controller given one.
    /// </summary>
    private static bool ReachesTheSender(RouteEndpoint endpoint)
    {
        if (endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is { } action)
        {
            return TakesTheSender(action.MethodInfo.GetParameters())
                   || action.ControllerTypeInfo.GetConstructors()
                       .Any(constructor => TakesTheSender(constructor.GetParameters()));
        }

        return endpoint.Metadata.GetMetadata<MethodInfo>() is { } handler
               && TakesTheSender(handler.GetParameters());
    }

    private static bool TakesTheSender(IEnumerable<ParameterInfo> parameters) =>
        parameters.Any(parameter => typeof(ISender).IsAssignableFrom(parameter.ParameterType)
                                    || SeamsThatSend.Contains(parameter.ParameterType));

    private IEnumerable<string> Undeclared() =>
        _endpoints
            .Where(DeclaresNothing)
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
