using Diaspora.Modules.Guides.Domain.Identifiers;

namespace Diaspora.Modules.Guides.Domain.LearningPaths;

/// <summary>
/// Reads and writes curated learning paths.
/// <para>
/// Every read loads the steps. A path without its steps is a title, and no caller in this module
/// wants one — which is why there is no card-shaped read here, unlike the guide repository where a
/// twenty-card page really would be several thousand rows.
/// </para>
/// </summary>
public interface ILearningPathRepository
{
    /// <summary>The full graph, tracked. For commands.</summary>
    Task<LearningPath?> GetAsync(LearningPathId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// One path by the pair that identifies it publicly.
    /// <para>
    /// The country is half the key, deliberately: <c>arriving</c> is the natural key for the
    /// arrival path of every country, and forcing curators to invent <c>arriving-mu</c>,
    /// <c>arriving-ca</c> would put the country in the key twice and in the URL twice.
    /// </para>
    /// </summary>
    Task<LearningPath?> GetByKeyAsync(
        string countryCode, string key, CancellationToken cancellationToken = default);

    /// <summary>The full graph, untracked. For the onboarding screen.</summary>
    Task<LearningPath?> GetForReadAsync(
        string countryCode, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every path for a country, ordered by title.
    /// <para>
    /// Not paged. A country with more than a handful of onboarding routes has a navigation problem
    /// that pagination would hide rather than solve, and the ceiling in <c>KnowledgeLimits</c>
    /// keeps a single path bounded.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<LearningPath>> ListByCountryAsync(
        string countryCode, bool publishedOnly = true, CancellationToken cancellationToken = default);

    /// <summary>Whether a key is taken within a country. Advisory — the unique index rules.</summary>
    Task<bool> KeyExistsAsync(
        string countryCode, string key, CancellationToken cancellationToken = default);

    Task AddAsync(LearningPath path, CancellationToken cancellationToken = default);
}
