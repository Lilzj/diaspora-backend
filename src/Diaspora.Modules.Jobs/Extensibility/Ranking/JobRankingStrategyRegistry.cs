using Diaspora.Modules.Jobs.Domain.Postings;

namespace Diaspora.Modules.Jobs.Extensibility.Ranking;

/// <summary>
/// Resolves a ranking strategy by key and applies it.
/// <para>
/// <b>Isolating each strategy is the whole job.</b> A registry that let one strategy's failure
/// escape would make a browse page fail because somebody's experimental ranking divided by zero.
/// A throwing strategy is skipped and the set keeps whatever order the repository returned,
/// which is always a defensible order rather than an error page.
/// </para>
/// <para>
/// <b>Scoped, not singleton.</b> A registry over scoped implementations registered as a
/// singleton is the captive dependency the host's provider validation refuses to start on — B4
/// recorded it, and B6, B7 and B8 each repeated the note. One dictionary construction per
/// request is a small price for not repeating it a fifth time.
/// </para>
/// </summary>
public sealed class JobRankingStrategyRegistry
{
    private readonly Dictionary<string, IJobRankingStrategy> _strategies;

    public JobRankingStrategyRegistry(IEnumerable<IJobRankingStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);

        _strategies = new Dictionary<string, IJobRankingStrategy>(StringComparer.OrdinalIgnoreCase);

        // Last registration wins, so a deployment can replace a shipped strategy by registering
        // its own under the same key — the same last-wins rule the search engine uses.
        foreach (var strategy in strategies)
        {
            _strategies[strategy.Key] = strategy;
        }
    }

    /// <summary>Every strategy this deployment offers. For the capabilities endpoint.</summary>
    public IReadOnlyCollection<IJobRankingStrategy> All => _strategies.Values;

    /// <summary>Whether a key resolves.</summary>
    public bool Has(string? key) =>
        !string.IsNullOrWhiteSpace(key) && _strategies.ContainsKey(key);

    /// <summary>
    /// Reorders a page of postings.
    /// <para>
    /// Returns the input unchanged when the key resolves to nothing, when there is fewer than
    /// one posting to order, or when the strategy throws. A browse page that ranked badly is
    /// worth far more than one that failed.
    /// </para>
    /// <para>
    /// The sort is stable on ties by publication instant, so two equally scored postings do not
    /// swap places between one page and the next — the same class of bug cursor pagination
    /// exists to prevent.
    /// </para>
    /// </summary>
    public IReadOnlyList<JobPosting> Rank(
        string? key, IReadOnlyList<JobPosting> postings, JobRankingContext context)
    {
        ArgumentNullException.ThrowIfNull(postings);
        ArgumentNullException.ThrowIfNull(context);

        if (postings.Count < 2
            || string.IsNullOrWhiteSpace(key)
            || !_strategies.TryGetValue(key, out var strategy))
        {
            return postings;
        }

        try
        {
            return [.. postings
                .Select(posting => (Posting: posting, Score: strategy.Score(posting, context)))
                .OrderByDescending(entry => entry.Score)
                .ThenByDescending(entry => entry.Posting.PublishedAt ?? entry.Posting.CreatedAt)
                .Select(entry => entry.Posting)];
        }
#pragma warning disable CA1031 // A failing strategy must not fail the page. See the summary.
        catch (Exception)
        {
            return postings;
        }
#pragma warning restore CA1031
    }
}
