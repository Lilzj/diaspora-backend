using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Policies;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Modules.Media.Extensibility.Processing;

namespace Diaspora.Modules.Media.Infrastructure.Processing;

/// <summary>
/// The metadata extractor Media ships with. Reads image headers; declines everything else.
/// <para>
/// <b>The only processing capability the module implements itself</b>, and it is the one that
/// needed no dependency to implement. Because of it, a deployment with no image codec at all
/// still populates the client's <c>width</c> and <c>height</c> fields, still refuses a
/// decompression bomb, and still knows which photographs arrived carrying GPS coordinates.
/// </para>
/// <para>
/// It declines video duration, audio duration and PDF page counts rather than guessing. A
/// richer extractor registers at a lower <see cref="IMediaProcessor.Order"/> and wins; because
/// this one declines rather than fails, the better extractor never has to fight it.
/// </para>
/// </summary>
public sealed class HeaderMetadataExtractor : IMetadataExtractor
{
    public string Name => "header-metadata";

    /// <summary>
    /// Last. This is the floor, not the preference — anything a deployment installs should
    /// take precedence over reading bytes by hand.
    /// </summary>
    public int Order => 1000;

    public IReadOnlySet<ProcessingOperation> SupportedOperations { get; } =
        new HashSet<ProcessingOperation>
        {
            ProcessingOperation.ExtractMetadata,
            ProcessingOperation.ValidateSignature
        };

    public bool CanProcess(MediaKind kind, ProcessingOperation operation) =>
        SupportedOperations.Contains(operation);

    public async Task<MediaProcessingOutcome> ExtractAsync(
        MediaProcessingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.RewindSource();

        var buffer = new byte[ImageHeaderReader.HeaderLength];
        var read = await ReadAtLeastAsync(context.Source, buffer, cancellationToken);

        if (read == 0)
        {
            return MediaProcessingOutcome.Failed("The file is empty.");
        }

        var header = buffer.AsSpan(0, read);
        var signature = FileSignatureReader.Identify(header);

        if (!signature.IsRecognized)
        {
            return MediaProcessingOutcome.Declined("The file format was not recognised.");
        }

        if (signature.Kind is not MediaKind.Image)
        {
            // Duration and page counts need a demuxer and a PDF parser respectively. Declining
            // is honest; guessing would put a wrong number in a field a client renders.
            return MediaProcessingOutcome.Declined(
                $"Header extraction covers images only; this is {signature.Description}.");
        }

        var info = ImageHeaderReader.Read(header, signature.ImageFormat);

        if (info is null)
        {
            return MediaProcessingOutcome.Declined(
                $"Dimensions are not readable from a {signature.Description} header.");
        }

        var metadata = MediaMetadata.ForImage(
            info.Dimensions, info.Orientation, info.HasEmbeddedMetadata);

        return MediaProcessingOutcome.WithMetadata(
            metadata, $"{signature.Description}, {info.Dimensions}");
    }

    /// <summary>
    /// Fills as much of the buffer as the stream has.
    /// <para>
    /// A single <c>ReadAsync</c> is allowed to return one byte, and on a network-backed stream
    /// it routinely does. Parsing a header from whatever the first read happened to yield is
    /// the bug that makes signature detection flaky under load and impossible to reproduce
    /// locally.
    /// </para>
    /// </summary>
    private static async Task<int> ReadAtLeastAsync(
        Stream source, byte[] buffer, CancellationToken cancellationToken)
    {
        var total = 0;

        while (total < buffer.Length)
        {
            var read = await source.ReadAsync(buffer.AsMemory(total), cancellationToken);

            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }
}
