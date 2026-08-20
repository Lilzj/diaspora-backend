namespace Diaspora.Modules.Media.Extensibility.Processing;

/// <summary>
/// Reads what a file says about itself: dimensions, duration, page count, orientation.
/// <para>
/// The one processing seam Media fills itself. <c>HeaderMetadataExtractor</c> parses image
/// headers in pure managed code with no codec, which is what makes the client's
/// <c>width</c>/<c>height</c> fields populate on a deployment with no image library at all.
/// </para>
/// <para>
/// A richer extractor — one that reads a PDF's page count or a video's duration — registers
/// alongside it at a lower <see cref="IMediaProcessor.Order"/> and wins. The built-in one
/// declines what it cannot read rather than guessing, so a better extractor never has to
/// fight it.
/// </para>
/// </summary>
public interface IMetadataExtractor : IMediaProcessor
{
    Task<MediaProcessingOutcome> ExtractAsync(
        MediaProcessingContext context, CancellationToken cancellationToken = default);
}
