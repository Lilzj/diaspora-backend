using Diaspora.Platform.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Diaspora.Observability.Validation;

/// <summary>
/// Refuses to start a production host whose telemetry goes nowhere.
/// <para>
/// <b>The platform has done the hard half of observability and none of the last inch.</b> Every
/// module contributes its own activity sources and meters through
/// <c>IObservabilityContributor</c>, the Financial module counts webhook signatures by outcome,
/// the outbox counts dead letters — and with no OTLP endpoint and the console exporter off, every
/// one of those measurements is computed and thrown away. The counter the webhook handler calls
/// the signal worth alerting on reaches nothing at all.
/// </para>
/// <para>
/// <b>Why a refusal rather than a warning.</b> Blind telemetry has no symptom. Nothing fails,
/// nothing is slow, no request is answered incorrectly; the platform simply cannot be operated
/// through its first incident, and nobody discovers that until the incident. A warning in a boot
/// log is read once, on the deploy where somebody was already reading the log for another reason.
/// The platform already refuses to start without a JWT signing key, without a named ingress, and
/// with an instance count it cannot serve correctly. This is the same class of configuration and
/// gets the same treatment.
/// </para>
/// <para>
/// <b>What it cannot check.</b> That the collector at the other end is reachable, accepting, or
/// anybody's. A boot check that opened a connection would fail every cold start where the
/// collector happened to restart first, and the validator seam requires these checks to be cheap
/// and to touch nothing. So this validates a declaration, and a declaration that is wrong while
/// still parsing — a collector decommissioned last month — is a gap by construction.
/// </para>
/// <para>
/// The interface is spelled out rather than imported because
/// <c>Microsoft.Extensions.Options</c> ships an <c>IStartupValidator</c> of its own, and this file
/// needs that namespace for <see cref="IOptions{TOptions}"/>.
/// </para>
/// </summary>
public sealed class ObservabilityExportValidator : Platform.Validation.IStartupValidator
{
    public string Name => nameof(ObservabilityExportValidator);

    public IEnumerable<StartupFinding> Validate(StartupValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Defaults when nothing is registered, which is the honest reading: a host where
        // AddObservability was never called exports nothing, and a Production host that exports
        // nothing is what this validator exists to stop — whether that is because the setting is
        // blank or because the wiring is absent.
        var target = ObservabilityExportTarget.Read(
            context.Services.GetService<IOptions<OpenTelemetryOptions>>()?.Value
            ?? new OpenTelemetryOptions());

        if (target.EndpointIsDeclaredButUnusable)
        {
            yield return StartupFinding.Error(
                "SV9001",
                ObservabilityExportTarget.OtlpEndpointKey,
                DescribeTheUnusableEndpoint(target));

            // In every environment, and alone. Somebody meant telemetry to go somewhere, so the
            // value is the diagnosis wherever it was typed — and every check below asks whether
            // telemetry is configured at all, which would restate this as a second finding about
            // the same line of configuration.
            yield break;
        }

        var environment = context.Services.GetService<IHostEnvironment>();

        if (!IsDeployed(environment))
        {
            yield break;
        }

        if (!target.ExportsAnything)
        {
            // Production refuses; any other deployed environment says so and starts. Production is
            // where the cost of being blind is real and where the audit asked for a refusal. A
            // staging host that cannot see itself is worth a line in its boot log, but an error
            // there would block the environment whose whole purpose is to be rehearsed in.
            yield return environment!.IsProduction()
                ? StartupFinding.Error("SV9002", ObservabilityExportTarget.OtlpEndpointKey, NothingExports)
                : StartupFinding.Warning("SV9002", ObservabilityExportTarget.OtlpEndpointKey, NothingExports);

            yield break;
        }

        // Reached only when something exports and it is not OTLP, which leaves the console.
        if (target.Otlp is null && environment!.IsProduction())
        {
            yield return StartupFinding.Warning(
                "SV9003",
                ObservabilityExportTarget.UseConsoleExporterKey,
                "The console exporter is the only exporter configured, so every span and every "
                + "metric is written to stdout beside the logs. That is a fair choice for a short "
                + "investigation and an expensive default: it multiplies log volume by the "
                + "platform's whole span rate, and a log aggregator cannot aggregate a histogram. "
                + "Point OpenTelemetry__OtlpEndpoint at a collector when one is available.");
        }
    }

    /// <summary>
    /// The sentence for a deployment that exports nothing, written once because the only thing
    /// that differs between Production and the rest is whether the host is allowed to continue.
    /// </summary>
    private const string NothingExports =
        "No exporter is configured, so every trace and every metric this host produces is computed "
        + "and discarded — including the counters that exist to be alerted on: unverified webhook "
        + "signatures, outbox dead-lettering, rate-limit rejections and unexpected errors. Set "
        + "OpenTelemetry__OtlpEndpoint to a collector's address, for example "
        + "http://otel.example.com:4318, or set OpenTelemetry__UseConsoleExporter to true to accept "
        + "telemetry in the log stream instead.";

    /// <summary>
    /// Development and Testing are not deployments.
    /// <para>
    /// Testing matters most here: the integration suite boots the real host in it, with no endpoint
    /// configured and none wanted, and the suite asserts that a correct platform produces an empty
    /// report. A finding there would fire on every boot of a correct platform, which is how a
    /// report becomes the thing people scroll past on the way to the error they came for.
    /// </para>
    /// <para>
    /// A null environment is treated the same way. That is the shape of a unit test constructing a
    /// context by hand, and refusing on it would make the validator fail closed against callers
    /// that are not deployments at all.
    /// </para>
    /// </summary>
    private static bool IsDeployed(IHostEnvironment? environment) =>
        environment is not null &&
        !environment.IsDevelopment() &&
        !environment.IsEnvironment("Testing");

    private static string DescribeTheUnusableEndpoint(ObservabilityExportTarget target) =>
        target.Problem switch
        {
            ObservabilityEndpointProblem.UnsupportedScheme =>
                $"{ObservabilityExportTarget.OtlpEndpointKey} is {Quote(target.DeclaredEndpoint)}, "
                + "whose scheme is not http or https. A value written as host and port with the "
                + "scheme left off is a structurally valid URI with an empty host: nothing throws, "
                + "the exporter is attached to it, and no span or metric is ever delivered. Add the "
                + "scheme, as in http://collector:4317. OTLP over gRPC uses an http endpoint too, "
                + "so grpc:// is not the alternative.",

            _ =>
                $"{ObservabilityExportTarget.OtlpEndpointKey} is {Quote(target.DeclaredEndpoint)}, "
                + "which is not an absolute URI, so no exporter could be attached and every trace "
                + "and metric is discarded. Set it to the collector's full address including a "
                + "scheme, as in http://collector:4318, or clear it so the host reports the absence "
                + "rather than the typo."
        };

    /// <summary>
    /// Wraps the configured value so that trailing whitespace, a stray quote or an empty-looking
    /// value is visible in a log line rather than invisible in one.
    /// </summary>
    private static string Quote(string? value) => "\"" + value + "\"";
}
