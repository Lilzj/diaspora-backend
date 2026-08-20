namespace Diaspora.Modules.Media.Contracts.V1.Responses;

/// <summary>
/// One digital asset, as every consumer sees it.
/// <para>
/// <b>The contract that ends thirty-one media fields with fourteen names.</b> Every business
/// module stores <c>mediaId</c> and renders from this; none of them stores a URL, a bucket
/// name, or a storage key. That is what makes changing storage provider invisible to
/// consumers, and it is the single constraint B5 exists to impose.
/// </para>
/// <para>
/// <see cref="Url"/> and <see cref="ThumbnailUrl"/> are conveniences over
/// <see cref="Variants"/> — they carry the names the client already uses so a renderer written
/// against the mock data needs no restructuring. They are routes into Media, never provider
/// URLs.
/// </para>
/// </summary>
public sealed record MediaAssetResponse
{
    public required Guid MediaId { get; init; }

    /// <summary><c>image</c>, <c>video</c>, <c>audio</c>, <c>document</c>, and so on.</summary>
    public required string Kind { get; init; }

    /// <summary><c>pending</c>, <c>processing</c>, <c>ready</c>, <c>quarantined</c>, <c>failed</c>.</summary>
    public required string Status { get; init; }

    /// <summary><c>private</c>, <c>authenticated</c>, <c>public</c>.</summary>
    public required string Visibility { get; init; }

    /// <summary>The client's <c>mimeType</c>.</summary>
    public required string ContentType { get; init; }

    /// <summary>The client's <c>fileName</c>. The name the member gave it, not the stored one.</summary>
    public required string FileName { get; init; }

    /// <summary>The client's <c>sizeBytes</c>.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>The original. A route into Media, signed when the asset is private.</summary>
    public required string Url { get; init; }

    /// <summary>The client's <c>thumbnailUrl</c>. Null when no rendition exists yet.</summary>
    public string? ThumbnailUrl { get; init; }

    public required MediaMetadataResponse Metadata { get; init; }

    public required IReadOnlyList<MediaVariantResponse> Variants { get; init; }

    /// <summary>Accessible description. Filled by the uploader or by an auto-tagger.</summary>
    public string? AltText { get; init; }

    /// <summary>The client's <c>ImageAttachment.caption</c>.</summary>
    public string? Caption { get; init; }

    /// <summary>What the asset was uploaded for. Null while it is unclaimed.</summary>
    public MediaReferenceResponse? Reference { get; init; }

    /// <summary>
    /// <c>notScanned</c>, <c>scanning</c>, <c>clean</c>, <c>infected</c>, <c>failed</c>,
    /// <c>skipped</c>.
    /// <para>
    /// <c>skipped</c> means this deployment has no scanner registered. Distinct from
    /// <c>clean</c> on purpose — a client showing a "verified safe" badge must not show it for
    /// a file nothing looked at.
    /// </para>
    /// </summary>
    public required string ScanStatus { get; init; }

    /// <summary>Whether reading this asset needs a signed link.</summary>
    public required bool RequiresSignedAccess { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
