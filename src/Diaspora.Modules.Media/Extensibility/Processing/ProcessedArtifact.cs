using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.ValueObjects;

namespace Diaspora.Modules.Media.Extensibility.Processing;

/// <summary>
/// Bytes a processor produced, on their way to becoming a variant.
/// <para>
/// A processor hands the pipeline a stream and says what it is; the pipeline stores it and
/// records the variant. That division is what keeps a codec entirely free of storage
/// knowledge — an image processor never learns what a bucket is, and a storage provider never
/// learns what a thumbnail is.
/// </para>
/// <para>
/// The pipeline disposes <see cref="Content"/>. A processor that also disposes it will find
/// the pipeline reading a closed stream.
/// </para>
/// </summary>
public sealed class ProcessedArtifact : IDisposable
{
    private bool _disposed;

    public ProcessedArtifact(
        MediaVariantPurpose purpose,
        Stream content,
        MediaContentType contentType,
        FileExtension extension,
        MediaDimensions? dimensions = null,
        string? transformationKey = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(contentType);
        ArgumentNullException.ThrowIfNull(extension);

        Purpose = purpose;
        Content = content;
        ContentType = contentType;
        Extension = extension;
        Dimensions = dimensions;
        TransformationKey = transformationKey;
    }

    public MediaVariantPurpose Purpose { get; }

    /// <summary>The produced bytes. Owned by the pipeline once handed over.</summary>
    public Stream Content { get; }

    public MediaContentType ContentType { get; }

    public FileExtension Extension { get; }

    public MediaDimensions? Dimensions { get; }

    /// <summary>
    /// The deterministic identity of the transformation, so a second request for the same
    /// rendition resolves to this variant rather than producing another.
    /// </summary>
    public string? TransformationKey { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Content.Dispose();
        _disposed = true;
    }
}
