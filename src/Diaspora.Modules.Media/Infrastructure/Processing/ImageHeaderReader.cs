using System.Buffers.Binary;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.ValueObjects;

namespace Diaspora.Modules.Media.Infrastructure.Processing;

/// <summary>
/// Reads an image's dimensions and EXIF orientation from its header, without decoding it.
/// <para>
/// <b>Why this exists rather than a codec call.</b> The client declares <c>width?</c> and
/// <c>height?</c> on every image attachment and populates neither; the server is expected to.
/// Every codec that could answer is a licensing or native-dependency decision this module
/// refuses to make on a deployment's behalf. But dimensions are in the header of every format
/// that matters, in a few dozen bytes, and reading them is arithmetic. So Media answers the
/// question itself and leaves the codec to the transformations that genuinely need one.
/// </para>
/// <para>
/// It is also the decompression-bomb defence. A 30 KB PNG declaring 60 000 × 60 000 costs
/// fourteen gigabytes to decode; the only place to refuse it is before decoding, from these
/// same bytes.
/// </para>
/// <para>
/// Reads a bounded prefix and never allocates proportionally to the file. Anything it cannot
/// parse returns null rather than a guess — a wrong dimension is worse than a missing one,
/// because a caller will act on it.
/// </para>
/// </summary>
public static class ImageHeaderReader
{
    /// <summary>
    /// How much of the file to read. Generous enough for a JPEG whose SOF marker sits behind
    /// a large EXIF block, bounded enough that a hostile file cannot make this expensive.
    /// </summary>
    public const int HeaderLength = 64 * 1024;

    /// <summary>
    /// Reads what the header says, or null when the format is unsupported or the header is
    /// malformed.
    /// </summary>
    public static ImageHeaderInfo? Read(ReadOnlySpan<byte> header, ImageFormat format) =>
        format switch
        {
            ImageFormat.Png => ReadPng(header),
            ImageFormat.Jpeg => ReadJpeg(header),
            ImageFormat.Gif => ReadGif(header),
            ImageFormat.WebP => ReadWebP(header),
            ImageFormat.Bmp => ReadBmp(header),
            _ => null
        };

