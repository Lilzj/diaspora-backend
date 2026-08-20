using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Domain.ValueObjects;

/// <summary>
/// A description of one video rendition: how tall, how many bits, which codec.
/// <para>
/// Separate from <see cref="ImageTransformation"/> rather than a shared "transformation" type
/// with nullable video fields. They share no axis: a video has a bitrate and a codec and no
/// resize mode, an image has a quality and a format and no frame rate, and a merged type would
/// be half-null in both directions with invariants nobody can state.
/// </para>
/// <para>
/// Expressed by <em>height</em> rather than longest edge because that is how the industry
/// names video renditions — 720p is a height — and a ladder whose rungs do not match what an
/// operator expects is a ladder that gets misconfigured.
/// </para>
/// </summary>
public sealed class VideoTransformation : ValueObject
{
    public const int MinHeight = 144;

    public const int MaxHeight = 2160;

    public const int MaxBitrateKbps = 20_000;

    /// <summary>
    /// The one rendition every video gets. H.264 in an MP4 container is the only combination
    /// that plays everywhere the app runs — iOS, Android, and every browser — without a
    /// per-platform fallback.
    /// </summary>
    public static readonly VideoTransformation WebSafe720P = new(720, 2_500, "h264", "aac", "mp4");

    private VideoTransformation(
        int height, int bitrateKbps, string videoCodec, string audioCodec, string container)
    {
        Height = height;
        BitrateKbps = bitrateKbps;
        VideoCodec = videoCodec;
        AudioCodec = audioCodec;
        Container = container;
    }

    public int Height { get; }

    public int BitrateKbps { get; }

    public string VideoCodec { get; }

    public string AudioCodec { get; }

    public string Container { get; }

    public static Result<VideoTransformation> TryCreate(
        int height,
        int bitrateKbps,
        string videoCodec = "h264",
        string audioCodec = "aac",
        string container = "mp4")
    {
        if (height is < MinHeight or > MaxHeight)
        {
            return Result.Failure<VideoTransformation>(Error.Validation(
                MediaErrorCodes.TransformationInvalid,
                $"A video rendition's height must be between {MinHeight} and {MaxHeight}."));
        }

        if (bitrateKbps is <= 0 or > MaxBitrateKbps)
        {
            return Result.Failure<VideoTransformation>(Error.Validation(
                MediaErrorCodes.TransformationInvalid,
                $"Bitrate must be between 1 and {MaxBitrateKbps} kbps."));
        }

        if (string.IsNullOrWhiteSpace(videoCodec) ||
            string.IsNullOrWhiteSpace(audioCodec) ||
            string.IsNullOrWhiteSpace(container))
        {
            return Result.Failure<VideoTransformation>(Error.Validation(
                MediaErrorCodes.TransformationInvalid,
                "A video rendition needs a codec pair and a container."));
        }

        return Result.Success(new VideoTransformation(
            height,
            bitrateKbps,
            videoCodec.Trim().ToLowerInvariant(),
            audioCodec.Trim().ToLowerInvariant(),
            container.Trim().ToLowerInvariant()));
    }

    public string ToCacheKey() => $"h{Height}-b{BitrateKbps}-{VideoCodec}-{AudioCodec}-{Container}";

    public override string ToString() => ToCacheKey();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Height;
        yield return BitrateKbps;
        yield return VideoCodec;
        yield return AudioCodec;
        yield return Container;
    }
}
