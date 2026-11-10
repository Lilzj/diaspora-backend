using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// The framework behaviour the platform's rate-limit default rests on, asserted against the
/// framework rather than taken from its documentation.
/// <para>
/// <b>Why this exists.</b> Coverage is applied once, where module endpoints are composed, so that
/// a new module is throttled by omission rather than unthrottled by omission. That only works if
/// three things are true of ASP.NET Core's route groups, and none is visible at the call site: a
/// policy named on the endpoint must beat the group's default, an endpoint must be able to opt out
/// of it, and a <c>Finally</c> convention must be able to read the endpoint's own metadata before
/// deciding. If the framework resolved the first the other way, the default would silently replace
/// every deliberate budget on the platform — a three-hundred-a-minute bucket quietly standing in
/// for the five-in-five-minutes one that guards one-time codes — and every existing test would
/// still pass.
/// </para>
/// <para>
/// These build a throwaway host rather than the platform's. The subject is the framework's
/// convention ordering, and one group with three routes is the whole of it; asserting the same
/// thing through three hundred composed endpoints would prove it less clearly, not more.
/// </para>
/// <para>
/// <b>Note on what is asserted.</b> <c>IRateLimiterMetadata</c> and
/// <c>IDisableRateLimitingMetadata</c> — the interfaces the middleware actually reads, and the
/// ones BSEC-002's verification criteria name — are <i>internal</i> to ASP.NET Core. The public
/// surface is <see cref="EnableRateLimitingAttribute"/> and
/// <see cref="DisableRateLimitingAttribute"/>, which are what <c>RequireRateLimiting</c> and
/// <c>DisableRateLimiting</c> add, so those are what any guardrail outside the framework can see.
/// </para>
/// </summary>
public sealed class RateLimitConventionTests
{
    private const string GroupDefault = "group-default";
    private const string EndpointChoice = "endpoint-choice";

    /// <summary>
    /// <b>A policy named on the endpoint beats the group's default.</b> This is what makes a
    /// default safe to apply to everything: the identity surface's tight budgets, declared route
    /// by route, keep their meaning.
    /// </summary>
    [Fact]
    public void A_policy_named_on_the_endpoint_beats_the_groups_default()
    {
        var endpoints = Compose(group =>
        {
            group.MapGet("/inherits", () => "a");
            group.MapGet("/overrides", () => "b").RequireRateLimiting(EndpointChoice);
        });

        Policy(endpoints, "/inherits").Should().Be(
            GroupDefault, "an endpoint that names no policy takes the group's default");

        Policy(endpoints, "/overrides").Should().Be(
            EndpointChoice,
            "the endpoint's own policy must be the one the middleware reads, or applying a " +
            "default where modules are composed would overwrite every deliberate budget");
    }

    /// <summary>
    /// <b>Opting out is declared on the route and survives the group.</b> That is the only honest
    /// way to exempt an endpoint once a default is on, and the shape the coverage guardrail
    /// requires an exemption to take.
    /// </summary>
    [Fact]
    public void An_endpoint_can_opt_out_of_the_groups_default()
    {
        var endpoints = Compose(group => group.MapGet("/exempt", () => "c").DisableRateLimiting());

        Endpoint(endpoints, "/exempt").Metadata
            .OfType<DisableRateLimitingAttribute>()
            .Should().NotBeEmpty("an exemption declared on the route must reach the middleware");
    }

    /// <summary>
    /// <b>A <c>Finally</c> convention on a group reads the endpoint's own metadata.</b> The
    /// platform's default is applied this way rather than as an ordinary group convention, because
    /// it has a question to ask about the finished endpoint — does this route require
    /// authentication, and did it already choose a policy — and an ordinary convention runs before
    /// the endpoint's own and so can see neither.
    /// <para>
    /// <c>RouteGroupBuilder</c> does not re-declare <c>Finally</c>; it inherits the default
    /// implementation on <c>IEndpointConventionBuilder</c>, so the call needs the interface. That
    /// is the kind of detail worth an assertion rather than a comment: if the inherited default
    /// did nothing, the platform's coverage would be absent and silent.
    /// </para>
    /// </summary>
    [Fact]
    public void A_finally_convention_on_a_group_reads_metadata_the_endpoint_added_itself()
    {
        // string? rather than string: EnableRateLimitingAttribute.PolicyName is nullable, because
        // the attribute also carries a generic policy that has no name. Declared nullable here
        // rather than filtered, so a nameless policy would show up as a null in the failure rather
        // than vanish from the collection being asserted.
        List<string?> seen = [];

        var endpoints = Compose(
            group => group.MapGet("/overrides", () => "b").RequireRateLimiting(EndpointChoice),
            group => ((IEndpointConventionBuilder)group).Finally(builder => seen.AddRange(
                builder.Metadata.OfType<EnableRateLimitingAttribute>().Select(
                    attribute => attribute.PolicyName))));

        endpoints.Should().NotBeEmpty("the throwaway host mapped a route");

        seen.Should().Contain(
            EndpointChoice,
            "a Finally convention runs after the endpoint's own conventions, which is the only " +
            "point at which 'did this route already choose a policy' has an answer");
    }

    private static List<Endpoint> Compose(
        Action<RouteGroupBuilder> map,
        Action<RouteGroupBuilder>? configure = null)
    {
        var builder = WebApplication.CreateSlimBuilder();

        using var app = builder.Build();

        var group = app.MapGroup(string.Empty);
        group.RequireRateLimiting(GroupDefault);
        configure?.Invoke(group);
        map(group);

        return [.. ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)];
    }

    private static RouteEndpoint Endpoint(List<Endpoint> endpoints, string route) =>
        endpoints.OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == route);

    private static string? Policy(List<Endpoint> endpoints, string route) =>
        Endpoint(endpoints, route).Metadata
            .OfType<EnableRateLimitingAttribute>()
            .LastOrDefault()?.PolicyName;
}
