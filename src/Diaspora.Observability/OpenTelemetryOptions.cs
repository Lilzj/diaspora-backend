namespace Diaspora.Observability;

/// <summary>
/// Where this host's traces and metrics go, and what it calls itself when they get there.
/// <para>
/// <b>This section existed in <c>appsettings.json</c> and nowhere else.</b> The platform's
/// configuration reference is generated from the options types that declare a
/// <see cref="SectionName"/>, so a section read through raw configuration keys is a section an
/// operator cannot find by looking — and until BARCH-008 this was the only one of fifty-seven
/// that worked that way. It is also now the one that decides whether a production host is
/// allowed to start, which makes being absent from the reference the worse half of the finding.
/// </para>
/// <para>
/// <b>Three settings, and the defaults are deliberately the blind ones.</b> A platform that
/// shipped a default collector address would be a platform that sent somebody else's telemetry
/// somewhere by accident. The protection is not a default, it is
/// <see cref="Validation.ObservabilityExportValidator"/> refusing to start a deployment that left
/// them alone.
/// </para>
/// </summary>
public sealed class OpenTelemetryOptions
{
    public const string SectionName = "OpenTelemetry";

    /// <summary>
    /// The OTLP collector to export traces and metrics to, with its scheme. Blank means nowhere,
    /// which a Production host refuses to start on.
    /// <para>
    /// The scheme is not optional and leaving it off fails quietly: <c>collector:4317</c> is a
    /// valid absolute URI whose scheme is <c>collector</c> and whose host is empty, so the
    /// exporter attaches to it and delivers nothing. That is why the value is parsed rather than
    /// tested for emptiness — see <see cref="ObservabilityEndpointProblem"/>.
    /// </para>
    /// </summary>
    public string OtlpEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// The <c>service.name</c> resource attribute every span and metric carries. One collector
    /// usually serves several services, and this is how they are told apart.
    /// </summary>
    public string ServiceName { get; set; } = "Diaspora.Api";

    /// <summary>
    /// Writes traces and metrics to stdout instead of to a collector, for a developer with no
    /// collector to hand.
    /// <para>
    /// It counts as an exporter, so it satisfies the boot check — and in Production it earns a
    /// warning of its own: spans into the log stream multiply log volume by the platform's whole
    /// span rate, and a log aggregator cannot aggregate a histogram.
    /// </para>
    /// </summary>
    public bool UseConsoleExporter { get; set; }
}
