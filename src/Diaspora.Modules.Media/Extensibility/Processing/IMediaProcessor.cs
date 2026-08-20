using Diaspora.Modules.Media.Domain.Enums;

namespace Diaspora.Modules.Media.Extensibility.Processing;

/// <summary>
/// The base contract every processing contributor shares: what it can do, and in what order it
/// wants to be asked.
/// <para>
/// The specialised interfaces below it — <see cref="IImageProcessor"/>,
/// <see cref="IVideoProcessor"/>, <see cref="IMetadataExtractor"/>,
/// <see cref="IVirusScanner"/>, <see cref="IThumbnailGenerator"/>,
/// <see cref="IOcrProvider"/>, <see cref="IMediaTagger"/>,
/// <see cref="IMediaModerationProvider"/> — exist so a contributor implements only the shape
/// it needs. A scanner and a codec have nothing in common but this.
/// </para>
/// <para>
/// <b>Declared capability is the whole mechanism.</b> The pipeline asks each processor what it
/// supports and plans a job out of the answers. A deployment with no video codec produces a
/// job whose transcode step is <see cref="ProcessingTaskStatus.Unsupported"/> rather than one
/// that fails — the asset is still stored, scanned, described and thumbnailed, and the missing
/// capability is visible in the job record instead of being an outage.
/// </para>
/// </summary>
public interface IMediaProcessor
{
    /// <summary>
    /// A stable name for this processor, recorded on every step it runs.
    /// <para>
    /// Recorded so that a bad rendition can be traced to the processor that made it, which
    /// matters most in the case where two processors claim the same operation and one of them
    /// is subtly wrong.
    /// </para>
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Order among processors claiming the same operation. Lower runs first, and the first one
    /// that reports success ends the operation.
    /// <para>
    /// This is what lets a deployment install a better codec without removing the built-in
    /// one: register it at a lower order and it wins, with the built-in still there as a
    /// fallback if it declines.
    /// </para>
    /// </summary>
    int Order => 100;

    /// <summary>The operations this processor claims.</summary>
    IReadOnlySet<ProcessingOperation> SupportedOperations { get; }

    /// <summary>Whether this processor will attempt <paramref name="operation"/> on this kind.</summary>
    bool CanProcess(MediaKind kind, ProcessingOperation operation);
}
