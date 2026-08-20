using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Domain.ValueObjects;

/// <summary>
/// A description of one image rendition: how big, how encoded, how cropped.
/// <para>
/// A value object rather than six loose parameters because it is also a <em>cache key</em>.
/// Two requests for the same transformation must produce the same variant and the same stored
/// object, and structural equality is what makes "same transformation" a decidable question
/// instead of a convention.
/// </para>
/// <para>
/// Bounded on every axis. The ceilings are not arbitrary caution: an endpoint that accepts an
/// arbitrary width is an endpoint where one attacker mints ten thousand renditions of a 15 MB
/// photograph, and the storage bill arrives before the alert does.
/// </para>
/// </summary>
public sealed class ImageTransformation : ValueObject
{
    public const int MinEdge = 16;

    public const int MaxEdge = 4096;

    public const int MinQuality = 40;

    public const int MaxQuality = 100;

    /// <summary>
    /// The picker's own compression setting. Every <c>launchImageLibraryAsync</c> call in the
    /// app passes <c>quality: 0.8</c>, so matching it means a re-encode does not visibly
    /// degrade what the client already compressed.
    /// </summary>
    public const int DefaultQuality = 80;

    private ImageTransformation(
        int longestEdge, ResizeMode mode, ImageFormat format, int quality, bool stripMetadata)
    {
        LongestEdge = longestEdge;
        Mode = mode;
        Format = format;
        Quality = quality;
        StripMetadata = stripMetadata;
    }

    /// <summary>The bounding square the image is fitted into.</summary>
    public int LongestEdge { get; }

    public ResizeMode Mode { get; }

    /// <summary><see cref="ImageFormat.Unknown"/> keeps the source format.</summary>
    public ImageFormat Format { get; }

    public int Quality { get; }

    public bool StripMetadata { get; }

    /// <summary>
    /// The transformation behind one rung of the fixed ladder.
    /// <para>
    /// The ladder is what a client may ask for by name. Thumbnails cover-crop to a square
    /// because that is what an avatar is; everything else fits inside its box, because
    /// cropping a listing photo to a card's aspect ratio silently removes the part the member
    /// framed.
    /// </para>
    /// </summary>
    public static Result<ImageTransformation> ForPurpose(MediaVariantPurpose purpose) => purpose switch
    {
        MediaVariantPurpose.Thumbnail => Result.Success(
            new ImageTransformation(96, ResizeMode.Cover, ImageFormat.Unknown, DefaultQuality, true)),

        MediaVariantPurpose.Small => Result.Success(
            new ImageTransformation(320, ResizeMode.Contain, ImageFormat.Unknown, DefaultQuality, true)),

        MediaVariantPurpose.Medium => Result.Success(
            new ImageTransformation(600, ResizeMode.Contain, ImageFormat.Unknown, DefaultQuality, true)),

        MediaVariantPurpose.Large => Result.Success(
            new ImageTransformation(1280, ResizeMode.Contain, ImageFormat.Unknown, DefaultQuality, true)),

        // Deliberately tiny and heavily compressed: this is rendered blurred behind the real
        // image, so anything larger is bytes nobody will ever see.
        MediaVariantPurpose.Preview => Result.Success(
            new ImageTransformation(32, ResizeMode.Contain, ImageFormat.Unknown, MinQuality, true)),

        _ => Result.Failure<ImageTransformation>(Error.Validation(
            MediaErrorCodes.VariantNotSupported,
            $"'{purpose}' is not an image rendition."))
    };

    public static Result<ImageTransformation> TryCreate(
        int longestEdge,
        ResizeMode mode = ResizeMode.Contain,
        ImageFormat format = ImageFormat.Unknown,
        int quality = DefaultQuality,
        bool stripMetadata = true)
    {
        if (longestEdge is < MinEdge or > MaxEdge)
        {
            return Result.Failure<ImageTransformation>(Error.Validation(
                MediaErrorCodes.TransformationInvalid,
                $"A rendition's longest edge must be between {MinEdge} and {MaxEdge} pixels."));
        }

        if (quality is < MinQuality or > MaxQuality)
        {
            return Result.Failure<ImageTransformation>(Error.Validation(
                MediaErrorCodes.TransformationInvalid,
                $"Quality must be between {MinQuality} and {MaxQuality}."));
        }

        // A vector image is never rasterised on this platform — see the security guide. Naming
        // it as an output format would create the one code path that decodes untrusted XML.
        if (format == ImageFormat.Svg)
        {
            return Result.Failure<ImageTransformation>(Error.Validation(
                MediaErrorCodes.TransformationInvalid, "SVG is not a rendition format."));
        }

        return Result.Success(
            new ImageTransformation(longestEdge, mode, format, quality, stripMetadata));
    }

    /// <summary>
    /// The stable identity of this transformation, used as the variant's cache key and as part
    /// of its storage key. Deterministic across processes and releases.
    /// </summary>
    public string ToCacheKey() =>
        $"e{LongestEdge}-m{(int)Mode}-f{(int)Format}-q{Quality}-s{(StripMetadata ? 1 : 0)}";

    public override string ToString() => ToCacheKey();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return LongestEdge;
        yield return Mode;
        yield return Format;
        yield return Quality;
        yield return StripMetadata;
    }
}
