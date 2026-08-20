namespace Diaspora.Modules.Media.Domain.Enums;

/// <summary>
/// What sort of thing an asset is, at the coarsest useful grain.
/// <para>
/// Coarse on purpose. The kind decides the size ceiling, the permission required to upload,
/// which processors are offered the file and whether it is private by default — four
/// decisions that do not need to know JPEG from PNG. Format lives in
/// <see cref="ValueObjects.MediaContentType"/>; this is the policy axis.
/// </para>
/// <para>
/// <see cref="VerificationDocument"/> is a kind rather than a flag on
/// <see cref="Document"/> because it differs in every one of those four decisions: a 5 MB
/// ceiling the client already advertises, private visibility that cannot be widened, no
/// thumbnail generation, and an access rule Trust owns rather than the uploader.
/// </para>
/// </summary>
public enum MediaKind
{
    /// <summary>Not yet determined — the state an upload session starts in.</summary>
    Unknown = 0,

    Image = 1,
    Video = 2,
    Audio = 3,

    /// <summary>PDF and office formats.</summary>
    Document = 4,

    Archive = 5,

    /// <summary>Immigration and residence evidence. Private, small, never public.</summary>
    VerificationDocument = 6,

    /// <summary>Reserved. Model inputs and outputs for the AI module.</summary>
    AiAsset = 7,

    /// <summary>Reserved. glTF, USDZ and friends.</summary>
    Model3D = 8
}
