using Diaspora.Modules.Media.Domain.Enums;

namespace Diaspora.Modules.Media.Extensibility.Processing;

/// <summary>
/// Produces one rung of the fixed size ladder.
/// <para>
/// Distinct from <see cref="IImageProcessor"/> because the two answer different questions. An
/// image processor is asked for an arbitrary transformation; a thumbnail generator is asked
/// for a named rung, and it may satisfy that request however it likes — including from a
/// source that is not an image at all. A PDF's first page and a video's poster frame are both
/// thumbnails, and neither is an image transformation.
/// </para>
/// <para>
/// Most deployments will register one implementation that also implements
/// <see cref="IImageProcessor"/> and delegates. Keeping the interfaces separate is what leaves
/// room for the deployment that does not.
/// </para>
/// </summary>
public interface IThumbnailGenerator : IMediaProcessor
{
    Task<MediaProcessingOutcome> GenerateAsync(
        MediaProcessingContext context,
        MediaVariantPurpose purpose,
        CancellationToken cancellationToken = default);
}
