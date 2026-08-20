using System.Buffers.Binary;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Modules.Media.Extensibility.Processing;

namespace Diaspora.Modules.Media.Infrastructure.Processing;

/// <summary>
/// Removes EXIF, IPTC and XMP from a JPEG without decoding it.
/// <para>
/// <b>Why this is worth implementing without a codec.</b> A photograph taken on a phone
/// carries the GPS coordinates of the place it was taken. A member posting a picture of their
/// living room to a marketplace listing, or of their front door to a housing listing, is
/// publishing their home address — and neither they nor the vertical that stored the photo has
/// any idea. Waiting for a codec dependency before fixing that would mean shipping a platform
/// that leaks home addresses by default.
/// </para>
/// <para>
/// A JPEG is a chain of length-prefixed segments, and metadata lives in APP1 (EXIF, XMP), APP2
/// (ICC, though that one is kept), APP13 (IPTC) and COM. Dropping those segments and copying
/// the rest byte-for-byte is a complete, lossless, re-encode-free strip. The pixels are never
/// touched, so there is no generational quality loss and no codec involved.
/// </para>
/// <para>
/// <b>What it does not do.</b> It cannot bake an orientation flag into the pixels — that needs
/// a real rotation, which needs a real codec. It therefore declines
/// <see cref="ProcessingOperation.CorrectOrientation"/>, and the orientation value survives on
/// the asset's metadata so a client can apply it. It handles JPEG only; PNG, WebP and HEIC
/// metadata removal declines, and an installed image processor covers them.
/// </para>
/// </summary>
public sealed class JpegMetadataStripper : IImageProcessor
{
    /// <summary>Segments carrying metadata rather than image data.</summary>
    private static readonly HashSet<byte> MetadataMarkers =
    [
        0xE1, // APP1 — EXIF and XMP. The one that carries GPS.
        0xE2, // APP2 — but see the ICC exception below.
        0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xEB, 0xEC,
        0xED, // APP13 — IPTC / Photoshop.
        0xEE, 0xEF,
        0xFE  // COM — comment.
    ];

    public string Name => "jpeg-metadata-stripper";

    public int Order => 1000;

    public IReadOnlySet<ProcessingOperation> SupportedOperations { get; } =
        new HashSet<ProcessingOperation> { ProcessingOperation.StripMetadata };

    public bool CanProcess(MediaKind kind, ProcessingOperation operation) =>
        kind is MediaKind.Image or MediaKind.VerificationDocument &&
        operation is ProcessingOperation.StripMetadata;

    /// <summary>
    /// Declines every transformation. Resizing needs a decoder; this class has none, and
    /// pretending otherwise would put a broken thumbnail where a missing one belongs.
    /// </summary>
    public Task<MediaProcessingOutcome> TransformAsync(
        MediaProcessingContext context,
        ImageTransformation transformation,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(MediaProcessingOutcome.Declined(
            "No image codec is registered in this deployment; renditions are unavailable."));

    public async Task<MediaProcessingOutcome> StripMetadataAsync(
        MediaProcessingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.RewindSource();

        using var buffer = new MemoryStream();
        await context.Source.CopyToAsync(buffer, cancellationToken);

        var source = buffer.ToArray();

        if (source.Length < 4 || source[0] != 0xFF || source[1] != 0xD8)
        {
            return MediaProcessingOutcome.Declined(
                "Metadata stripping without a codec covers JPEG only.");
        }

        var output = new MemoryStream(source.Length);

        // The start-of-image marker, always.
        output.Write(source.AsSpan(0, 2));

        var offset = 2;
        var removed = 0;

        while (offset + 4 <= source.Length)
        {
            if (source[offset] != 0xFF)
            {
                // A malformed chain. Copy the remainder verbatim rather than truncating the
                // image — a file we cannot fully parse must still come out the other side
                // intact.
                output.Write(source.AsSpan(offset));
                offset = source.Length;
                break;
            }

            var marker = source[offset + 1];

            // Start of scan: everything after this is compressed image data.
            if (marker == 0xDA)
            {
                output.Write(source.AsSpan(offset));
                offset = source.Length;
                break;
            }

            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(source.AsSpan(offset + 2, 2));

            if (segmentLength < 2 || offset + 2 + segmentLength > source.Length)
            {
                output.Write(source.AsSpan(offset));
                offset = source.Length;
                break;
            }

            if (ShouldDrop(marker, source.AsSpan(offset, 2 + segmentLength)))
            {
                removed++;
            }
            else
            {
                output.Write(source.AsSpan(offset, 2 + segmentLength));
            }

            offset += 2 + segmentLength;
        }

        if (removed == 0)
        {
            await output.DisposeAsync();

            return MediaProcessingOutcome.WithMetadata(
                context.KnownMetadata.WithMetadataStripped(),
                "No embedded metadata was present.");
        }

        output.Position = 0;

        var artifact = new ProcessedArtifact(
            MediaVariantPurpose.Original,
            output,
            context.ContentType,
            context.FileName.Extension,
            context.KnownMetadata.Dimensions);

        return MediaProcessingOutcome.WithArtifact(
            artifact, $"Removed {removed} metadata segment(s).");
    }

    /// <summary>
    /// Whether a segment is metadata rather than image data.
    /// <para>
    /// APP2 is the exception worth calling out. It usually carries an ICC colour profile,
    /// which is not personal data and whose removal makes wide-gamut photographs render with
    /// visibly wrong colours. It is kept; every other APP segment goes.
    /// </para>
    /// </summary>
    private static bool ShouldDrop(byte marker, ReadOnlySpan<byte> segment)
    {
        if (!MetadataMarkers.Contains(marker))
        {
            return false;
        }

        if (marker == 0xE2 &&
            segment.Length >= 16 &&
            segment.Slice(4, 11).SequenceEqual("ICC_PROFILE"u8))
        {
            return false;
        }

        return true;
    }
}
