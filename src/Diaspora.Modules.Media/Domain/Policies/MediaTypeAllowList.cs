using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.ValueObjects;

namespace Diaspora.Modules.Media.Domain.Policies;

/// <summary>
/// What the platform accepts, by kind.
/// <para>
/// An allow-list, never a deny-list. A deny-list is a claim to have enumerated every dangerous
/// format, which nobody can make and which every new format invalidates. An allow-list is a
/// claim to have enumerated what the product needs, which the frontend contract report already
/// did.
/// </para>
/// <para>
/// Two formats are conspicuously absent and their absence is deliberate.
/// </para>
/// <list type="bullet">
/// <item>
/// <b>SVG.</b> An SVG is an XML document that may contain script and external entity
/// references. Serving one from the platform's own origin is a stored cross-site-scripting
/// vector; sanitising one correctly is a research problem. The frontend renders no SVG from
/// user content, so accepting it would add a serious attack surface for no feature.
/// </item>
/// <item>
/// <b>Anything executable.</b> Not because a stored <c>.exe</c> runs on the server — it does
/// not — but because a platform members trust is an excellent place to host one for somebody
/// else to download.
/// </item>
/// </list>
/// </summary>
public static class MediaTypeAllowList
{
    private static readonly HashSet<string> ImageTypes = new(StringComparer.Ordinal)
    {
        "image/jpeg", "image/jpg", "image/pjpeg",
        "image/png", "image/x-png",
        "image/gif",
        "image/webp",
        "image/avif",

        // The iOS camera's default. Rejecting it rejects the default output of every iPhone.
        "image/heic", "image/heif",

        "image/bmp", "image/tiff"
    };

    private static readonly HashSet<string> VideoTypes = new(StringComparer.Ordinal)
    {
        "video/mp4", "video/quicktime", "video/webm", "video/x-msvideo", "video/mpeg"
    };

    private static readonly HashSet<string> AudioTypes = new(StringComparer.Ordinal)
    {
        "audio/mpeg", "audio/mp4", "audio/aac", "audio/wav", "audio/x-wav",
        "audio/ogg", "audio/webm", "audio/flac",

        // What an iOS voice note actually arrives as.
        "audio/x-m4a", "audio/m4a"
    };

    private static readonly HashSet<string> DocumentTypes = new(StringComparer.Ordinal)
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "application/rtf",
        "text/plain",
        "text/csv"
    };

    private static readonly HashSet<string> ArchiveTypes = new(StringComparer.Ordinal)
    {
        "application/zip", "application/x-zip-compressed",
        "application/x-7z-compressed", "application/vnd.rar",
        "application/gzip", "application/x-tar"
    };

    /// <summary>
    /// What a verification document may be. The narrowest list on the platform, and it is the
    /// client's own published one: "Supports PDF, PNG, JPG".
    /// </summary>
    private static readonly HashSet<string> VerificationTypes = new(StringComparer.Ordinal)
    {
        "application/pdf", "image/png", "image/jpeg", "image/jpg", "image/heic", "image/heif"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.Ordinal)
    {
        "jpg", "jpeg", "png", "gif", "webp", "avif", "heic", "heif", "bmp", "tif", "tiff"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.Ordinal)
    {
        "mp4", "mov", "webm", "avi", "mpeg", "mpg", "m4v"
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.Ordinal)
    {
        "mp3", "m4a", "aac", "wav", "ogg", "oga", "opus", "flac"
    };

    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.Ordinal)
    {
        "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "rtf", "txt", "csv"
    };

    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.Ordinal)
    {
        "zip", "7z", "rar", "gz", "tar", "tgz"
    };

    /// <summary>Whether a declared content type is acceptable for the requested kind.</summary>
    public static bool AllowsContentType(MediaKind kind, MediaContentType contentType)
    {
        ArgumentNullException.ThrowIfNull(contentType);

        return TypesFor(kind).Contains(contentType.Value);
    }

    /// <summary>
    /// Whether an extension is acceptable for the requested kind. An absent extension passes:
    /// plenty of camera captures arrive without one, and the signature check is the real gate.
    /// </summary>
    public static bool AllowsExtension(MediaKind kind, FileExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);

        return !extension.HasValue || ExtensionsFor(kind).Contains(extension.Value);
    }

    /// <summary>
    /// Whether sniffed bytes are acceptable for the requested kind.
    /// <para>
    /// The modern Office formats are the awkward case: a <c>.docx</c> is a ZIP archive, so its
    /// signature says "archive" while its declared type and extension say "document". Rather
    /// than parse the archive's central directory — which means reading the whole file before
    /// accepting any of it — a ZIP is accepted as a document when the extension agrees. The
    /// combination an attacker would need is a real Office extension, a real Office content
    /// type, and ZIP bytes, which is a <c>.docx</c>.
    /// </para>
    /// </summary>
    public static bool AllowsSignature(MediaKind kind, FileSignature signature, FileExtension extension)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(extension);

        if (!signature.IsRecognized)
        {
            return false;
        }

        if (signature.Kind == kind)
        {
            return true;
        }

        var zipBackedDocument =
            kind is MediaKind.Document &&
            signature.Kind is MediaKind.Archive &&
            extension.Value is "docx" or "xlsx" or "pptx";

        // Verification evidence is a PDF or an image; the kind is a policy label rather than a
        // format, so the signature is checked against what the label permits.
        var verificationEvidence =
            kind is MediaKind.VerificationDocument &&
            signature.Kind is MediaKind.Document or MediaKind.Image;

        return zipBackedDocument || verificationEvidence;
    }

    private static HashSet<string> TypesFor(MediaKind kind) => kind switch
    {
        MediaKind.Image => ImageTypes,
        MediaKind.Video => VideoTypes,
        MediaKind.Audio => AudioTypes,
        MediaKind.Document => DocumentTypes,
        MediaKind.Archive => ArchiveTypes,
        MediaKind.VerificationDocument => VerificationTypes,
        _ => []
    };

    private static HashSet<string> ExtensionsFor(MediaKind kind) => kind switch
    {
        MediaKind.Image => ImageExtensions,
        MediaKind.Video => VideoExtensions,
        MediaKind.Audio => AudioExtensions,
        MediaKind.Document => DocumentExtensions,
        MediaKind.Archive => ArchiveExtensions,
        MediaKind.VerificationDocument => ["pdf", "png", "jpg", "jpeg", "heic", "heif"],
        _ => []
    };
}
