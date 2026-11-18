using Diaspora.Observability;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.UnitTests.Observability;

/// <summary>
/// What "an exporter is configured" means, pinned at the one place that decides it.
/// <para>
/// <b>The test that matters most here is the one about a missing scheme.</b> BARCH-008 was raised
/// as "no exporter configured", and the obvious remedy is a check for a blank setting. That check
/// would pass a deployment whose endpoint reads <c>collector:4317</c> — a value that parses as an
/// absolute URI with an empty host, that the OTLP exporter accepts without complaint, and that
/// delivers nothing. The finding's symptom with none of its stated cause, and invisible.
/// </para>
/// </summary>
public class ObservabilityExportTargetTests
{
    /// <summary>
    /// The shipped defaults export nothing, which is the finding — and the right default, since a
    /// platform with a built-in collector address would be one that sends somebody else's
    /// telemetry somewhere by accident. The protection is the refusal, not the default.
    /// </summary>
    [Fact]
    public void The_shipped_defaults_export_nothing()
    {
        var target = Read();

        target.DeclaredEndpoint.Should().BeNull();
        target.Otlp.Should().BeNull();
        target.Problem.Should().Be(ObservabilityEndpointProblem.None);
        target.ExportsAnything.Should().BeFalse();
        target.EndpointIsDeclaredButUnusable.Should().BeFalse(
            "nothing was declared, so there is no value to report back to anybody");
    }

    /// <summary>
    /// <c>appsettings.json</c> carries an empty string rather than omitting the key, and an
    /// environment variable can carry whitespace. All of it has to mean "nowhere", or the check
    /// that fires in Production depends on whether somebody deleted a line.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void A_blank_endpoint_is_the_same_as_no_endpoint(string blank)
    {
        var target = Read(endpoint: blank);

        target.DeclaredEndpoint.Should().BeNull();
        target.ExportsAnything.Should().BeFalse();
        target.EndpointIsDeclaredButUnusable.Should().BeFalse();
    }

    /// <summary>
    /// The section is absent from <c>appsettings.Production.json</c> and from <c>fly.toml</c>, so
    /// the binder returning null for it is the live production path rather than an edge case. It
    /// has to arrive at the same answer as a blank string.
    /// </summary>
    [Fact]
    public void An_absent_section_binds_to_the_blind_defaults()
    {
        var options = new ConfigurationBuilder().Build()
            .GetSection(OpenTelemetryOptions.SectionName)
            .Get<OpenTelemetryOptions>() ?? new OpenTelemetryOptions();

        var target = ObservabilityExportTarget.Read(options);

        target.ExportsAnything.Should().BeFalse();
        target.EndpointIsDeclaredButUnusable.Should().BeFalse();
    }

