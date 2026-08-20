using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.Assets;

/// <summary>
/// One derived rendition of an asset: a thumbnail, a 600-pixel copy, a transcoded video.
/// <para>
/// An entity inside the <see cref="MediaAsset"/> aggregate, never loaded or created on its
/// own. That is what keeps "at most one variant per purpose" true: a variant that could be
/// created independently is a variant two concurrent processing runs can duplicate, and the
/// read path would then have to pick one.
/// </para>
/// <para>
/// <see cref="TransformationKey"/> is the deduplication handle. Two requests for the same
/// transformation of the same asset must resolve to the same stored object rather than
/// producing two, and comparing a deterministic key is how that question is answered without
/// re-deriving the transformation.
/// </para>
/// </summary>
public sealed class MediaVariant : Entity<MediaVariantId>
{
    public const int MaxTransformationKeyLength = 128;

    public MediaId MediaId { get; private set; } = default!;

    public MediaVariantPurpose Purpose { get; private set; }

    public StorageLocation Location { get; private set; } = default!;

    public MediaContentType ContentType { get; private set; } = default!;

    public ByteSize Size { get; private set; } = ByteSize.Zero;

    /// <summary>Pixel size, when the variant is visual.</summary>
    public MediaDimensions? Dimensions { get; private set; }

    /// <summary>
    /// The deterministic identity of the transformation that produced this rendition, from
    /// <see cref="ImageTransformation.ToCacheKey"/> or
    /// <see cref="VideoTransformation.ToCacheKey"/>. Null for a rendition produced by
    /// something with no parameters — extracted text, for instance.
    /// </summary>
    public string? TransformationKey { get; private set; }

    public DateTimeOffset GeneratedAt { get; private set; }

    private MediaVariant() { }

    private MediaVariant(
        MediaVariantId id,
        MediaId mediaId,
        MediaVariantPurpose purpose,
        StorageLocation location,
        MediaContentType contentType,
        ByteSize size,
        MediaDimensions? dimensions,
        string? transformationKey) : base(id)
    {
        MediaId = mediaId;
        Purpose = purpose;
        Location = location;
        ContentType = contentType;
        Size = size;
        Dimensions = dimensions;
        TransformationKey = transformationKey;
        GeneratedAt = DateTimeOffset.UtcNow;
    }

    internal static MediaVariant Create(
        MediaId mediaId,
        MediaVariantPurpose purpose,
        StorageLocation location,
        MediaContentType contentType,
        ByteSize size,
        MediaDimensions? dimensions,
        string? transformationKey)
    {
        ArgumentNullException.ThrowIfNull(mediaId);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(contentType);
        ArgumentNullException.ThrowIfNull(size);

        return new MediaVariant(
            MediaVariantId.New(),
            mediaId,
            purpose,
            location,
            contentType,
            size,
            dimensions,
            Truncate(transformationKey, MaxTransformationKeyLength));
    }

    /// <summary>Whether this rendition was produced by the given transformation.</summary>
    public bool Matches(string? transformationKey) =>
        string.Equals(TransformationKey, transformationKey, StringComparison.Ordinal);

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
