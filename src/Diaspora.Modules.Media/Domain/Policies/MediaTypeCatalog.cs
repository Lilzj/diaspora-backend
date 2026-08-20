using Diaspora.Modules.Media.Domain.Enums;

namespace Diaspora.Modules.Media.Domain.Policies;

/// <summary>
/// Publishes the allow-list, so a client can render an accurate file picker.
/// <para>
/// Separate from <see cref="MediaTypeAllowList"/>, which decides. This only reports, and the
/// separation is what keeps the decision surface small: a bug here shows a client the wrong
/// list of formats, a bug there accepts the wrong file.
/// </para>
/// <para>
/// Both read the same sets, so the published list cannot drift from the enforced one — which
/// is the failure the frontend already has, where the verification screen advertises "PDF,
/// PNG, JPG" and nothing checks it.
/// </para>
/// </summary>
public static class MediaTypeCatalog
{
    /// <summary>Content types a client may offer for a given kind.</summary>
    public static IReadOnlyList<string> AcceptedContentTypesFor(MediaKind kind) => kind switch
    {
        MediaKind.Image =>
        [
            "image/jpeg", "image/png", "image/gif", "image/webp",
            "image/avif", "image/heic", "image/heif"
        ],

        MediaKind.Video => ["video/mp4", "video/quicktime", "video/webm"],

        MediaKind.Audio => ["audio/mpeg", "audio/mp4", "audio/aac", "audio/wav", "audio/ogg"],

        MediaKind.Document =>
        [
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "text/plain",
            "text/csv"
        ],

        MediaKind.Archive => ["application/zip", "application/x-7z-compressed"],

        // The narrowest list on the platform, and it is the client's own published one:
        // "Supports PDF, PNG, JPG".
        MediaKind.VerificationDocument => ["application/pdf", "image/png", "image/jpeg"],

        _ => []
    };
}