    /// <summary>
    /// The section binds past the narrative <c>//</c> keys the platform's configuration files use
    /// for documentation, which is worth one test because the block this type reads has three of
    /// them and a binder configured to object to unknown keys would refuse to start the host.
    /// </summary>
    [Fact]
    public void The_narrative_comment_keys_do_not_disturb_the_binding()
    {
        var options = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["OpenTelemetry://"] = "Empty here is the shipped default.",
                ["OpenTelemetry://Scheme"] = "The scheme is not optional.",
                ["OpenTelemetry:OtlpEndpoint"] = "http://collector:4318",
                ["OpenTelemetry:UseConsoleExporter"] = "false"
            })
            .Build()
            .GetSection(OpenTelemetryOptions.SectionName)
            .Get<OpenTelemetryOptions>();

        options.Should().NotBeNull();
        ObservabilityExportTarget.Read(options!).Otlp.Should().NotBeNull();
    }

    /// <summary>
    /// <b>The finding this type exists for.</b> Host and port with the scheme left off is the
    /// natural way to write a collector address and it is wrong in a way nothing reports.
    /// </summary>
    [Theory]
    [InlineData("collector:4317")]
    [InlineData("localhost:4318")]
    [InlineData("otel.example.com:4317")]
    public void An_endpoint_with_no_scheme_but_a_port_is_unusable_rather_than_accepted(string value)
    {
        // The trap, stated at the level it lives at: this is a valid absolute URI. Nothing about
        // it is malformed, which is why a check on emptiness and a check on parse success both
        // pass it.
        var parsed = new Uri(value);
        parsed.IsAbsoluteUri.Should().BeTrue();
        parsed.Host.Should().BeEmpty("which is the whole problem — there is nowhere to send to");

        var target = Read(endpoint: value);

        target.Otlp.Should().BeNull();
        target.Problem.Should().Be(ObservabilityEndpointProblem.UnsupportedScheme);
        target.ExportsAnything.Should().BeFalse();
        target.EndpointIsDeclaredButUnusable.Should().BeTrue();
        target.DeclaredEndpoint.Should().Be(value, "the operator has to be shown what they typed");
    }

    /// <summary>
    /// A bare name does not parse at all, and the old code turned that into a
    /// <see cref="UriFormatException"/> raised from inside OpenTelemetry while the tracer provider
    /// was being built. Both halves are asserted, because the second is the reason the first
    /// matters.
    /// </summary>
    [Theory]
    [InlineData("otel-collector")]
    [InlineData("collector.example.com")]
    [InlineData("http://")]
    public void An_endpoint_that_does_not_parse_is_reported_rather_than_thrown(string value)
    {
        var constructing = () => new Uri(value);
        constructing.Should().Throw<UriFormatException>(
            "this is what the endpoint used to be handed to, inside the exporter's options callback");

        var target = Read(endpoint: value);

        target.Problem.Should().Be(ObservabilityEndpointProblem.Unparseable);
        target.Otlp.Should().BeNull();
        target.EndpointIsDeclaredButUnusable.Should().BeTrue();
    }

    /// <summary>
    /// OTLP travels over http or https whichever wire protocol is in use — gRPC here is gRPC over
    /// HTTP/2, whose endpoint is still an http one. A <c>grpc://</c> endpoint is somebody guessing,
    /// and it parses.
    /// </summary>
    [Theory]
    [InlineData("grpc://collector:4317")]
    [InlineData("tcp://collector:4317")]
    [InlineData("file:///var/telemetry")]
    public void An_endpoint_on_another_scheme_is_unusable(string value)
    {
        var target = Read(endpoint: value);

        target.Problem.Should().Be(ObservabilityEndpointProblem.UnsupportedScheme);
        target.ExportsAnything.Should().BeFalse();
    }

    [Theory]
    [InlineData("http://collector:4318")]
    [InlineData("https://otlp.example.com")]
    [InlineData("http://collector:4318/v1/traces")]
    [InlineData("HTTP://collector:4318")]
    public void A_full_endpoint_is_usable(string value)
    {
        var target = Read(endpoint: value);

        target.Problem.Should().Be(ObservabilityEndpointProblem.None);
        target.Otlp.Should().NotBeNull();
        target.Otlp!.Host.Should().NotBeEmpty();
        target.ExportsAnything.Should().BeTrue();
        target.EndpointIsDeclaredButUnusable.Should().BeFalse();
    }

    /// <summary>
    /// Environment variables arrive with whatever whitespace the deployment file gave them, and a
    /// trailing newline is not a different collector.
    /// </summary>
    [Fact]
    public void An_endpoint_is_trimmed_before_it_is_parsed()
    {
        var target = Read(endpoint: "  http://collector:4318  ");

        target.Otlp.Should().NotBeNull();
        target.DeclaredEndpoint.Should().Be("http://collector:4318");
    }

    /// <summary>
    /// The console exporter is an exporter. Writing spans to stdout is a poor production default
    /// and it is not blindness, so the refusal must not fire on a host that chose it.
    /// </summary>
    [Fact]
    public void The_console_exporter_counts_as_an_exporter()
    {
        var target = Read(console: true);

        target.Otlp.Should().BeNull();
        target.ConsoleExporterRequested.Should().BeTrue();
        target.ExportsAnything.Should().BeTrue();
    }

    /// <summary>
    /// A collector wins over the console, which is the behaviour the previous implementation had
    /// and the reason it computed its console flag as "and no OTLP endpoint".
    /// </summary>
    [Fact]
    public void A_collector_and_the_console_together_still_export()
    {
        var target = Read(endpoint: "http://collector:4318", console: true);

        target.Otlp.Should().NotBeNull();
        target.ExportsAnything.Should().BeTrue();
    }

    /// <summary>
    /// The regression test for the crash. Registration composes the whole OpenTelemetry pipeline
    /// and must survive every endpoint a deployment can type, because the alternative is a host
    /// that cannot start far enough to tell anybody why.
    /// </summary>
    [Theory]
    [InlineData("otel-collector")]
    [InlineData("collector:4317")]
    [InlineData("")]
    [InlineData("http://collector:4318")]
    public void Registration_survives_any_endpoint_a_deployment_can_type(string endpoint)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var registering = () => services.AddObservability(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [ObservabilityExportTarget.OtlpEndpointKey] = endpoint
                })
                .Build());

        registering.Should().NotThrow();
    }

    private static ObservabilityExportTarget Read(string? endpoint = null, bool console = false) =>
        ObservabilityExportTarget.Read(new OpenTelemetryOptions
        {
            OtlpEndpoint = endpoint ?? string.Empty,
            UseConsoleExporter = console
        });
}
