using System.Diagnostics;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Policies;
using Diaspora.Modules.Media.Domain.Processing;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Modules.Media.Extensibility.Processing;
using Diaspora.Modules.Media.Extensibility.Storage;
using Diaspora.Modules.Media.Observability;
using Diaspora.Modules.Media.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Media.Application.Services;

/// <summary>
/// Runs the planned work over one asset and records how each step went.
/// <para>
/// <b>The design rule this file exists to enforce:</b> a missing capability is not a failure.
/// A deployment with no image codec still validates, scans, describes and stores every upload;
/// only the renditions are absent, and each absence is recorded as
/// <see cref="ProcessingTaskStatus.Unsupported"/> rather than as an error. The alternative —
/// failing the upload because nobody installed an AVIF encoder — turns an operational choice
/// into a member-facing outage.
/// </para>
/// <para>
/// <b>Order is load-bearing.</b> Signature validation first, because everything after it
/// assumes the file is what it claims. Scanning second, because nothing should decode bytes
/// that have not been scanned — a decoder is the largest attack surface in the pipeline, and
/// pointing it at unscanned input inverts the whole point of scanning. Metadata third, because
/// a rendition needs the source dimensions to know it would be an upscale. Renditions last.
/// </para>
/// <para>
/// The source stream is buffered once and every processor reads it from the start. Re-reading
/// from storage per step would be one round trip per operation, and the pipeline runs six of
/// them on a typical image.
/// </para>
/// </summary>
public sealed class MediaProcessingPipeline
{
    private readonly MediaProcessorRegistry _processors;
    private readonly StorageProviderRegistry _storage;
    private readonly IClock _clock;
    private readonly MediaProcessingOptions _options;
    private readonly ILogger<MediaProcessingPipeline> _logger;

    public MediaProcessingPipeline(
        MediaProcessorRegistry processors,
        StorageProviderRegistry storage,
        IClock clock,
        IOptions<MediaProcessingOptions> options,
        ILogger<MediaProcessingPipeline> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _processors = processors;
        _storage = storage;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// The work an asset of this kind needs, in execution order.
    /// <para>
    /// Planned from the kind rather than from what is installed, deliberately. A plan that
    /// omitted the steps nothing can perform would make a deployment's gaps invisible: the job
    /// would report complete success while producing no thumbnails at all, and an operator
    /// would have nothing to look at. Planning the full set and recording the unsupported ones
    /// is what turns "we have no codec" into a fact somebody can see.
    /// </para>
    /// </summary>
    public static IReadOnlyList<ProcessingOperation> Plan(MediaKind kind)
    {
        var operations = new List<ProcessingOperation>
        {
            ProcessingOperation.ValidateSignature,
            ProcessingOperation.ScanForMalware,
            ProcessingOperation.ExtractMetadata
        };

        switch (kind)
        {
            case MediaKind.Image:
            case MediaKind.VerificationDocument:
                operations.Add(ProcessingOperation.StripMetadata);
                operations.Add(ProcessingOperation.CorrectOrientation);

                // A verification document is reviewed by a human at full size and never
                // rendered in a gallery. Generating a ladder for one would be storage spent on
                // renditions nothing requests.
                if (kind is MediaKind.Image)
                {
                    operations.Add(ProcessingOperation.GenerateThumbnails);
                    operations.Add(ProcessingOperation.GeneratePreview);
                }

                break;

            case MediaKind.Video:
                operations.Add(ProcessingOperation.ExtractDuration);
                operations.Add(ProcessingOperation.ExtractPosterFrame);
                operations.Add(ProcessingOperation.TranscodeVideo);
                break;

            case MediaKind.Audio:
                operations.Add(ProcessingOperation.ExtractDuration);
                break;
        }

        return operations;
    }

    /// <summary>
    /// Runs a job to completion, applying every outcome to the asset.
    /// <para>
    /// Never throws for a processing failure. A processor that throws has its step marked
    /// failed and the pipeline continues — one bad codec must not cost the asset its scan
    /// result or its metadata.
    /// </para>
    /// </summary>
    public async Task ExecuteAsync(
        ProcessingJob job,
        MediaAsset asset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(asset);

        using var activity = MediaTelemetry.Activity.StartActivity("media.process");
        activity?.SetTag("media.id", asset.Id.Value);
        activity?.SetTag("media.kind", asset.Kind.ToString());

        job.Begin(_clock.UtcNow);
        asset.BeginProcessing();

        var provider = _storage.Resolve(asset.Location.Provider);

        if (provider.IsFailure)
        {
            job.Fail("The asset's storage provider is not registered.", _clock.UtcNow);
            asset.MarkFailed("Storage provider unavailable.");
            return;
        }

        await using var stored = await provider.Value.OpenReadAsync(
            asset.Location.Container, asset.Location.Key, cancellationToken);

        if (stored is null)
        {
            job.Fail("The stored object is missing.", _clock.UtcNow);
            asset.MarkFailed("The stored file is missing.");
            return;
        }

        // Buffered once. Every processor rewinds and reads from the start; going back to
        // storage per step would be a round trip per operation, six of them on a photograph.
        await using var buffered = new MemoryStream();
        await stored.CopyToAsync(buffered, cancellationToken);

        var metadata = asset.Metadata;

        foreach (var task in job.PendingTasks)
        {
            buffered.Position = 0;

            var context = new MediaProcessingContext(
                buffered, asset.Kind, asset.ContentType, asset.FileName, asset.Size, metadata);

            var stopwatch = Stopwatch.StartNew();
            var outcome = await RunOperationAsync(task.Operation, context, provider.Value, asset, cancellationToken);
            stopwatch.Stop();

            job.RecordTask(task.Operation, outcome.Status, outcome.Detail, stopwatch.Elapsed);

            MediaTelemetry.ProcessingSteps.Add(1,
                new KeyValuePair<string, object?>("operation", task.Operation.ToString()),
                new KeyValuePair<string, object?>("status", outcome.Status.ToString()));

            MediaTelemetry.ProcessingStepDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("operation", task.Operation.ToString()));

            if (outcome.Metadata is not null)
            {
                metadata = outcome.Metadata;
                asset.ApplyMetadata(metadata);
            }

            if (outcome.ScanResult is not null)
            {
                asset.RecordScan(outcome.ScanResult);

                if (outcome.ScanResult.RequiresQuarantine)
                {
                    // Nothing else runs over bytes a scanner just flagged. Decoding them to
                    // make a thumbnail would be the single worst moment to point a codec at
                    // hostile input.
                    _logger.LogWarning(
                        "Media {MediaId} was quarantined by {Scanner}; processing stopped.",
                        asset.Id.Value, outcome.ScanResult.Scanner);

                    job.Complete(_clock.UtcNow);
                    return;
                }
            }

            if (outcome.Artifact is not null)
            {
                await StoreArtifactAsync(asset, outcome.Artifact, provider.Value, cancellationToken);
                outcome.Artifact.Dispose();
            }
        }

        job.Complete(_clock.UtcNow);
        FinishAsset(asset, job);
    }

