namespace Diaspora.Modules.Media.Options;

/// <summary>How the processing pipeline behaves.</summary>
public sealed class MediaProcessingOptions
{
    public const string SectionName = "Media:Processing";

    /// <summary>
    /// Whether the pipeline runs inline with the upload or on the background worker.
    /// <para>
    /// Inline is the default and is right for the platform's actual traffic: the overwhelming
    /// majority of uploads are one small photograph, and a member who waits 200 ms and gets a
    /// working thumbnail has a better experience than one who waits 0 ms and sees a spinner
    /// resolve seconds later. A deployment that adds video transcoding should turn this off —
    /// a transcode is minutes, and no request should hold a connection that long.
    /// </para>
    /// </summary>
    public bool ProcessInline { get; set; } = true;

    /// <summary>
    /// How long an inline pipeline may take before it is abandoned to the background worker.
    /// <para>
    /// The escape hatch that makes inline processing safe. A pathological file that takes
    /// thirty seconds to decode does not hold a request thread for thirty seconds; it is
    /// handed to the queue and the member gets their asset in the pending state.
    /// </para>
    /// </summary>
    public int InlineTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Whether this deployment runs <c>MediaProcessingWorker</c> at all.
    /// <para>
    /// On by default. Without it, any job the inline pipeline abandons on
    /// <see cref="InlineTimeoutSeconds"/> stays pending for ever — so a deployment turning this off
    /// is choosing to process nothing that could not be processed inline.
    /// </para>
    /// <para>
    /// <b>Added in PSR-1 Final Certification</b>, with <c>Media:Upload:EnableSweeper</c> — see
    /// DEBT-049.
    /// </para>
    /// </summary>
    public bool EnableWorker { get; set; } = true;

    /// <summary>How often the background worker looks for pending jobs.</summary>
    public int WorkerIntervalSeconds { get; set; } = 15;

    /// <summary>How many jobs the worker takes per pass.</summary>
    public int WorkerBatchSize { get; set; } = 10;

    /// <summary>
    /// Whether the size ladder is generated at upload or on first request.
    /// <para>
    /// At upload, by default. The alternative — generate on demand — sounds cheaper and is
    /// worse in the one case that matters: a gallery of forty photographs rendered for the
    /// first time becomes forty simultaneous decode operations while somebody watches a blank
    /// screen.
    /// </para>
    /// </summary>
    public bool GenerateVariantsEagerly { get; set; } = true;

    /// <summary>
    /// Whether a missing capability fails the upload.
    /// <para>
    /// False, and it should stay false. An upload that fails because the deployment has no
    /// AVIF encoder is an upload that fails for a reason the member cannot understand or act
    /// on. The asset is stored, the step is recorded as unsupported, and the gap is visible in
    /// the job.
    /// </para>
    /// </summary>
    public bool FailOnUnsupportedOperation { get; set; }
}
