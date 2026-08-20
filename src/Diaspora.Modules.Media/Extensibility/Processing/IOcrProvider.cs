namespace Diaspora.Modules.Media.Extensibility.Processing;

/// <summary>
/// Reads text out of an image or a document.
/// <para>
/// Declared now, unimplemented now, and the shape is what matters. Two consumers are already
/// visible in the roadmap: Trust wants to cross-check a passport number against what a
/// reviewer typed, and Search wants a scanned guide to be findable by its contents. Both are
/// later phases, and both would otherwise reach for their own OCR — at which point the
/// platform has two, neither of which knows about the other's results.
/// </para>
/// <para>
/// Recognised text becomes a variant with purpose
/// <see cref="Domain.Enums.MediaVariantPurpose.ExtractedText"/>, stored as an object rather
/// than a column. A scanned forty-page document's text is not something to put in a row that
/// every asset query then carries.
/// </para>
/// </summary>
public interface IOcrProvider : IMediaProcessor
{
    Task<MediaProcessingOutcome> RecognizeAsync(
        MediaProcessingContext context, CancellationToken cancellationToken = default);
}