    /// <summary>
    /// Offers an operation to each processor that claims it, stopping at the first that does
    /// not decline. Every failure mode is caught here rather than propagated.
    /// </summary>
    private async Task<MediaProcessingOutcome> RunOperationAsync(
        ProcessingOperation operation,
        MediaProcessingContext context,
        IMediaStorageProvider provider,
        MediaAsset asset,
        CancellationToken cancellationToken)
    {
        var candidates = _processors.For(asset.Kind, operation);

        if (candidates.Count == 0)
        {
            return operation is ProcessingOperation.ScanForMalware
                ? MediaProcessingOutcome.WithScan(
                    VirusScanResult.Skipped(_clock.UtcNow),
                    "No malware scanner is registered in this deployment.")
                : MediaProcessingOutcome.Declined("No processor claims this operation.");
        }

        MediaProcessingOutcome? lastDeclined = null;

        foreach (var processor in candidates)
        {
            context.RewindSource();

            MediaProcessingOutcome outcome;

            try
            {
                outcome = await InvokeAsync(processor, operation, context, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                // One processor throwing must not cost the asset its other steps. A codec that
                // faults on a malformed file is a normal event, not an outage.
                _logger.LogError(
                    exception,
                    "Media processor {Processor} threw during {Operation} on {MediaId}.",
                    processor.Name, operation, asset.Id.Value);

                return MediaProcessingOutcome.Failed(
                    $"{processor.Name} failed: {exception.Message}");
            }

            if (outcome.Status is ProcessingTaskStatus.Unsupported)
            {
                lastDeclined = outcome;
                continue;
            }

            return outcome;
        }

        return lastDeclined ?? MediaProcessingOutcome.Declined(
            "Every registered processor declined this operation.");
    }

    private static async Task<MediaProcessingOutcome> InvokeAsync(
        IMediaProcessor processor,
        ProcessingOperation operation,
        MediaProcessingContext context,
        CancellationToken cancellationToken) => (operation, processor) switch
    {
        (ProcessingOperation.ExtractMetadata or ProcessingOperation.ExtractDuration,
            IMetadataExtractor extractor) =>
            await extractor.ExtractAsync(context, cancellationToken),

        (ProcessingOperation.ScanForMalware, IVirusScanner scanner) =>
            await scanner.ScanAsync(context, cancellationToken),

        (ProcessingOperation.StripMetadata or ProcessingOperation.CorrectOrientation,
            IImageProcessor image) =>
            await image.StripMetadataAsync(context, cancellationToken),

        (ProcessingOperation.GenerateThumbnails, IThumbnailGenerator generator) =>
            await generator.GenerateAsync(context, MediaVariantPurpose.Thumbnail, cancellationToken),

        (ProcessingOperation.GeneratePreview, IThumbnailGenerator generator) =>
            await generator.GenerateAsync(context, MediaVariantPurpose.Preview, cancellationToken),

        (ProcessingOperation.TranscodeVideo, IVideoProcessor video) =>
            await video.TranscodeAsync(context, VideoTransformation.WebSafe720P, cancellationToken),

        (ProcessingOperation.ExtractPosterFrame, IVideoProcessor video) =>
            await video.ExtractPosterFrameAsync(context, TimeSpan.FromSeconds(1), cancellationToken),

        (ProcessingOperation.RecognizeText, IOcrProvider ocr) =>
            await ocr.RecognizeAsync(context, cancellationToken),

        (ProcessingOperation.AutoTag, IMediaTagger tagger) =>
            await tagger.TagAsync(context, cancellationToken),

        (ProcessingOperation.Moderate, IMediaModerationProvider moderator) =>
            await moderator.ModerateAsync(context, cancellationToken),

        (ProcessingOperation.ValidateSignature, IMetadataExtractor extractor) =>
            await extractor.ExtractAsync(context, cancellationToken),

        _ => MediaProcessingOutcome.Declined(
            $"{processor.Name} claims {operation} but exposes no method for it.")
    };

    /// <summary>
    /// Stores a produced rendition and records it on the asset.
    /// <para>
    /// A processor producing a <see cref="MediaVariantPurpose.Original"/> artifact is the
    /// metadata stripper handing back the same image minus its EXIF. That replaces the stored
    /// original in place rather than becoming a variant — the whole point is that the copy
    /// carrying GPS coordinates stops existing.
    /// </para>
    /// </summary>
    private static async Task StoreArtifactAsync(
        MediaAsset asset,
        ProcessedArtifact artifact,
        IMediaStorageProvider provider,
        CancellationToken cancellationToken)
    {
        artifact.Content.Position = 0;

        if (artifact.Purpose is MediaVariantPurpose.Original)
        {
            await provider.WriteAsync(
                new StorageWriteRequest
                {
                    Container = asset.Location.Container,
                    Key = asset.Location.Key,
                    Content = artifact.Content,
                    ContentType = artifact.ContentType,
                    ContentLength = artifact.Content.Length,
                    Tier = asset.Location.Tier
                },
                cancellationToken);

            asset.ApplyMetadata(asset.Metadata.WithMetadataStripped());
            return;
        }

        var key = StorageKey.ForVariant(asset.Location.Key, artifact.Purpose, artifact.Extension);

        var written = await provider.WriteAsync(
            new StorageWriteRequest
            {
                Container = asset.Location.Container,
                Key = key,
                Content = artifact.Content,
                ContentType = artifact.ContentType,
                ContentLength = artifact.Content.Length,
                Tier = StorageTier.Hot
            },
            cancellationToken);

        asset.AddVariant(
            artifact.Purpose,
            asset.Location.Sibling(key),
            artifact.ContentType,
            ByteSize.FromBytes(written.SizeInBytes),
            artifact.Dimensions,
            artifact.TransformationKey);
    }

    /// <summary>
    /// Decides what the asset's status becomes once the job closes.
    /// <para>
    /// A job that failed outright leaves the asset unreadable. Everything else — including a
    /// job where every rendition was unsupported — publishes it, because an asset whose bytes
    /// are safe is an asset a member should be able to see, with or without a thumbnail.
    /// </para>
    /// </summary>
    private void FinishAsset(MediaAsset asset, ProcessingJob job)
    {
        if (job.Status is ProcessingJobStatus.Failed && _options.FailOnUnsupportedOperation)
        {
            asset.MarkFailed(job.FailureReason ?? "Processing failed.");
            return;
        }

        if (job.Status is ProcessingJobStatus.Failed)
        {
            // A failed rendition is not a failed asset. The original is intact and readable;
            // the missing thumbnail is recorded on the job for whoever investigates.
            _logger.LogWarning(
                "Media {MediaId} finished processing with failures: {Reason}",
                asset.Id.Value, job.FailureReason);
        }

        var ready = asset.MarkReady();

        if (ready.IsFailure)
        {
            _logger.LogInformation(
                "Media {MediaId} is not yet servable: {Reason}",
                asset.Id.Value, ready.Error.Description);
        }
    }
}
