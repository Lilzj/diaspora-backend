namespace Diaspora.Modules.Media.Options;

/// <summary>
/// The limits a deployment may tune.
/// <para>
/// Every value here has a domain default in <c>MediaLimits</c>, and the domain default holds
/// when configuration is absent. That ordering matters: a missing configuration section must
/// not mean "no limit", which is what happens when the limit lives only in options and the
/// binder finds nothing.
/// </para>
/// <para>
/// The verification-document ceiling is <em>not</em> here. It is five megabytes because the
/// client says so on screen, in two places, and Trust's own domain constant agrees; making it
/// configurable would let a deployment contradict its own UI.
/// </para>
/// </summary>
public sealed class MediaUploadOptions
{
    public const string SectionName = "Media:Upload";

    /// <summary>Largest single-request upload, in megabytes. Above this a session is required.</summary>
    public int DirectUploadCeilingMegabytes { get; set; } = 8;

    /// <summary>Part size for chunked uploads, in megabytes.</summary>
    public int ChunkSizeMegabytes { get; set; } = 5;

    /// <summary>How long an unfinished upload survives.</summary>
    public int SessionLifetimeHours { get; set; } = 24;

    /// <summary>
    /// How many uploads one member may have open at once.
    /// <para>
    /// A quota rather than a rate limit, because the resource being protected is staged
    /// storage rather than request throughput. Ten is generous for a housing wizard uploading
    /// a gallery and restrictive for a script opening sessions in a loop.
    /// </para>
    /// </summary>
    public int MaxConcurrentSessionsPerUser { get; set; } = 10;

    /// <summary>
    /// How long an asset nothing has claimed survives before the sweeper removes it.
    /// <para>
    /// Longer than the session lifetime on purpose. A member who uploads photographs, closes
    /// the app and finishes the listing the next morning must not find the photographs gone —
    /// every composer in the client persists a draft and expects exactly that.
    /// </para>
    /// </summary>
    public int OrphanGraceHours { get; set; } = 48;

    /// <summary>
    /// Whether this deployment runs <c>MediaSweeper</c> at all.
    /// <para>
    /// On by default. The sweeper expires abandoned upload sessions and collects orphaned assets;
    /// without it both accumulate, and the orphans are real bytes in real storage.
    /// </para>
    /// <para>
    /// <b>Added in PSR-1 Final Certification</b>, with <c>Media:Processing:EnableWorker</c> — see
    /// DEBT-049. Media was the module with two of the six unswitchable loops.
    /// </para>
    /// </summary>
    public bool EnableSweeper { get; set; } = true;

    /// <summary>How often the sweeper runs.</summary>
    public int SweepIntervalMinutes { get; set; } = 30;

    /// <summary>
    /// Whether an upload whose bytes disagree with its declared type is refused.
    /// <para>
    /// Configurable only so a deployment can diagnose a false positive against a format the
    /// signature table does not yet know. Turning it off in production removes the module's
    /// single most valuable control, and the module logs a warning at startup when it is off.
    /// </para>
    /// </summary>
    public bool EnforceSignatureValidation { get; set; } = true;
}
