using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.ValueObjects;

/// <summary>
/// Everything discovered about an asset by inspecting it, as opposed to everything the
/// uploader claimed.
/// <para>
/// The field set is not invented: <c>width</c>, <c>height</c>, <c>durationSeconds</c> and
/// <c>pageCount</c> are exactly the four properties the client's attachment DTOs declare and
/// never populate, and this is the type that fills them. Keeping the client's names means its
/// renderers work unchanged.
/// </para>
/// <para>
/// Immutable, and replaced wholesale rather than mutated. Metadata is the output of a
/// processing pass; a half-updated metadata record — new dimensions, stale orientation — is a
/// state no reader can interpret.
/// </para>
/// </summary>
public sealed class MediaMetadata : ValueObject
{
    /// <summary>What is known before anything has inspected the file.</summary>
    public static readonly MediaMetadata Empty = new(null, null, null, null, false, null);

    /// <summary>
    /// For the persistence layer only.
    /// <para>
    /// EF cannot use the real constructor because <see cref="Dimensions"/> is itself an owned
    /// type, and owned references cannot be bound to constructor parameters. The materialiser
    /// writes the backing fields instead, which is why every property here is a get-only auto
    /// property rather than a readonly field.
    /// </para>
    /// </summary>
    private MediaMetadata() { }

    private MediaMetadata(
        MediaDimensions? dimensions,
        TimeSpan? duration,
        int? pageCount,
        int? orientation,
        bool hasEmbeddedMetadata,
        string? blurHash)
    {
        Dimensions = dimensions;
        Duration = duration;
        PageCount = pageCount;
        Orientation = orientation;
        HasEmbeddedMetadata = hasEmbeddedMetadata;
        BlurHash = blurHash;
    }

    /// <summary>Pixel size, for images and video.</summary>
    public MediaDimensions? Dimensions { get; }

    /// <summary>Playing time, for audio and video. The client's <c>durationSeconds</c>.</summary>
    public TimeSpan? Duration { get; }

    /// <summary>Page count, for documents. The client's <c>pageCount</c>.</summary>
    public int? PageCount { get; }

    /// <summary>
    /// The EXIF orientation flag, 1–8, when the file carried one.
    /// <para>
    /// Kept after correction rather than cleared, because "this image was rotated" is
    /// information a support case needs. A value above 1 on a <c>Ready</c> asset means the
    /// correction step did not run.
    /// </para>
    /// </summary>
    public int? Orientation { get; }

    /// <summary>
    /// Whether the file arrived carrying EXIF, IPTC or XMP. Almost always true for a phone
    /// photo, and almost always carrying GPS coordinates — which is why the pipeline strips it.
    /// </summary>
    public bool HasEmbeddedMetadata { get; }

    /// <summary>A compact blurred placeholder, when a processor produced one.</summary>
    public string? BlurHash { get; }

    /// <summary>True once anything at all has been learned about the file.</summary>
    public bool IsEmpty =>
        Dimensions is null && Duration is null && PageCount is null && BlurHash is null;

    public static MediaMetadata ForImage(
        MediaDimensions dimensions, int? orientation = null, bool hasEmbeddedMetadata = false)
    {
        ArgumentNullException.ThrowIfNull(dimensions);
        return new MediaMetadata(dimensions, null, null, orientation, hasEmbeddedMetadata, null);
    }

    public static MediaMetadata ForTimedMedia(TimeSpan duration, MediaDimensions? dimensions = null) =>
        new(dimensions, duration < TimeSpan.Zero ? TimeSpan.Zero : duration, null, null, false, null);

    public static MediaMetadata ForDocument(int pageCount) =>
        new(null, null, pageCount < 0 ? 0 : pageCount, null, false, null);

    public MediaMetadata WithDimensions(MediaDimensions dimensions)
    {
        ArgumentNullException.ThrowIfNull(dimensions);
        return new MediaMetadata(
            dimensions, Duration, PageCount, Orientation, HasEmbeddedMetadata, BlurHash);
    }

    public MediaMetadata WithDuration(TimeSpan duration) =>
        new(Dimensions, duration, PageCount, Orientation, HasEmbeddedMetadata, BlurHash);

    public MediaMetadata WithPageCount(int pageCount) =>
        new(Dimensions, Duration, pageCount, Orientation, HasEmbeddedMetadata, BlurHash);

    public MediaMetadata WithBlurHash(string? blurHash) =>
        new(Dimensions, Duration, PageCount, Orientation, HasEmbeddedMetadata, blurHash);

    /// <summary>
    /// Records that embedded metadata has been removed. Orientation is reset to 1 because the
    /// rotation is now baked into the pixels — leaving the original flag would make every
    /// downstream renderer rotate the image a second time.
    /// </summary>
    public MediaMetadata WithMetadataStripped() =>
        new(Dimensions, Duration, PageCount, orientation: 1, hasEmbeddedMetadata: false, BlurHash);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Dimensions;
        yield return Duration;
        yield return PageCount;
        yield return Orientation;
        yield return HasEmbeddedMetadata;
        yield return BlurHash;
    }
}
