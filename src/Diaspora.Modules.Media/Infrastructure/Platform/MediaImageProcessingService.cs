using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Policies;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Modules.Media.Extensibility.Processing;
using Diaspora.Modules.Media.Infrastructure.Processing;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.Platform.Services.Models;

namespace Diaspora.Modules.Media.Infrastructure.Platform;

/// <summary>
/// Fulfils the platform's <see cref="IImageProcessingService"/> by delegating to whichever
/// image processors this deployment registered.
/// <para>
/// <b>Registering this is a deliberate, and slightly awkward, decision.</b> The platform
/// contract catalog lists <c>IImageProcessingService</c> as declared-but-unimplemented and
/// warns that injecting it throws at resolution. Leaving it that way after B5 would be strange
/// — Media is the module that owns image processing — but the interface returns bare
/// <see cref="Stream"/>s, so it has no way to express "this deployment has no codec". The
/// honest options were to leave it unresolvable, or to implement it and throw a clear,
/// specific exception for the operations that genuinely cannot be performed.
/// </para>
/// <para>
/// The second is better, for one concrete reason: <see cref="ExtractMetadataAsync"/> works
/// with no codec at all, because Media reads image headers itself. A caller that only wants
/// dimensions gets them. A caller that wants a resize gets a
/// <see cref="MediaProcessingUnavailableException"/> naming the missing capability rather than
/// a service-resolution failure that names nothing.
/// </para>
/// <para>
/// This is recorded as an additive platform-evolution finding rather than fixed here: the
/// interface should return <c>Result&lt;T&gt;</c> like every other platform contract written
/// since B1. Changing it is an edit to a frozen module, so B5 documents it instead. See the
/// platform evolution report.
/// </para>
/// </summary>
public sealed class MediaImageProcessingService : IImageProcessingService
{
    private readonly MediaProcessorRegistry _processors;

    public MediaImageProcessingService(MediaProcessorRegistry processors)
    {
        _processors = processors;
    }

    public Task<Stream> CompressAsync(
        Stream source, int quality = 80, CancellationToken cancellationToken = default) =>
        TransformAsync(
            source,
            ImageTransformation.TryCreate(
                ImageTransformation.MaxEdge, ResizeMode.Contain, ImageFormat.Unknown, quality),
            ProcessingOperation.Optimize,
            cancellationToken);

    public Task<Stream> ResizeAsync(
        Stream source, int maxWidth, int maxHeight, CancellationToken cancellationToken = default) =>
        TransformAsync(
            source,
            ImageTransformation.TryCreate(Math.Max(maxWidth, maxHeight)),
            ProcessingOperation.GenerateThumbnails,
            cancellationToken);

    /// <summary>
    /// Cropping to an arbitrary rectangle is not part of Media's own transformation vocabulary
    /// — the variant ladder is a closed set precisely so the cache key space stays finite —
    /// so this is only ever satisfied by a registered processor.
    /// </summary>
    public Task<Stream> CropAsync(
        Stream source,
        int x,
        int y,
        int width,
        int height,
        CancellationToken cancellationToken = default) =>
        throw new MediaProcessingUnavailableException(
            "Arbitrary cropping is not offered by the media module. Request a named variant, " +
            "or register an IImageProcessor that supports it.");

    public Task<Stream> GenerateThumbnailAsync(
        Stream source, int size, CancellationToken cancellationToken = default) =>
        TransformAsync(
            source,
            ImageTransformation.TryCreate(size, ResizeMode.Cover),
            ProcessingOperation.GenerateThumbnails,
            cancellationToken);

    public Task<string> GenerateBlurHashAsync(
        Stream source, CancellationToken cancellationToken = default) =>
        throw new MediaProcessingUnavailableException(
            "No registered processor produces a blur hash. Register an IImageProcessor that " +
            "supports GeneratePreview.");

    /// <summary>
    /// The one method that works on every deployment, because Media reads image headers
    /// itself rather than decoding them.
    /// </summary>
    public async Task<ImageMetadata> ExtractMetadataAsync(
        Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.CanSeek)
        {
            source.Position = 0;
        }

        var buffer = new byte[ImageHeaderReader.HeaderLength];
        var read = 0;

        while (read < buffer.Length)
        {
            var chunk = await source.ReadAsync(buffer.AsMemory(read), cancellationToken);

            if (chunk == 0)
            {
                break;
            }

            read += chunk;
        }

        var header = buffer.AsSpan(0, read);
        var signature = FileSignatureReader.Identify(header);
        var info = ImageHeaderReader.Read(header, signature.ImageFormat);

        return new ImageMetadata
        {
            Width = info?.Dimensions.Width ?? 0,
            Height = info?.Dimensions.Height ?? 0,
            MimeType = signature.IsRecognized ? signature.ContentType.Value : null,
            SizeBytes = source.CanSeek ? source.Length : read
        };
    }

    private async Task<Stream> TransformAsync(
        Stream source,
        SharedKernel.Results.Result<ImageTransformation> transformation,
        ProcessingOperation operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (transformation.IsFailure)
        {
            throw new MediaProcessingUnavailableException(transformation.Error.Description);
        }

        var processors = _processors.For(MediaKind.Image, operation)
            .OfType<IImageProcessor>()
            .ToList();

        if (processors.Count == 0)
        {
            throw new MediaProcessingUnavailableException(
                "No image processor is registered in this deployment. Media stores, validates " +
                "and describes images without one, but cannot re-encode them.");
        }

        var context = new MediaProcessingContext(
            source,
            MediaKind.Image,
            MediaContentType.OctetStream,
            SafeFileName.Create("image"),
            ByteSize.FromBytes(source.CanSeek ? source.Length : 0),
            MediaMetadata.Empty);

        foreach (var processor in processors)
        {
            context.RewindSource();

            var outcome = await processor.TransformAsync(
                context, transformation.Value, cancellationToken);

            if (outcome.Artifact is not null)
            {
                // Ownership of the stream passes to the caller, which is what the platform
                // interface's Stream-returning shape implies.
                var content = outcome.Artifact.Content;
                content.Position = 0;
                return content;
            }
        }

        throw new MediaProcessingUnavailableException(
            "Every registered image processor declined the requested transformation.");
    }
}
