namespace Diaspora.Modules.Media.Extensibility.Processing;

/// <summary>
/// Describes what is in an asset, automatically.
/// <para>
/// The immediate use is not search — it is accessibility. Thirty-one media fields across the
/// client and not one of them has alt text, because no upload flow asks for it and nobody
/// types it when asked. A tagger that fills <c>AltText</c> is the difference between a
/// marketplace a screen-reader user can shop and one they cannot.
/// </para>
/// <para>
/// Whatever a tagger produces is a suggestion. It fills <c>AltText</c> only when the uploader
/// supplied none, and never overwrites what a member wrote.
/// </para>
/// </summary>
public interface IMediaTagger : IMediaProcessor
{
    Task<MediaProcessingOutcome> TagAsync(
        MediaProcessingContext context, CancellationToken cancellationToken = default);
}
