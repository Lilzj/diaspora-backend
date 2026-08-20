using Diaspora.Modules.Media.Domain.ValueObjects;

namespace Diaspora.Modules.Media.Extensibility.Processing;

/// <summary>
/// Transcodes video and lifts poster frames from it.
/// <para>
/// Separate from <see cref="IImageProcessor"/> because the implementations have nothing in
/// common. An image codec is an in-process library over a buffer; a transcoder is a long-
/// running external process, or a hosted job with a webhook, and it is measured in minutes
/// rather than milliseconds. A merged interface would force one shape onto both.
/// </para>
/// <para>
/// A transcoder that will run for minutes should honour the cancellation token and return
/// <see cref="MediaProcessingOutcome.Declined"/> rather than block a request thread — the
/// pipeline runs it on a background worker precisely because of this, and a synchronous
/// implementation would put a request in the same wait.
/// </para>
/// </summary>
public interface IVideoProcessor : IMediaProcessor
{
    Task<MediaProcessingOutcome> TranscodeAsync(
        MediaProcessingContext context,
        VideoTransformation transformation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lifts a still to use as a poster frame.
    /// <para>
    /// <paramref name="at"/> is a hint, not an instruction — the first frame of most videos is
    /// black, so a processor is free to seek to the nearest keyframe or to pick a
    /// representative one instead.
    /// </para>
    /// </summary>
    Task<MediaProcessingOutcome> ExtractPosterFrameAsync(
        MediaProcessingContext context,
        TimeSpan at,
        CancellationToken cancellationToken = default);
}
