using Diaspora.Modules.Media.Domain.ValueObjects;

namespace Diaspora.Modules.Media.Extensibility.Processing;

/// <summary>
/// Produces image renditions: resize, crop, re-encode, optimise, strip metadata, correct
/// orientation.
/// <para>
/// <b>The seam that keeps an image codec out of the platform.</b> Every codec worth using is
/// either a native dependency, a licensing decision, or both — ImageSharp's split licence,
/// SkiaSharp's native binaries, System.Drawing's Windows-only support. Making the choice a
/// contributor rather than a package reference means a deployment picks one, or picks a hosted
/// service, or picks none, and the module works in all three cases.
/// </para>
/// <para>
/// <b>What "picks none" means in practice.</b> Media ships no codec. Assets are still stored,
/// hashed, signature-checked, scanned, described from their headers, and served; the size
/// ladder is simply absent, and the read path falls back to the original — a larger image
/// than the client asked for, not a broken one. The processing job records each missing
/// rendition as <c>Unsupported</c>, so the gap is visible rather than silent. Registering a
/// codec fills the ladder with no change anywhere else.
/// </para>
/// </summary>
public interface IImageProcessor : IMediaProcessor
{
    /// <summary>
    /// Applies a transformation, returning the produced bytes.
    /// <para>
    /// Returns <see cref="MediaProcessingOutcome.Declined"/> — not a failure — for a
    /// transformation this processor will not perform. A JPEG-only encoder asked for AVIF has
    /// not failed; it has nothing to offer, and the pipeline should try the next processor.
    /// </para>
    /// </summary>
    Task<MediaProcessingOutcome> TransformAsync(
        MediaProcessingContext context,
        ImageTransformation transformation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes EXIF, IPTC and XMP, baking any orientation flag into the pixels first.
    /// <para>
    /// Not an optimisation. A photograph taken on a phone carries the GPS coordinates of the
    /// place it was taken, and a member posting a picture of their living room to a
    /// marketplace listing is publishing their home address without knowing it. This runs on
    /// every image, before any rendition is derived from it.
    /// </para>
    /// </summary>
    Task<MediaProcessingOutcome> StripMetadataAsync(
        MediaProcessingContext context, CancellationToken cancellationToken = default);
}
