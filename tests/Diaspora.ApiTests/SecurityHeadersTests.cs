using Diaspora.Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diaspora.ApiTests;

/// <summary>
/// BSEC-010, at the middleware. Every response is sent a Content-Security-Policy that forbids
/// everything unless its endpoint sends one of its own, and the one path sent none is Scalar's API
/// reference, in Development, the one environment that maps it.
/// <para>
/// The middleware and nothing else. The host's own pages, and the policies they send in place of
/// this one, are proved against the whole pipeline in
/// <c>Diaspora.IntegrationTests.Platform.HostPagePolicyTests</c>.
/// </para>
/// </summary>
public sealed class SecurityHeadersTests
{
    /// <summary>Written out rather than read from the middleware, so the two cannot drift together.</summary>
    private const string ForbidsEverything =
        "default-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

    private const string OwnPolicyPath = "/declares-its-own";

    private const string OwnPolicy = "default-src 'none'; style-src 'sha256-c3R5bGU='";

    [Theory]
    [InlineData("/api/v1/anything")]
    [InlineData("/open/profile")]
    [InlineData("/")]
    public async Task A_response_whose_endpoint_sends_no_policy_is_sent_the_one_that_forbids_everything(string path)
    {
        using var host = await HostAsync(Environments.Production);

        var response = await host.GetTestClient().GetAsync(new Uri(path, UriKind.Relative));

        response.Headers.GetValues("Content-Security-Policy").Should().Equal(ForbidsEverything);
    }

    [Fact]
    public async Task An_endpoints_own_policy_replaces_the_default_rather_than_joining_it()
    {
        using var host = await HostAsync(Environments.Production);

        var response = await host.GetTestClient().GetAsync(new Uri(OwnPolicyPath, UriKind.Relative));

        string[] expected = [OwnPolicy];

        response.Headers.GetValues("Content-Security-Policy").Should().Equal(
            expected,
            "a browser enforces every policy a response carries, so a page sent the default beside "
            + "its own would still have its style and script refused");
    }

    [Theory]
    [InlineData("/scalar")]
    [InlineData("/scalar/v1")]
    public async Task The_API_reference_is_sent_no_policy_in_Development_where_it_is_mapped(string path)
    {
        using var host = await HostAsync(Environments.Development);

        var response = await host.GetTestClient().GetAsync(new Uri(path, UriKind.Relative));

        response.Headers.Contains("Content-Security-Policy").Should().BeFalse(
            "Scalar's page runs scripts of its own, which the default would refuse");
    }

    [Theory]
    [InlineData("Production", "/scalar/v1")]
    [InlineData("Staging", "/scalar/v1")]
    [InlineData("Testing", "/scalar/v1")]
    [InlineData("Development", "/scalarship")]
    [InlineData("Development", "/api/v1/anything")]
    public async Task Nothing_else_is_sent_without_one(string environment, string path)
    {
        using var host = await HostAsync(environment);

        var response = await host.GetTestClient().GetAsync(new Uri(path, UriKind.Relative));

        response.Headers.GetValues("Content-Security-Policy").Should().Equal(ForbidsEverything);
    }

    /// <summary>
    /// The middleware, and a terminal that answers every path with a page and declares a policy of
    /// its own only at <see cref="OwnPolicyPath"/>.
    /// </summary>
    private static Task<IHost> HostAsync(string environment) =>
        new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .UseEnvironment(environment)
                .Configure(app =>
                {
                    app.UseMiddleware<SecurityHeadersMiddleware>();
                    app.Run(context =>
                    {
                        if (context.Request.Path.Equals(new PathString(OwnPolicyPath), StringComparison.Ordinal))
                        {
                            context.Response.Headers.ContentSecurityPolicy = OwnPolicy;
                        }

                        context.Response.ContentType = "text/html; charset=utf-8";

                        return context.Response.WriteAsync("<!doctype html><title>page</title>");
                    });
                }))
            .StartAsync();
}
