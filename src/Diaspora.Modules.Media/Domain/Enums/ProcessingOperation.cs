namespace Diaspora.Modules.Media.Domain.Enums;

/// <summary>
/// One named unit of work a processor can perform on an asset.
/// <para>
/// This is the vocabulary the pipeline and its contributors share. A processor declares which
/// of these it supports; the pipeline plans a job out of them and records the outcome of each
/// one separately, so "thumbnails generated, transcode unsupported, EXIF stripped" is a
/// readable state rather than a single opaque failure.
/// </para>
/// </summary>
public enum ProcessingOperation
{
    /// <summary>Read format, dimensions, duration, page count, EXIF.</summary>
    ExtractMetadata = 0,

    /// <summary>Confirm the bytes are what the extension and content type claim.</summary>
    ValidateSignature = 1,

    /// <summary>Anti-malware pass.</summary>
    ScanForMalware = 2,

    /// <summary>Remove EXIF, GPS and other embedded metadata.</summary>
    StripMetadata = 3,

    /// <summary>Bake the EXIF orientation flag into the pixels and clear it.</summary>
    CorrectOrientation = 4,

    /// <summary>Produce the fixed size ladder.</summary>
    GenerateThumbnails = 5,

    /// <summary>Re-encode at a lower quality without changing dimensions.</summary>
    Optimize = 6,

    /// <summary>Produce a WebP rendition.</summary>
    EncodeWebP = 7,

    /// <summary>Produce an AVIF rendition.</summary>
    EncodeAvif = 8,

    /// <summary>Re-encode a video to a web-safe codec and bitrate.</summary>
    TranscodeVideo = 9,

    /// <summary>Lift a still from a video for use as a poster frame.</summary>
    ExtractPosterFrame = 10,

    /// <summary>Read a media file's playing time.</summary>
    ExtractDuration = 11,

    /// <summary>Optical character recognition.</summary>
    RecognizeText = 12,

    /// <summary>Automatic subject tagging.</summary>
    AutoTag = 13,

    /// <summary>Automated content moderation.</summary>
    Moderate = 14,

    /// <summary>Compute the tiny blurred placeholder.</summary>
    GeneratePreview = 15
}
