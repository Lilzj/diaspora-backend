using System.Text.RegularExpressions;
using Diaspora.Api.Middleware;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// BSEC-012 in the real pipeline, and in the deployment's own file.
/// <para>
/// This suite runs with <c>AllowedHosts=localhost</c> (<see cref="CustomWebApplicationFactory"/>), so
/// every other test here already passes through the filter a deployment runs. These facts send what
/// it must refuse, and the one thing it must not: the orchestrator's probe naming the machine's
/// private address. That fact is the one that fails if the framework's own filter, which runs ahead
/// of all middleware and knows nothing of probes, is ever left holding the list.
/// </para>
/// </summary>
[Collection(PlatformCollection.Name)]
public sealed partial class AllowedHostTests : PooledIdentityTestBase
{
    public AllowedHostTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Theory]
    [InlineData("evil.example")]
    [InlineData("172.19.0.2:8080")]
    public async Task A_request_naming_a_host_this_deployment_does_not_serve_is_refused(string host)
    {
        var context = await SendAsync("/", host);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task The_host_it_serves_is_answered()
    {
        var context = await SendAsync("/", "localhost");

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Theory]
    [InlineData("172.19.0.2:8080")]
    [InlineData("[fdaa:0:1::3]:8080")]
    public async Task The_orchestrators_probe_is_answered_whatever_host_it_names(string host)
    {
        var context = await SendAsync("/health/live", host);

        context.Response.StatusCode.Should().Be(
            StatusCodes.Status200OK,
            "Fly.io checks liveness over the private network with the machine's own address as its "
            + "Host, and a failed check stops all traffic to the only machine");
    }

    [Fact]
    public void Every_https_url_the_deployment_configures_names_a_host_it_accepts()
    {
        var fly = File.ReadAllText(Path.Combine(RepositoryRoot.Current.FullName, "fly.toml"));

        var declared = AllowedHostsAssignment().Match(fly);

        declared.Success.Should().BeTrue(
            "fly.toml must name the hosts this deployment answers on, or a Production host refuses "
            + "to start");

        var hosts = AllowedHostOptions.Parse(declared.Groups["hosts"].Value);

        new AllowedHostOptionsValidator(new ProductionEnvironment())
            .Validate(null, new AllowedHostOptions { Hosts = hosts })
            .Succeeded.Should().BeTrue("the deployment's own list has to pass the rule its host enforces");

        var configured = HttpsAssignment().Matches(fly)
            .Select(match => match.Groups["host"].Value.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        configured.Should().NotBeEmpty(
            "fly.toml configures its email links, media and Stripe return URLs as https:// URLs");

        configured.Should().BeSubsetOf(
            hosts,
            "a URL this deployment hands out on a host it then refuses is a link that answers 400 to "
            + "everyone. A URL of a service it calls, rather than one it serves, does not belong in "
            + "this check");
    }

    private Task<HttpContext> SendAsync(string path, string host) =>
        Factory.Server.SendAsync(context =>
        {
            context.Request.Method = HttpMethods.Get;
            context.Request.Path = path;
            context.Request.Host = new HostString(host);
        });

    [GeneratedRegex(@"^\s*AllowedHosts\s*=\s*'(?<hosts>[^']*)'", RegexOptions.Multiline)]
    private static partial Regex AllowedHostsAssignment();

    /// <summary>An uncommented assignment whose value is an https:// URL, capturing its host.</summary>
    [GeneratedRegex(@"^\s*[A-Za-z_][A-Za-z0-9_]*\s*=\s*'https://(?<host>[^/:']+)", RegexOptions.Multiline)]
    private static partial Regex HttpsAssignment();

    private sealed class ProductionEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "Diaspora.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
