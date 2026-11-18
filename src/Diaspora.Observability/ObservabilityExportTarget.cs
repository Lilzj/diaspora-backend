namespace Diaspora.Observability;

/// <summary>
/// Why a declared OTLP endpoint cannot be used.
/// <para>
/// Two members, because a configuration string can only produce two. An absolute http or https
/// URI always carries an authority — <c>Uri.TryCreate</c> refuses <c>http://</c>,
/// <c>http:///x</c> and <c>http://:4317</c> outright — so "parsed, but no host" is unreachable
/// once the scheme has been checked, and a third member would name a branch no deployment can
/// enter. Measured on .NET 10.
/// </para>
/// </summary>
public enum ObservabilityEndpointProblem
{
    /// <summary>The endpoint is usable, or none was declared.</summary>
    None,

    /// <summary>
    /// Not an absolute URI: <c>otel-collector</c>, <c>collector.example.com</c>, <c>http://</c>.
    /// <para>
    /// This is the case that used to take the host down. <c>new Uri(value)</c> sat inside the OTLP
    /// exporter's own options callback, so a value of this shape threw
    /// <see cref="UriFormatException"/> when OpenTelemetry built the tracer provider during host
    /// start-up: from an OpenTelemetry stack frame, naming neither the configuration key nor the
    /// value that was wrong. <see cref="ObservabilityExportTarget.Read"/> never throws, and the
    /// refusal now comes from a validator that names both — and that runs first among hosted
    /// services, so it is reached before the provider is built.
    /// </para>
    /// </summary>
    Unparseable,

    /// <summary>
    /// An absolute URI whose scheme is neither http nor https. <b>The quiet one.</b>
    /// <para>
    /// <c>collector:4317</c> is the obvious way to write a collector address and it is wrong. It
    /// parses: scheme <c>collector</c>, empty host, port -1. Nothing throws, the OTLP exporter is
    /// attached to it, and every span and every metric is delivered nowhere. A check that asked
    /// only whether the setting was blank would pass this deployment while it emitted to nothing,
    /// which is the exact failure BARCH-008 describes — so the check parses instead of asking.
    /// </para>
    /// </summary>
    UnsupportedScheme
}

/// <summary>
/// Where this host's traces and metrics go.
/// <para>
/// <b>One statement of what "an exporter is configured" means, because two would drift.</b>
/// <see cref="DependencyInjection.AddObservability"/> decides which exporter to attach and
/// <c>ObservabilityExportValidator</c> decides whether to let the host start at all. Those have to
/// be the same decision: a validator that read the keys for itself could refuse a deployment whose
/// telemetry works, or pass one whose telemetry does not — and the second mistake is invisible,
/// which is the whole reason the finding existed.
/// </para>
/// <para>
/// <b>A decision about <see cref="OpenTelemetryOptions"/>, not a second reading of it.</b> The
/// options type says what a deployment set; this says what that amounts to. Keeping them apart is
/// what lets the settings stay plain strings and booleans — bindable, documentable, and exactly
/// what the operator typed — while the interpretation of them lives in one place with a name.
/// </para>
/// </summary>
public sealed class ObservabilityExportTarget
{
    /// <summary>
    /// The configuration paths, for a finding's subject and for a message an operator reads.
    /// <para>
    /// Built with <c>nameof</c> so that renaming a setting cannot leave a boot log pointing at a
    /// key that no longer exists — which is the failure a hard-coded string would produce, in the
    /// one log line somebody is reading because the host would not start.
    /// </para>
    /// </summary>
    public const string OtlpEndpointKey =
        OpenTelemetryOptions.SectionName + ":" + nameof(OpenTelemetryOptions.OtlpEndpoint);

    /// <inheritdoc cref="OtlpEndpointKey"/>
    public const string UseConsoleExporterKey =
        OpenTelemetryOptions.SectionName + ":" + nameof(OpenTelemetryOptions.UseConsoleExporter);

    private ObservabilityExportTarget(
        string? declaredEndpoint,
        Uri? otlp,
        ObservabilityEndpointProblem problem,
        bool consoleExporterRequested)
    {
        DeclaredEndpoint = declaredEndpoint;
        Otlp = otlp;
        Problem = problem;
        ConsoleExporterRequested = consoleExporterRequested;
    }

    /// <summary>The endpoint as configured, trimmed. Null when absent or blank.</summary>
    public string? DeclaredEndpoint { get; }

    /// <summary>
    /// The endpoint to export to, and non-null only when it is usable — so a caller that attaches
    /// an exporter whenever this is set cannot attach one to an address that can receive nothing.
    /// </summary>
    public Uri? Otlp { get; }

    /// <summary>Why <see cref="Otlp"/> is null despite a declared endpoint.</summary>
    public ObservabilityEndpointProblem Problem { get; }

    /// <summary>Whether configuration asks for the console exporter.</summary>
    public bool ConsoleExporterRequested { get; }

    /// <summary>Whether anything at all receives this host's traces and metrics.</summary>
    public bool ExportsAnything => Otlp is not null || ConsoleExporterRequested;

    /// <summary>
    /// Somebody set the endpoint and it cannot be used.
    /// <para>
    /// Worth separating from "nothing is configured", because the two need different sentences
    /// said to different people. Nothing configured is an omission, and the operator should be
    /// invited to set a value. A value that cannot be used is a typo, and the operator needs to be
    /// shown the value they typed — inviting them to set one they have already set is how a boot
    /// log loses somebody an hour.
    /// </para>
    /// </summary>
    public bool EndpointIsDeclaredButUnusable => DeclaredEndpoint is not null && Otlp is null;

    /// <summary>
    /// Decides what a configured deployment actually exports. Pure, and never throws: a value
    /// nobody can use has to come back as a finding rather than as an exception, because the whole
    /// point is to be able to say which key was wrong.
    /// </summary>
    public static ObservabilityExportTarget Read(OpenTelemetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var console = options.UseConsoleExporter;

        if (string.IsNullOrWhiteSpace(options.OtlpEndpoint))
        {
            return new ObservabilityExportTarget(
                null, null, ObservabilityEndpointProblem.None, console);
        }

        var declared = options.OtlpEndpoint.Trim();

        if (!Uri.TryCreate(declared, UriKind.Absolute, out var endpoint))
        {
            return new ObservabilityExportTarget(
                declared, null, ObservabilityEndpointProblem.Unparseable, console);
        }

        // The scheme is checked before anything else about the parsed URI, because the scheme is
        // what the common mistake gets wrong and because the rest of the URI is meaningless once
        // it is wrong — 'collector:4317' has a scheme of 'collector' and no host at all, so a
        // host check here would report the symptom and hide the cause.
        //
        // http and https are the whole supported set. OTLP over gRPC is gRPC over HTTP/2 and its
        // endpoint is still an http:// one, so 'grpc://' is a guess rather than a protocol choice.
        if (!string.Equals(endpoint.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return new ObservabilityExportTarget(
                declared, null, ObservabilityEndpointProblem.UnsupportedScheme, console);
        }

        return new ObservabilityExportTarget(
            declared, endpoint, ObservabilityEndpointProblem.None, console);
    }
}
