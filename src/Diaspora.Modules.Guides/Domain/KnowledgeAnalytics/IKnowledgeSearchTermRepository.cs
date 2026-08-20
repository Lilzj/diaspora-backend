namespace Diaspora.Modules.Guides.Domain.KnowledgeAnalytics;

/// <summary>Reads and writes the per-country search phrase counters.</summary>
public interface IKnowledgeSearchTermRepository
{
    /// <summary>One phrase within one country, tracked. The upsert path on every search.</summary>
    Task<KnowledgeSearchTerm?> GetAsync(
        string countryCode, string normalizedTerm, CancellationToken cancellationToken = default);

    /// <summary>
    /// The most searched phrases, most recent activity first among equals.
    /// <para>
    /// A country of the empty string returns the platform-wide phrases — the ones searched without
    /// naming a country — rather than the union of every country, because those are different
    /// questions and summing them would let one large diaspora define what "trending" means
    /// everywhere.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<KnowledgeSearchTerm>> TopAsync(
        string countryCode,
        int limit,
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The phrases that currently find nothing, most searched first. The commissioning queue.
    /// </summary>
    Task<IReadOnlyList<KnowledgeSearchTerm>> UnansweredAsync(
        string countryCode, int limit, CancellationToken cancellationToken = default);

    Task AddAsync(KnowledgeSearchTerm term, CancellationToken cancellationToken = default);
}
