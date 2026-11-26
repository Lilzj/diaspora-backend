using Diaspora.Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.ApiTests;

/// <summary>
/// BSEC-012, at the middleware. A Host the deployment does not serve is refused, the orchestrator's
/// two probes are answered whatever Host they send, and nothing else goes through on their account.
/// <para>
/// The registration and the middleware, nothing else. A bare host has none of the framework's own
/// filtering, so that the real host stands that filter down, where it runs ahead of everything, is
/// proved against the whole pipeline in <c>Diaspora.IntegrationTests.Platform.AllowedHostTests</c>.
/// </para>
/// </summary>
public sealed class AllowedHostFilteringTests
{
    private const string Served = "diaspora-api.fly.dev";

    [Theory]
    [InlineData("evil.example")]
    [InlineData("diaspora-api.fly.dev.evil.example")]
    [InlineData("172.19.0.2:8080")]
    public async Task A_host_the_deployment_does_not_serve_is_refused(string host)
    {
        using var server = await HostAsync(Environments.Production, Served);

        var context = await SendAsync(server, "/api/v1/anything", host);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task A_refusal_is_a_bare_400_with_no_page()
    {
        using var server = await HostAsync(Environments.Production, Served);

        var context = await SendAsync(server, "/api/v1/anything", "evil.example");

        context.Response.ContentType.Should().BeNull(
            "the framework's failure message is a page of HTML, and this host serves none it did not "
            + "write");
    }

    [Fact]
    public async Task A_request_that_names_no_host_is_refused()
    {
        using var server = await HostAsync(Environments.Production, Served);

        var context = await SendAsync(server, "/api/v1/anything", string.Empty);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Theory]
    [InlineData("diaspora-api.fly.dev")]
    [InlineData("DIASPORA-API.FLY.DEV")]
    [InlineData("diaspora-api.fly.dev:443")]
    public async Task The_host_it_serves_is_answered_in_any_case_and_on_any_port(string host)
    {
        using var server = await HostAsync(Environments.Production, Served);

        var context = await SendAsync(server, "/api/v1/anything", host);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Theory]
    [InlineData("/health/live", "172.19.0.2:8080")]
    [InlineData("/health/ready", "172.19.0.2:8080")]
    [InlineData("/health/live", "[fdaa:0:1::3]:8080")]
    [InlineData("/HEALTH/LIVE", "evil.example")]
    public async Task The_orchestrators_probes_are_answered_whatever_host_they_name(string path, string host)
    {
        using var server = await HostAsync(Environments.Production, Served);

        var context = await SendAsync(server, path, host);

        context.Response.StatusCode.Should().Be(
            StatusCodes.Status200OK,
            "Fly.io checks the machine over its private network, and a check answered 400 stops all "
            + "traffic to it");
    }

    [Theory]
    [InlineData("/health/livez")]
    [InlineData("/health/live/extra")]
    [InlineData("/health")]
    [InlineData("/api/v1/health/live")]
    public async Task Nothing_but_the_probes_themselves_goes_through(string path)
    {
        using var server = await HostAsync(Environments.Production, Served);

        var context = await SendAsync(server, path, "evil.example");

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Theory]
    [InlineData("Development", "*")]
    [InlineData("Testing", null)]
    public async Task Where_any_host_is_accepted_no_filter_runs(string environment, string? configured)
    {
        using var server = await HostAsync(environment, configured);

        var context = await SendAsync(server, "/api/v1/anything", "evil.example");

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("*")]
    public async Task A_deployed_host_that_accepts_any_host_does_not_start(string? configured)
    {
        var start = () => HostAsync(Environments.Production, configured);

        await start.Should().ThrowAsync<OptionsValidationException>();
    }

    /// <summary>The registration and the middleware, in front of a terminal that answers 200.</summary>
    private static async Task<IHost> HostAsync(string environment, string? configured)
    {
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal);

        if (configured is not null)
        {
            settings[AllowedHostOptions.ConfigurationKey] = configured;
        }

        return await new HostBuilder()
            .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(settings))
            .ConfigureWebHost(web => web
                .UseTestServer()
                .UseEnvironment(environment)
                .ConfigureServices(services => services.AddAllowedHostFiltering())
                .Configure(app =>
                {
                    app.UseAllowedHostFiltering();
                    app.Run(context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status200OK;

                        return Task.CompletedTask;
                    });
                }))
            .StartAsync();
    }

    private static Task<HttpContext> SendAsync(IHost host, string path, string hostHeader) =>
        host.GetTestServer().SendAsync(context =>
        {
            context.Request.Method = HttpMethods.Get;
            context.Request.Path = path;
            context.Request.Host = new HostString(hostHeader);
        });
}
