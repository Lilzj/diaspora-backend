using System.Text;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.ValueObjects;

namespace Diaspora.Modules.Media.Domain.Policies;

/// <summary>
/// Identifies a file from its leading bytes.
/// <para>
/// The single most valuable security control in the module, and the cheapest. The declared
/// content type comes from a header the caller wrote; the extension comes from a name the
/// caller chose. Only this comes from the file. Refusing an upload whose signature disagrees
/// with its declaration closes polyglot files, the double-extension family, and every variant
/// of "rename the payload to .jpg and let the CDN serve it as script" at once.
/// </para>
/// <para>
/// Pure and allocation-light on purpose: it reads at most
/// <see cref="FileSignature.InspectionLength"/> bytes, never decodes anything, and has no
/// dependency on a codec. It runs on the first chunk of every upload, before a single byte is
/// committed to storage — which is only possible because it needs no more than the head.
/// </para>
/// </summary>
public static class FileSignatureReader
{
    /// <summary>
    /// Identifies the format of <paramref name="head"/>, the first bytes of a file.
    /// <para>
    /// Ordered most-specific first. RIFF- and ISO-BMFF-based formats share a leading pattern
    /// with several others, so they are matched on their inner brand rather than their first
    /// four bytes — the mistake that makes a naive table report every MP4 as a QuickTime file
    /// and every WebP as a WAV.
    /// </para>
    /// </summary>
    public static FileSignature Identify(ReadOnlySpan<byte> head)
    {
        if (head.Length < 4)
        {
            return FileSignature.Unrecognized;
        }

        // ── Images ───────────────────────────────────────────────────────

        if (StartsWith(head, [0xFF, 0xD8, 0xFF]))
        {
            return FileSignature.For(MediaKind.Image, ImageFormat.Jpeg, "image/jpeg", "JPEG image");
        }

        if (StartsWith(head, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return FileSignature.For(MediaKind.Image, ImageFormat.Png, "image/png", "PNG image");
        }

        if (StartsWithAscii(head, "GIF87a") || StartsWithAscii(head, "GIF89a"))
        {
            return FileSignature.For(MediaKind.Image, ImageFormat.Gif, "image/gif", "GIF image");
        }

        if (StartsWithAscii(head, "BM"))
        {
            return FileSignature.For(MediaKind.Image, ImageFormat.Bmp, "image/bmp", "Bitmap image");
        }

        if (StartsWith(head, [0x49, 0x49, 0x2A, 0x00]) || StartsWith(head, [0x4D, 0x4D, 0x00, 0x2A]))
        {
            return FileSignature.For(MediaKind.Image, ImageFormat.Tiff, "image/tiff", "TIFF image");
        }

        // ── RIFF containers: WebP and WAV share the first four bytes ─────

        if (StartsWithAscii(head, "RIFF") && head.Length >= 12)
        {
            var brand = Ascii(head.Slice(8, 4));

            if (string.Equals(brand, "WEBP", StringComparison.Ordinal))
            {
                return FileSignature.For(MediaKind.Image, ImageFormat.WebP, "image/webp", "WebP image");
            }

            if (string.Equals(brand, "WAVE", StringComparison.Ordinal))
            {
                return FileSignature.For(MediaKind.Audio, ImageFormat.Unknown, "audio/wav", "WAV audio");
            }

            if (string.Equals(brand, "AVI ", StringComparison.Ordinal))
            {
                return FileSignature.For(MediaKind.Video, ImageFormat.Unknown, "video/x-msvideo", "AVI video");
            }
        }

        // ── ISO base media: MP4, MOV, HEIC and AVIF all start with ftyp ──
        // The brand at offset 8 is the only thing separating an iPhone photo from an iPhone
        // video, which makes this the branch that decides whether an upload is a 5 MB image
        // or a 500 MB video — and therefore which ceiling applies.

        if (head.Length >= 12 && StartsWithAscii(head[4..], "ftyp"))
        {
            var brand = Ascii(head.Slice(8, 4));

            return brand switch
            {
                "avif" or "avis" => FileSignature.For(
                    MediaKind.Image, ImageFormat.Avif, "image/avif", "AVIF image"),

                "heic" or "heix" or "hevc" or "heim" or "heis" or "mif1" or "msf1" =>
                    FileSignature.For(MediaKind.Image, ImageFormat.Heic, "image/heic", "HEIC image"),

                "qt  " => FileSignature.For(
                    MediaKind.Video, ImageFormat.Unknown, "video/quicktime", "QuickTime video"),

                _ => FileSignature.For(MediaKind.Video, ImageFormat.Unknown, "video/mp4", "MP4 video")
            };
        }

        // ── Video and audio ──────────────────────────────────────────────

        if (StartsWith(head, [0x1A, 0x45, 0xDF, 0xA3]))
        {
            // Matroska and WebM share a container. WebM is the web-facing one, and calling it
            // video/webm is right far more often than calling it video/x-matroska.
            return FileSignature.For(MediaKind.Video, ImageFormat.Unknown, "video/webm", "WebM video");
        }

        if (StartsWith(head, [0x49, 0x44, 0x33]) || StartsWith(head, [0xFF, 0xFB]) ||
            StartsWith(head, [0xFF, 0xF3]) || StartsWith(head, [0xFF, 0xF2]))
        {
            return FileSignature.For(MediaKind.Audio, ImageFormat.Unknown, "audio/mpeg", "MP3 audio");
        }

        if (StartsWithAscii(head, "OggS"))
        {
            return FileSignature.For(MediaKind.Audio, ImageFormat.Unknown, "audio/ogg", "Ogg audio");
        }

        if (StartsWithAscii(head, "fLaC"))
        {
            return FileSignature.For(MediaKind.Audio, ImageFormat.Unknown, "audio/flac", "FLAC audio");
        }

        // ── Documents ────────────────────────────────────────────────────

        if (StartsWithAscii(head, "%PDF-"))
        {
            return FileSignature.For(MediaKind.Document, ImageFormat.Unknown, "application/pdf", "PDF document");
        }

        // The legacy Office compound-file format. Also used by .msi and a few installers,
        // which is exactly why an allow-list on extension runs alongside this.
        if (StartsWith(head, [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]))
        {
            return FileSignature.For(
                MediaKind.Document, ImageFormat.Unknown, "application/msword", "Office document");
        }

        // ── Archives ─────────────────────────────────────────────────────
        // Modern Office files (.docx, .xlsx, .pptx) are ZIP archives. They are reported as
        // archives here and separated by extension and declared type at the policy layer,
        // because the alternative — parsing the central directory for [Content_Types].xml —
        // means reading the whole file before deciding whether to accept a single byte of it.

        if (StartsWith(head, [0x50, 0x4B, 0x03, 0x04]) ||
            StartsWith(head, [0x50, 0x4B, 0x05, 0x06]) ||
            StartsWith(head, [0x50, 0x4B, 0x07, 0x08]))
        {
            return FileSignature.For(
                MediaKind.Archive, ImageFormat.Unknown, "application/zip", "ZIP archive");
        }

        if (StartsWith(head, [0x52, 0x61, 0x72, 0x21, 0x1A, 0x07]))
        {
            return FileSignature.For(
                MediaKind.Archive, ImageFormat.Unknown, "application/vnd.rar", "RAR archive");
        }

        if (StartsWith(head, [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C]))
        {
            return FileSignature.For(
                MediaKind.Archive, ImageFormat.Unknown, "application/x-7z-compressed", "7-Zip archive");
        }

        if (StartsWith(head, [0x1F, 0x8B]))
        {
            return FileSignature.For(
                MediaKind.Archive, ImageFormat.Unknown, "application/gzip", "GZip archive");
        }

        return FileSignature.Unrecognized;
    }

    private static bool StartsWith(ReadOnlySpan<byte> head, ReadOnlySpan<byte> magic) =>
        head.Length >= magic.Length && head[..magic.Length].SequenceEqual(magic);

    private static bool StartsWithAscii(ReadOnlySpan<byte> head, string magic) =>
        head.Length >= magic.Length &&
        string.Equals(Ascii(head[..magic.Length]), magic, StringComparison.Ordinal);

    private static string Ascii(ReadOnlySpan<byte> bytes) => Encoding.ASCII.GetString(bytes);
}
