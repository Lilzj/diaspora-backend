using System.Reflection;
using Diaspora.Observability;
using Diaspora.Observability.Validation;
using Diaspora.Platform.Validation;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Observability;

/// <summary>
/// The refusal that makes BARCH-008 impossible to ship rather than merely recorded.
/// <para>
/// <b>Blindness has no symptom, which is the whole argument for checking at boot.</b> A deployment
/// exporting no telemetry does not fail, slow down or answer anything incorrectly. Every counter
/// the platform builds — webhook signatures by outcome, outbox dead letters, rate-limit
/// rejections — is computed and dropped, and the first person to notice is whoever is trying to
/// run an incident with nothing but logs. There is no runtime signal to alert on, so the only
/// moment this can be caught is the one moment somebody is watching.
/// </para>
/// <para>
/// The environment arms are load-bearing in both directions: Production must refuse, and Testing
/// must stay silent, because the integration suite boots a real host in Testing with no endpoint
/// and asserts the startup report is empty.
/// </para>
/// </summary>
public class ObservabilityExportValidatorTests
{
    private readonly ObservabilityExportValidator _validator = new();

    /// <summary>
    /// The finding. A production host that exports nothing does not start.
    /// </summary>
    [Fact]
    public void Production_with_no_exporter_is_refused()
    {
        var findings = Validate("Production");

        var finding = findings.Should().ContainSingle().Which;

        finding.Code.Should().Be("SV9002");
        finding.Severity.Should().Be(StartupFindingSeverity.Error);
        finding.Message.Should().Contain("OpenTelemetry__OtlpEndpoint",
            "the message is read in a crashed boot log on Fly, where the setting is an " +
            "environment variable rather than a configuration path");
        finding.Message.Should().Contain("webhook",
            "naming a counter that is being discarded is what makes the cost concrete");
    }

    [Fact]
    public void Production_with_a_collector_reports_nothing()
    {
        Validate("Production", endpoint: "http://collector:4318")
            .Should().BeEmpty();
    }

    /// <summary>
    /// The console exporter is an exporter, so the host starts — and it is a poor production
    /// default, so it says so once. Warning rather than Error: somebody chose it, and telemetry
    /// reaching stdout is not telemetry reaching nothing.
    /// </summary>
    [Fact]
    public void Production_with_only_the_console_exporter_starts_and_says_so()
    {
        var finding = Validate("Production", console: true).Should().ContainSingle().Which;

        finding.Code.Should().Be("SV9003");
        finding.Severity.Should().Be(StartupFindingSeverity.Warning);
    }

    /// <summary>
    /// A collector wins over the console, and the console warning must not fire alongside it —
    /// a warning on a correct deployment is the kind nobody reads.
    /// </summary>
    [Fact]
    public void Production_with_both_reports_nothing()
    {
        Validate("Production", endpoint: "https://otlp.example.com", console: true)
            .Should().BeEmpty();
    }

    /// <summary>
    /// A value that cannot be used is a typo, and a typo is a mistake in every environment.
    /// Nobody sets this key by accident, so wherever it appears the intent was telemetry and the
    /// outcome is none.
    /// </summary>
    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void An_unusable_endpoint_is_refused_in_every_environment(string environment)
    {
        var finding = Validate(environment, endpoint: "collector:4317")
            .Should().ContainSingle().Which;

        finding.Code.Should().Be("SV9001");
        finding.Severity.Should().Be(StartupFindingSeverity.Error);
    }

    /// <summary>
    /// One mistake, one finding. The unusable endpoint also means nothing is exported, and
    /// reporting both would put two findings about one line of configuration in front of somebody
    /// deciding which of them to fix.
    /// </summary>
    [Fact]
    public void An_unusable_endpoint_is_not_also_reported_as_an_absent_one()
    {
        Validate("Production", endpoint: "otel-collector")
            .Should().ContainSingle()
            .Which.Code.Should().Be("SV9001");
    }

    /// <summary>
    /// The message has to carry the value, because the operator's next action is to look at what
    /// they typed. A finding that says the endpoint is invalid without quoting it invites them to
    /// set a value they have already set.
    /// </summary>
    [Fact]
    public void The_unusable_endpoint_finding_quotes_what_was_typed()
    {
        var finding = Validate("Production", endpoint: "collector:4317").Single();

        finding.Message.Should().Contain("collector:4317");
        finding.Message.Should().Contain("http://collector:4317",
            "showing the corrected form is the shortest path from the log line to a working " +
            "deployment");
        finding.Subject.Should().Be(ObservabilityExportTarget.OtlpEndpointKey);
    }

