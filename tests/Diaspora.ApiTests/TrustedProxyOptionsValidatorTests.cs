using Diaspora.Api.Middleware;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.ApiTests;

/// <summary>
/// Pins the fail-closed half of BSEC-001: a deployed host that names no trusted ingress must not
/// start.
/// <para>
/// The finding was not that a value was missing. It was that an empty allow-list made ASP.NET Core
/// trust <i>every</i> proxy, that two comments asserted the opposite, and that nothing anywhere
/// executed the claim. Both sibling validators on this host — <c>RateLimitingOptionsValidator</c>
/// and <c>ApiRoutingOptionsValidator</c> — still have no tests of their own, so this suite is
/// deliberately the first of its kind here rather than a copy of a pattern.
/// </para>
/// </summary>
public class TrustedProxyOptionsValidatorTests
{
    // ─── The refusal ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Preview")]
    public void A_deployed_host_that_names_no_ingress_is_refused(string environment)
    {
        var result = Validate(environment, new TrustedProxyOptions());

        result.Failed.Should().BeTrue(
            "an empty allow-list makes ASP.NET Core apply X-Forwarded-For unconditionally, so it "
            + "is a misconfiguration rather than a default");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void Development_and_testing_may_leave_the_allow_list_empty(string environment)
    {
        // Load-bearing, not convenience: CustomWebApplicationFactory runs the integration suite
        // as Testing, WebApplicationFactory boots this suite as Development, and the build's own
        // OpenAPI generation starts the whole application as Development to emit
        // contracts/Diaspora.Api.json. Refusing in these three fails the build, not an attacker.
        var result = Validate(environment, new TrustedProxyOptions());

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void The_refusal_names_the_indexed_environment_variable_form()
    {
        // The message is the whole point of the refusal. An operator who reads it and then sets
        // the flat form has not fixed anything, so the message has to say so where they will see
        // it — in the startup failure, not in a report.
        var result = Validate("Production", new TrustedProxyOptions());

        result.FailureMessage.Should().Contain("KnownIPNetworks__0");
        result.FailureMessage.Should().Contain("binds to nothing");
    }

    [Theory]
    [InlineData("172.16.0.0/12")]
    [InlineData("fdaa::/16")]
    public void A_network_on_its_own_satisfies_a_deployed_host(string network)
    {
        var result = Validate("Production", new TrustedProxyOptions { KnownIPNetworks = [network] });

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void A_single_proxy_address_on_its_own_satisfies_a_deployed_host()
    {
        var result = Validate(
            "Production",
            new TrustedProxyOptions { KnownProxies = ["172.19.11.233"] });

        result.Succeeded.Should().BeTrue();
    }

    // ─── Values that used to be dropped in silence ──────────────────────────────────────────

    [Fact]
    public void A_network_that_is_not_cidr_is_refused_rather_than_dropped()
    {
        // The old block parsed with TryParse and no else, so "172.16.0.0" without a prefix, or a
        // typo, left the allow-list emptier than the configuration said it was.
        var result = Validate(
            "Production",
            new TrustedProxyOptions { KnownIPNetworks = ["172.16.0.0"] });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("172.16.0.0");
    }

    [Fact]
    public void A_proxy_that_is_not_an_address_is_refused_rather_than_dropped()
    {
        var result = Validate(
            "Production",
            new TrustedProxyOptions { KnownProxies = ["ingress.internal"] });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("ingress.internal");
    }

    // ─── The binding trap, which is how this fix is most likely to be undone ────────────────

    [Fact]
    public void A_flat_environment_variable_binds_to_an_empty_list_and_is_therefore_refused()
    {
        // `fly secrets set ForwardedHeaders__KnownIPNetworks=172.16.0.0/12` is the obvious fix
        // for BSEC-001 and it changes nothing: configuration reads an array from indexed child
        // keys, so a section holding a scalar has a value, no children, and binds to an empty
        // list. Measured on .NET 10.0.13. Before the validator this deployed green and left the
        // platform exposed; this test is what keeps that from being quiet again.
        var options = Bind(("ForwardedHeaders:KnownIPNetworks", "172.16.0.0/12"));

        options.KnownIPNetworks.Should().BeEmpty("a scalar does not bind to a string[]");
        Validate("Production", options).Failed.Should().BeTrue();
    }

    [Fact]
    public void The_indexed_environment_variable_form_binds_and_is_accepted()
    {
        var options = Bind(("ForwardedHeaders:KnownIPNetworks:0", "172.16.0.0/12"));

        options.KnownIPNetworks.Should().Equal("172.16.0.0/12");
        Validate("Production", options).Succeeded.Should().BeTrue();
    }

    // ─── The wiring, as the host assembles it ───────────────────────────────────────────────

    [Fact]
    public void The_options_pipeline_throws_rather_than_yielding_an_empty_allow_list()
    {
        // Mirrors Program.cs's registration rather than executing it, which is this test's one
        // limitation and is stated rather than glossed: it proves the validator is reached
        // through AddOptions/Bind/ValidateOnStart, not that Program.cs calls it.
        using var provider = BuildHostLikeProvider("Production");

        var resolve = () => provider.GetRequiredService<IOptions<TrustedProxyOptions>>().Value;

        resolve.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void The_options_pipeline_yields_the_configured_network_when_it_is_named()
    {
        using var provider = BuildHostLikeProvider(
            "Production",
            ("ForwardedHeaders:KnownIPNetworks:0", "172.16.0.0/12"));

        var options = provider.GetRequiredService<IOptions<TrustedProxyOptions>>().Value;

        options.KnownIPNetworks.Should().Equal("172.16.0.0/12");
        options.Limit.Should().Be(1, "one trusted hop, which is the default");
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────────────────

    private static ValidateOptionsResult Validate(string environment, TrustedProxyOptions options) =>
        new TrustedProxyOptionsValidator(new StubEnvironment(environment))
            .Validate(Options.DefaultName, options);

    private static TrustedProxyOptions Bind(params (string Key, string Value)[] settings)
    {
        var configuration = Configuration(settings);
        var options = new TrustedProxyOptions();

        configuration.GetSection(TrustedProxyOptions.SectionName).Bind(options);

        return options;
    }

    private static IConfigurationRoot Configuration((string Key, string Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

    private static ServiceProvider BuildHostLikeProvider(
        string environment,
        params (string Key, string Value)[] settings)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IHostEnvironment>(new StubEnvironment(environment));

        services.AddOptions<TrustedProxyOptions>()
            .Bind(Configuration(settings).GetSection(TrustedProxyOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<TrustedProxyOptions>, TrustedProxyOptionsValidator>();

        return services.BuildServiceProvider();
    }

    private sealed class StubEnvironment : IHostEnvironment
    {
        public StubEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "Diaspora.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