    /// <summary>
    /// PNG: the IHDR chunk is always first and always at a fixed offset. Big-endian, like
    /// everything else in the format.
    /// </summary>
    private static ImageHeaderInfo? ReadPng(ReadOnlySpan<byte> header)
    {
        // 8-byte signature, 4-byte length, "IHDR", then width and height.
        if (header.Length < 24 ||
            !header.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            return null;
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(header.Slice(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(header.Slice(20, 4));

        return Build(width, height, null, hasEmbeddedMetadata: false);
    }

    /// <summary>
    /// JPEG: a chain of length-prefixed segments. Dimensions live in whichever start-of-frame
    /// marker the encoder used, and there are a dozen of them — SOF0 through SOF15, excluding
    /// the four that are not frame headers. Walking the chain is the only correct way; the
    /// common shortcut of assuming SOF0 fails on every progressive JPEG, which is most
    /// phone-camera output.
    /// </summary>
    private static ImageHeaderInfo? ReadJpeg(ReadOnlySpan<byte> header)
    {
        var offset = 2;
        var hasExif = false;
        int? orientation = null;

        while (offset + 4 <= header.Length)
        {
            if (header[offset] != 0xFF)
            {
                return null;
            }

            var marker = header[offset + 1];

            // Standalone markers carry no length.
            if (marker is 0xD8 or 0x01 || (marker >= 0xD0 && marker <= 0xD7))
            {
                offset += 2;
                continue;
            }

            // Start of scan: compressed data follows and there is no more header to walk.
            if (marker == 0xDA)
            {
                break;
            }

            if (offset + 4 > header.Length)
            {
                break;
            }

            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(header.Slice(offset + 2, 2));

            if (segmentLength < 2)
            {
                return null;
            }

            // APP1 with an "Exif" identifier: the block carrying orientation and, on a phone,
            // the GPS coordinates of wherever the photograph was taken.
            if (marker == 0xE1 &&
                offset + 4 + 6 <= header.Length &&
                header.Slice(offset + 4, 4).SequenceEqual("Exif"u8))
            {
                hasExif = true;

                var exifStart = offset + 4 + 6;
                var exifLength = Math.Min(segmentLength - 8, header.Length - exifStart);

                if (exifLength > 0)
                {
                    orientation = ExifReader.ReadOrientation(header.Slice(exifStart, exifLength));
                }
            }

            // Any start-of-frame marker. 0xC4, 0xC8 and 0xCC are not frame headers.
            if (marker >= 0xC0 && marker <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC))
            {
                if (offset + 9 > header.Length)
                {
                    return null;
                }

                var height = BinaryPrimitives.ReadUInt16BigEndian(header.Slice(offset + 5, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(header.Slice(offset + 7, 2));

                return Build(width, height, orientation, hasExif);
            }

            offset += 2 + segmentLength;
        }

        return null;
    }

    /// <summary>GIF: fixed offsets, little-endian.</summary>
    private static ImageHeaderInfo? ReadGif(ReadOnlySpan<byte> header)
    {
        if (header.Length < 10)
        {
            return null;
        }

        var width = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(6, 2));
        var height = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(8, 2));

        return Build(width, height, null, hasEmbeddedMetadata: false);
    }

    /// <summary>
    /// WebP: three sub-formats — lossy (VP8), lossless (VP8L) and extended (VP8X) — each
    /// encoding dimensions differently, and two of them not on byte boundaries.
    /// </summary>
    private static ImageHeaderInfo? ReadWebP(ReadOnlySpan<byte> header)
    {
        if (header.Length < 30)
        {
            return null;
        }

        var chunk = header.Slice(12, 4);

        if (chunk.SequenceEqual("VP8 "u8))
        {
            // Lossy: the 14-bit dimensions sit behind a 3-byte frame tag and a start code.
            if (header.Length < 30)
            {
                return null;
            }

            var width = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(26, 2)) & 0x3FFF;
            var height = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(28, 2)) & 0x3FFF;

            return Build(width, height, null, hasEmbeddedMetadata: false);
        }

        if (chunk.SequenceEqual("VP8L"u8))
        {
            // Lossless: 14 bits each, minus one, packed across four bytes.
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(21, 4));

            var width = (int)(bits & 0x3FFF) + 1;
            var height = (int)((bits >> 14) & 0x3FFF) + 1;

            return Build(width, height, null, hasEmbeddedMetadata: false);
        }

        if (chunk.SequenceEqual("VP8X"u8))
        {
            // Extended: 24-bit dimensions minus one. Also the variant that can carry EXIF.
            var width = (header[24] | (header[25] << 8) | (header[26] << 16)) + 1;
            var height = (header[27] | (header[28] << 8) | (header[29] << 16)) + 1;

            var hasMetadata = (header[20] & 0x08) != 0;

            return Build(width, height, null, hasMetadata);
        }

        return null;
    }

    /// <summary>BMP: fixed offsets in the DIB header, little-endian, height may be negative.</summary>
    private static ImageHeaderInfo? ReadBmp(ReadOnlySpan<byte> header)
    {
        if (header.Length < 26)
        {
            return null;
        }

        var width = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(18, 4));

        // A negative height means the rows are stored top-down. The magnitude is the height.
        var height = Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(header.Slice(22, 4)));

        return Build(width, height, null, hasEmbeddedMetadata: false);
    }

    private static ImageHeaderInfo? Build(
        int width, int height, int? orientation, bool hasEmbeddedMetadata)
    {
        var dimensions = MediaDimensions.TryCreate(width, height);

        if (dimensions.IsFailure)
        {
            return null;
        }

        // Orientation 5–8 swap the axes. Reporting the stored dimensions for a portrait
        // photograph the camera wrote landscape means every layout that trusts them is wrong,
        // and every "why is my photo sideways" bug report starts here.
        var effective = orientation is >= 5 and <= 8
            ? MediaDimensions.Create(dimensions.Value.Height, dimensions.Value.Width)
            : dimensions.Value;

        return new ImageHeaderInfo(effective, orientation, hasEmbeddedMetadata);
    }
}