    /// <summary>
    /// A bare name and a scheme-less host and port fail for different reasons and need different
    /// sentences — the first did not parse, the second parsed into something with no host.
    /// </summary>
    [Fact]
    public void The_two_unusable_shapes_are_explained_differently()
    {
        var unparseable = Validate("Production", endpoint: "otel-collector").Single().Message;
        var schemeless = Validate("Production", endpoint: "collector:4317").Single().Message;

        unparseable.Should().Contain("not an absolute URI");
        schemeless.Should().Contain("empty host");
        unparseable.Should().NotBe(schemeless);
    }

    /// <summary>
    /// Development is not a deployment. A developer running the API locally has no collector and
    /// wants none, and a boot warning on every <c>dotnet run</c> is noise with a cost.
    /// </summary>
    [Fact]
    public void Development_with_no_exporter_reports_nothing()
    {
        Validate("Development").Should().BeEmpty();
    }

    /// <summary>
    /// <b>This one guards another test.</b> The integration suite boots the real host in Testing,
    /// with no endpoint configured, and asserts the whole startup report is empty — so a finding
    /// here would turn every integration run red for a platform that is correct.
    /// </summary>
    [Fact]
    public void Testing_with_no_exporter_reports_nothing()
    {
        Validate("Testing").Should().BeEmpty();
    }

    /// <summary>
    /// A staging host that cannot see itself is worth saying out loud and not worth blocking:
    /// the point of such an environment is to be rehearsed in, and refusing it would stop a
    /// rehearsal to enforce a dashboard.
    /// </summary>
    [Fact]
    public void Another_deployed_environment_with_no_exporter_warns_rather_than_refusing()
    {
        var finding = Validate("Staging").Should().ContainSingle().Which;

        finding.Code.Should().Be("SV9002");
        finding.Severity.Should().Be(StartupFindingSeverity.Warning);
    }

    /// <summary>
    /// With no environment and no configuration the validator is being called by something that
    /// is not a host. It says nothing rather than assuming the worst, because a validator that
    /// failed closed against its own unit tests would be checked by none of them.
    /// </summary>
    [Fact]
    public void A_context_with_no_environment_reports_nothing()
    {
        Validate(environment: null).Should().BeEmpty();
    }

    /// <summary>
    /// <b>Observability never wired at all fails the same way as observability wired to nothing.</b>
    /// <para>
    /// The two are indistinguishable from the outside — no span leaves the process either way —
    /// and treating an absent registration as acceptable would be the fail-open case: a host that
    /// stopped calling <c>AddObservability</c> would pass the check that exists because its
    /// telemetry goes nowhere.
    /// </para>
    /// </summary>
    [Fact]
    public void Production_with_observability_never_wired_is_refused()
    {
        var finding = Validate("Production", wireObservability: false)
            .Should().ContainSingle().Which;

        finding.Code.Should().Be("SV9002");
        finding.Severity.Should().Be(StartupFindingSeverity.Error);
    }

    [Fact]
    public void A_context_with_nothing_registered_at_all_reports_nothing()
    {
        Validate(environment: null, wireObservability: false).Should().BeEmpty();
    }

    private List<StartupFinding> Validate(
        string? environment,
        string? endpoint = null,
        bool console = false,
        bool wireObservability = true)
    {
        var services = new ServiceCollection();

        if (wireObservability)
        {
            services.AddSingleton<IOptions<OpenTelemetryOptions>>(Options.Create(
                new OpenTelemetryOptions
                {
                    OtlpEndpoint = endpoint ?? string.Empty,
                    UseConsoleExporter = console
                }));
        }

        if (environment is not null)
        {
            services.AddSingleton<IHostEnvironment>(new StubEnvironment(environment));
        }

        var registrations = services.ToList();

        return [.. _validator.Validate(new StartupValidationContext(
            services.BuildServiceProvider(),
            registrations,
            [Assembly.GetExecutingAssembly()]))];
    }

    /// <summary>
    /// The environment name is the only member any of this reads. The file provider is never
    /// touched, and giving it a working one would suggest it was.
    /// </summary>
    private sealed class StubEnvironment : IHostEnvironment
    {
        public StubEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "Diaspora.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
