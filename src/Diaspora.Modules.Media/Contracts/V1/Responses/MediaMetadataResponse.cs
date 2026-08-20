namespace Diaspora.Modules.Media.Contracts.V1.Responses;

/// <summary>
/// What the platform discovered about a file.
/// <para>
/// <b>Every field name here is the client's, not the server's.</b> <c>width</c>,
/// <c>height</c>, <c>durationSeconds</c> and <c>pageCount</c> are exactly the four properties
/// the frontend's <c>ImageAttachment</c>, <c>VoiceAttachment</c> and <c>DocumentAttachment</c>
/// already declare and never populate. Keeping the names means the client's existing renderers
/// work against real data without a rewrite — which was the point of doing the contract
/// analysis before writing any of this.
/// </para>
/// </summary>
public sealed record MediaMetadataResponse
{
    public int? Width { get; init; }

    public int? Height { get; init; }

    /// <summary>Playing time for audio and video. The client's <c>durationSeconds</c>.</summary>
    public double? DurationSeconds { get; init; }

    public int? PageCount { get; init; }

    /// <summary>
    /// The EXIF orientation flag the file arrived with, 1–8.
    /// <para>
    /// Reported so a client can rotate an image the server could not — a deployment with no
    /// image codec strips the metadata but cannot bake the rotation into the pixels.
    /// </para>
    /// </summary>
    public int? Orientation { get; init; }

    /// <summary>A tiny blurred placeholder to render while the real image loads.</summary>
    public string? BlurHash { get; init; }

    /// <summary>
    /// The SHA-256 digest of the file's bytes.
    /// <para>
    /// Published because the client's own anti-spam check fingerprints re-used images by URL,
    /// which any re-upload defeats. Trust can ask the same question of the bytes. Media forms
    /// no opinion about what reuse means.
    /// </para>
    /// </summary>
    public string? ContentHash { get; init; }
}
