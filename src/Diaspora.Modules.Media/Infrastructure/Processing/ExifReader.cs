using System.Buffers.Binary;

namespace Diaspora.Modules.Media.Infrastructure.Processing;

/// <summary>
/// Reads the one EXIF tag the platform acts on: orientation.
/// <para>
/// A deliberately minimal reader. EXIF is a large, quirky, deeply nested format and a general
/// parser for it is a meaningful attack surface — several image libraries have had memory-
/// safety bugs in exactly this code. Media needs one 16-bit tag from the first directory, so
/// it reads that and nothing else, bounds-checks every access, and returns null the moment
/// anything looks wrong.
/// </para>
/// <para>
/// Everything else in an EXIF block — including the GPS coordinates a phone writes into every
/// photograph — is not read, because it is not kept. The pipeline strips the whole block.
/// </para>
/// </summary>
internal static class ExifReader
{
    private const ushort OrientationTag = 0x0112;

    /// <summary>
    /// Reads the orientation value, 1–8, from a TIFF-structured EXIF payload. Null when the
    /// tag is absent or the structure is not readable.
    /// </summary>
    public static int? ReadOrientation(ReadOnlySpan<byte> exif)
    {
        if (exif.Length < 8)
        {
            return null;
        }

        // The byte order mark: "II" little-endian (Intel) or "MM" big-endian (Motorola).
        var littleEndian = exif[0] == 0x49 && exif[1] == 0x49;
        var bigEndian = exif[0] == 0x4D && exif[1] == 0x4D;

        if (!littleEndian && !bigEndian)
        {
            return null;
        }

        var magic = ReadUInt16(exif.Slice(2, 2), littleEndian);

        if (magic != 42)
        {
            return null;
        }

        var ifdOffset = (int)ReadUInt32(exif.Slice(4, 4), littleEndian);

        if (ifdOffset < 8 || ifdOffset + 2 > exif.Length)
        {
            return null;
        }

        var entryCount = ReadUInt16(exif.Slice(ifdOffset, 2), littleEndian);

        for (var index = 0; index < entryCount; index++)
        {
            // Each directory entry is twelve bytes: tag, type, count, then value or offset.
            var entryOffset = ifdOffset + 2 + (index * 12);

            if (entryOffset + 12 > exif.Length)
            {
                return null;
            }

            var tag = ReadUInt16(exif.Slice(entryOffset, 2), littleEndian);

            if (tag != OrientationTag)
            {
                continue;
            }

            // A SHORT value is stored inline in the first two bytes of the value field.
            var value = ReadUInt16(exif.Slice(entryOffset + 8, 2), littleEndian);

            return value is >= 1 and <= 8 ? value : null;
        }

        return null;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, bool littleEndian) =>
        littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt16BigEndian(bytes);

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, bool littleEndian) =>
        littleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt32BigEndian(bytes);
}
