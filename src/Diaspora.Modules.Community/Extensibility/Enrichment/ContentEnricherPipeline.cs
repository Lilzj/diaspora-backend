using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Community.Extensibility.Enrichment;

/// <summary>
/// Runs the registered enrichers, and swallows their failures.
/// <para>
/// Swallowing is the design, not laziness. Every enricher is optional by definition — the post
/// is complete without it — so an enricher throwing must degrade the post, not reject it. The
/// failure is logged at warning with the enricher named, so a permanently broken one is visible
/// rather than silent.
/// </para>
/// <para>
/// Cancellation is deliberately <em>not</em> swallowed: a cancelled request should stop, not
/// continue running enrichers nobody is waiting for.
/// </para>
/// </summary>
public sealed class ContentEnricherPipeline
{
    private readonly List<IContentEnricher> _enrichers;
    private readonly ILogger<ContentEnricherPipeline> _logger;

    public ContentEnricherPipeline(
        IEnumerable<IContentEnricher> enrichers, ILogger<ContentEnricherPipeline> logger)
    {
        ArgumentNullException.ThrowIfNull(enrichers);

        _enrichers = enrichers
            .OrderBy(enricher => enricher.Order)
            .ThenBy(enricher => enricher.Name, StringComparer.Ordinal)
            .ToList();

        _logger = logger;
    }

    public int Count => _enrichers.Count;

    public async Task RunAsync(
        ContentEnrichmentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var enricher in _enrichers)
        {
            try
            {
                await enricher.EnrichAsync(context, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // An enricher must never be able to fail a publish.
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Content enricher {Enricher} failed for post {PostId}. The post is unaffected.",
                    enricher.Name,
                    context.Post.Id.Value);
            }
#pragma warning restore CA1031
        }
    }
}
