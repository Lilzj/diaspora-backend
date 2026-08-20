using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Diaspora.Modules.Media.Observability;

/// <summary>
/// The module's live telemetry instruments.
/// <para>
/// Declared alongside the contributor that subscribes the host's pipeline to them, and both
/// are required — declaring a source name without creating an <see cref="ActivitySource"/>
/// behind it subscribes the tracer to silence, which is the B2 mistake the module extension
/// guide calls out by name.
/// </para>
/// <para>
/// The instruments were chosen for the questions this module will actually be asked in
/// production, and they are not the obvious ones. "How many uploads" is a vanity metric.
/// "How many uploads were <em>refused</em>, and for which reason" tells you whether the
/// signature check is rejecting real files. "How many bytes are stored" tells you the bill.
/// "How many assets were never claimed" tells you a vertical is leaking storage. And
/// <see cref="UnsupportedOperations"/> tells you the deployment is missing a capability — a
/// number that should be zero in production and will be non-zero the day somebody forgets to
/// deploy the codec sidecar.
/// </para>
/// </summary>
public static class MediaTelemetry
{
    public const string SourceName = "Diaspora.Media";

    public const string Version = "1.0.0";

    public static readonly ActivitySource Activity = new(SourceName, Version);

    public static readonly Meter Meter = new(SourceName, Version);

    /// <summary>Upload sessions opened, tagged by protocol and media kind.</summary>
    public static readonly Counter<long> UploadsStarted =
        Meter.CreateCounter<long>(
            "diaspora.media.uploads_started",
            unit: "{upload}",
            description: "Upload sessions opened.");

    /// <summary>Uploads that produced an asset.</summary>
    public static readonly Counter<long> UploadsCompleted =
        Meter.CreateCounter<long>(
            "diaspora.media.uploads_completed",
            unit: "{upload}",
            description: "Upload sessions that produced an asset.");

    /// <summary>
    /// Uploads refused, tagged by reason.
    /// <para>
    /// The most operationally useful counter in the module. A spike in
    /// <c>signature-mismatch</c> is either an attack or a format the signature table does not
    /// know; a spike in <c>too-large</c> is a client that stopped compressing. The two need
    /// opposite responses, and an undifferentiated "failed uploads" number tells you neither.
    /// </para>
    /// </summary>
    public static readonly Counter<long> UploadsRejected =
        Meter.CreateCounter<long>(
            "diaspora.media.uploads_rejected",
            unit: "{upload}",
            description: "Uploads refused, tagged by reason.");

    /// <summary>Bytes accepted into storage. Divided by time, the bill.</summary>
    public static readonly Counter<long> BytesStored =
        Meter.CreateCounter<long>(
            "diaspora.media.bytes_stored",
            unit: "By",
            description: "Bytes written to storage.");

    /// <summary>Bytes served, tagged by variant purpose.</summary>
    public static readonly Counter<long> BytesServed =
        Meter.CreateCounter<long>(
            "diaspora.media.bytes_served",
            unit: "By",
            description: "Bytes read out of storage and returned to clients.");

    /// <summary>Chunks received. Divided by sessions, how fragmented uploads really are.</summary>
    public static readonly Counter<long> ChunksReceived =
        Meter.CreateCounter<long>(
            "diaspora.media.chunks_received",
            unit: "{chunk}",
            description: "Upload parts received.");

    /// <summary>Processing steps run, tagged by operation and outcome.</summary>
    public static readonly Counter<long> ProcessingSteps =
        Meter.CreateCounter<long>(
            "diaspora.media.processing_steps",
            unit: "{step}",
            description: "Processing steps executed, tagged by operation and status.");

    /// <summary>Per-step latency. Where "processing is slow" becomes actionable.</summary>
    public static readonly Histogram<double> ProcessingStepDuration =
        Meter.CreateHistogram<double>(
            "diaspora.media.processing_step_duration",
            unit: "ms",
            description: "Time each processing step took.");

    /// <summary>
    /// Steps no registered processor could perform.
    /// <para>
    /// Should be zero in a fully-provisioned deployment. Non-zero means a capability is
    /// missing — which is a legitimate configuration, and exactly the sort of thing that
    /// otherwise goes unnoticed until somebody asks why no thumbnails exist.
    /// </para>
    /// </summary>
    public static readonly Counter<long> UnsupportedOperations =
        Meter.CreateCounter<long>(
            "diaspora.media.unsupported_operations",
            unit: "{step}",
            description: "Processing steps no registered processor could perform.");

    /// <summary>Assets quarantined by a scanner.</summary>
    public static readonly Counter<long> AssetsQuarantined =
        Meter.CreateCounter<long>(
            "diaspora.media.assets_quarantined",
            unit: "{asset}",
            description: "Assets a malware scanner flagged.");

    /// <summary>Signed URLs minted, tagged by media kind.</summary>
    public static readonly Counter<long> SignedUrlsIssued =
        Meter.CreateCounter<long>(
            "diaspora.media.signed_urls_issued",
            unit: "{url}",
            description: "Short-lived access links minted.");

    /// <summary>
    /// Signed links refused at redemption, tagged by reason.
    /// <para>
    /// Expiry is normal and expected. A rising <c>invalid</c> count is somebody probing the
    /// signature, which is worth an alert on its own.
    /// </para>
    /// </summary>
    public static readonly Counter<long> SignedUrlsRejected =
        Meter.CreateCounter<long>(
            "diaspora.media.signed_urls_rejected",
            unit: "{url}",
            description: "Signed links refused, tagged by reason.");

    /// <summary>
    /// Assets swept because nothing ever claimed them.
    /// <para>
    /// A rising count means a vertical is uploading and forgetting to attach — a silent
    /// storage leak that is invisible in every other metric because the uploads all succeeded.
    /// </para>
    /// </summary>
    public static readonly Counter<long> OrphansCollected =
        Meter.CreateCounter<long>(
            "diaspora.media.orphans_collected",
            unit: "{asset}",
            description: "Unclaimed assets removed by the sweeper.");
}
