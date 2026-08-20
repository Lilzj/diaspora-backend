using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.ValueObjects;

namespace Diaspora.Modules.Media.Extensibility.Processing;

/// <summary>
/// What a processor reports back.
/// <para>
/// The three outcomes are deliberately distinct, and collapsing any two of them loses
/// operational information that cannot be recovered later:
/// </para>
/// <list type="bullet">
/// <item><see cref="Succeeded"/> — it did the thing.</item>
/// <item><see cref="Declined"/> — it will not do the thing, and no error occurred. A JPEG
/// encoder asked for an AVIF. The pipeline moves to the next processor.</item>
/// <item><see cref="Failed"/> — it tried and could not. Something is wrong.</item>
/// </list>
/// <para>
/// A processor that returns <c>Failed</c> where it means <c>Declined</c> turns a deployment
/// without a codec into a deployment with a broken pipeline, and somebody gets paged for a
/// capability that was never installed.
/// </para>
/// </summary>
public sealed record MediaProcessingOutcome
{
    private MediaProcessingOutcome(ProcessingTaskStatus status, string? detail)
    {
        Status = status;
        Detail = detail;
    }

    public ProcessingTaskStatus Status { get; private init; }

    /// <summary>What happened, in words. Recorded on the processing step.</summary>
    public string? Detail { get; private init; }

    /// <summary>Metadata this processor learned, merged into the asset by the pipeline.</summary>
    public MediaMetadata? Metadata { get; private init; }

    /// <summary>
    /// A rendition this processor produced, if any. The pipeline stores the bytes and records
    /// the variant; a processor never touches storage itself, which is what keeps a codec free
    /// of any knowledge about buckets.
    /// </summary>
    public ProcessedArtifact? Artifact { get; private init; }

    /// <summary>A scan verdict, when the processor was a scanner.</summary>
    public VirusScanResult? ScanResult { get; private init; }

    public bool IsSuccess => Status is ProcessingTaskStatus.Succeeded;

    public static MediaProcessingOutcome Succeeded(string? detail = null) =>
        new(ProcessingTaskStatus.Succeeded, detail);

    public static MediaProcessingOutcome WithMetadata(MediaMetadata metadata, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new MediaProcessingOutcome(ProcessingTaskStatus.Succeeded, detail)
        {
            Metadata = metadata
        };
    }

    public static MediaProcessingOutcome WithArtifact(ProcessedArtifact artifact, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        return new MediaProcessingOutcome(ProcessingTaskStatus.Succeeded, detail)
        {
            Artifact = artifact
        };
    }

    public static MediaProcessingOutcome WithScan(VirusScanResult scanResult, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(scanResult);

        return new MediaProcessingOutcome(ProcessingTaskStatus.Succeeded, detail)
        {
            ScanResult = scanResult
        };
    }

    /// <summary>Not this processor's job. No error; the pipeline tries the next one.</summary>
    public static MediaProcessingOutcome Declined(string? reason = null) =>
        new(ProcessingTaskStatus.Unsupported, reason);

    /// <summary>Not applicable to this asset at all — a poster frame for a PDF.</summary>
    public static MediaProcessingOutcome NotApplicable(string? reason = null) =>
        new(ProcessingTaskStatus.NotApplicable, reason);

    /// <summary>Tried and could not. Something is wrong.</summary>
    public static MediaProcessingOutcome Failed(string reason) =>
        new(ProcessingTaskStatus.Failed, reason);
}
