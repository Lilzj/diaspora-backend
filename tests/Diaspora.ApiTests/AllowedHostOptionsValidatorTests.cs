using Diaspora.Api.Middleware;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.ApiTests;

/// <summary>
/// BSEC-012, at the validator. A deployed host names the hosts it serves, and no environment may
/// name one in a form that can never match a request.
/// </summary>
public sealed class AllowedHostOptionsValidatorTests
{
    [Theory]
    [InlineData("Production", null)]
    [InlineData("Production", "*")]
    [InlineData("Production", "diaspora-api.fly.dev;*")]
    [InlineData("Staging", "*")]
    [InlineData("Preview", null)]
    public void A_deployed_host_that_accepts_any_host_is_refused(string environment, string? configured)
    {
        var result = Validate(environment, configured);

        result.Failed.Should().BeTrue(
            "a list that accepts any host lets a forged Host header reach every handler, and a URL "
            + "built from the request's own host follows it");
    }

    [Theory]
    [InlineData("Development", null)]
    [InlineData("Development", "*")]
    [InlineData("Testing", "*")]
    public void Development_and_testing_may_accept_any_host(string environment, string? configured)
    {
        // Load-bearing, as TrustedProxyOptionsValidator's exemption is: the build's own OpenAPI
        // generation starts the application as Development, WebApplicationFactory boots this suite
        // as Development, and a developer host has no public name to list.
        Validate(environment, configured).Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("diaspora-api.fly.dev")]
    [InlineData("diaspora-api.fly.dev;api.diaspora.app")]
    [InlineData("*.diaspora.app")]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("[::1]")]
    public void A_list_of_host_names_satisfies_a_deployed_host(string configured)
    {
        Validate("Production", configured).Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://diaspora-api.fly.dev")]
    [InlineData("diaspora-api.fly.dev:443")]
    [InlineData("diaspora-api.fly.dev/")]
    [InlineData("diaspora-api*.fly.dev")]
    [InlineData("exa mple.com")]
    [InlineData("::1")]
    [InlineData("[]")]
    public void An_entry_that_could_never_match_is_refused_in_every_environment(string entry)
    {
        foreach (var environment in new[] { "Production", "Development", "Testing" })
        {
            Validate(environment, entry).Failed.Should().BeTrue(
                $"'{entry}' matches no request, so in {environment} too a list of such entries would "
                + "refuse every one");
        }
    }

    [Fact]
    public void The_refusal_says_where_a_deployment_names_its_hosts()
    {
        // The message is the remedy. An operator reads it in a startup failure, not in a report.
        var result = Validate("Production", null);

        result.FailureMessage.Should().Contain("fly.toml")
            .And.Contain("AllowedHosts = 'diaspora-api.fly.dev'");
    }

    private static ValidateOptionsResult Validate(string environment, string? configured) =>
        new AllowedHostOptionsValidator(new StubEnvironment(environment))
            .Validate(null, new AllowedHostOptions { Hosts = AllowedHostOptions.Parse(configured) });

    private sealed class StubEnvironment : IHostEnvironment
    {
        public StubEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "Diaspora.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
