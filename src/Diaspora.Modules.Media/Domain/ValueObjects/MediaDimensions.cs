using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Domain.ValueObjects;

/// <summary>
/// The pixel dimensions of an image or a video frame.
/// <para>
/// The client's <c>ImageAttachment</c> declares <c>width?</c> and <c>height?</c> and nothing in
/// the app computes them, so this is a field the server is expected to fill. It is also the
/// defence against a decompression bomb: a 30 KB PNG that declares 60 000 × 60 000 costs
/// fourteen gigabytes to decode, and the only place to refuse it is before decoding, from the
/// header.
/// </para>
/// </summary>
public sealed class MediaDimensions : ValueObject
{
    /// <summary>
    /// The largest edge accepted, in pixels. Comfortably above any real camera output and far
    /// below the point where decoding costs more memory than a request may have.
    /// </summary>
    public const int MaxEdge = 16_384;

    private MediaDimensions(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public long PixelCount => (long)Width * Height;

    public double AspectRatio => Height == 0 ? 0 : (double)Width / Height;

    public bool IsLandscape => Width > Height;

    public bool IsPortrait => Height > Width;

    public bool IsSquare => Width == Height;

    public static Result<MediaDimensions> TryCreate(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return Result.Failure<MediaDimensions>(Error.Validation(
                MediaErrorCodes.DimensionsExceeded, "Image dimensions must be positive."));
        }

        if (width > MaxEdge || height > MaxEdge)
        {
            return Result.Failure<MediaDimensions>(Error.Validation(
                MediaErrorCodes.DimensionsExceeded,
                $"An image may not exceed {MaxEdge} pixels on either edge."));
        }

        return Result.Success(new MediaDimensions(width, height));
    }

    public static MediaDimensions Create(int width, int height) =>
        TryCreate(width, height) is { IsSuccess: true } result
            ? result.Value
            : throw new ArgumentOutOfRangeException(nameof(width), "Not usable dimensions.");

    /// <summary>
    /// What these dimensions become when fitted inside a square box of
    /// <paramref name="longestEdge"/>, preserving aspect ratio and never enlarging.
    /// <para>
    /// Never enlarging is the important half. A 96-pixel avatar upscaled to a 1280-pixel
    /// "large" variant is bigger, blurrier and more expensive than the original, which is the
    /// opposite of what a size ladder is for.
    /// </para>
    /// </summary>
    public MediaDimensions ScaledToFit(int longestEdge)
    {
        if (longestEdge <= 0 || (Width <= longestEdge && Height <= longestEdge))
        {
            return this;
        }

        var scale = (double)longestEdge / Math.Max(Width, Height);

        return new MediaDimensions(
            Math.Max(1, (int)Math.Round(Width * scale)),
            Math.Max(1, (int)Math.Round(Height * scale)));
    }

    public override string ToString() => $"{Width}×{Height}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Width;
        yield return Height;
    }
}
