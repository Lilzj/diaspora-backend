namespace Diaspora.Modules.Guides.Domain.KnowledgeAnalytics;

/// <summary>Reads and writes the per-country usage counters.</summary>
public interface ICountryKnowledgeAnalyticsRepository
{
    /// <summary>The counters for one country, tracked, with the category breakdown loaded.</summary>
    Task<CountryKnowledgeAnalytics?> GetAsync(
        string countryCode, CancellationToken cancellationToken = default);

    /// <summary>The counters for one country, untracked. For the dashboard.</summary>
    Task<CountryKnowledgeAnalytics?> GetForReadAsync(
        string countryCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// The countries readers are most interested in, by directory views.
    /// <para>
    /// Bounded rather than paged: this feeds an "explore" grid and an editorial planning screen,
    /// both of which want a leaderboard. Anybody who wants the whole set wants the directory
    /// listing instead, which is already unpaged and small.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<CountryKnowledgeAnalytics>> TopByInterestAsync(
        int limit, CancellationToken cancellationToken = default);

    Task AddAsync(
        CountryKnowledgeAnalytics analytics, CancellationToken cancellationToken = default);
}
