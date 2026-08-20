namespace Diaspora.Modules.Community.Extensibility.Enrichment;

/// <summary>
/// Adds something to a post that the author did not type.
/// <para>
/// Link previews, AI summaries, automatic translation, topic tags, a safety-information banner
/// on an emergency post: all of them are this, and none of them belongs in the aggregate. A post
/// that could not be created without a translation service is a post that cannot be created when
/// the translation service is down.
/// </para>
/// <para>
/// <b>An enricher must not fail a publish.</b> The pipeline catches and logs, because the worst
/// outcome of an enricher failing should be a post without a link preview — never a member's
/// safety alert being refused because an unfurler timed out.
/// </para>
/// </summary>
public interface IContentEnricher
{
    string Name { get; }

    int Order => 0;

    Task EnrichAsync(
        ContentEnrichmentContext context, CancellationToken cancellationToken = default);
}
