using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.ValueObjects;

namespace Diaspora.Modules.Media.Extensibility.Processing;

/// <summary>
/// What a processor is given: the bytes, and everything already known about them.
/// <para>
/// The source stream is opened by the pipeline and owned by the pipeline. A processor reads
/// it and does not dispose it — several processors run over the same asset, and a processor
/// that disposed the stream would break every one after it. When a processor needs to read
/// from the start it seeks; when the stream is not seekable the pipeline has already buffered
/// it.
/// </para>
/// <para>
/// <see cref="KnownMetadata"/> carries forward what earlier steps discovered, which is why
/// order matters in the job plan: a thumbnail generator that knows the source dimensions can
/// decline to upscale, and one that does not will happily produce a 1280-pixel rendition of a
/// 96-pixel avatar.
/// </para>
/// </summary>
public sealed class MediaProcessingContext
{
    public MediaProcessingContext(
        Stream source,
        MediaKind kind,
        MediaContentType contentType,
        SafeFileName fileName,
        ByteSize size,
        MediaMetadata knownMetadata)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(contentType);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(size);
        ArgumentNullException.ThrowIfNull(knownMetadata);

        Source = source;
        Kind = kind;
        ContentType = contentType;
        FileName = fileName;
        Size = size;
        KnownMetadata = knownMetadata;
    }

    /// <summary>The asset's bytes. Read; never dispose.</summary>
    public Stream Source { get; }

    public MediaKind Kind { get; }

    public MediaContentType ContentType { get; }

    public SafeFileName FileName { get; }

    public ByteSize Size { get; }

    /// <summary>What earlier steps in this job discovered.</summary>
    public MediaMetadata KnownMetadata { get; }

    /// <summary>
    /// Rewinds the source, so a processor always starts at byte zero regardless of what ran
    /// before it. Non-seekable streams are buffered by the pipeline before it gets here.
    /// </summary>
    public void RewindSource()
    {
        if (Source.CanSeek)
        {
            Source.Position = 0;
        }
    }
}
