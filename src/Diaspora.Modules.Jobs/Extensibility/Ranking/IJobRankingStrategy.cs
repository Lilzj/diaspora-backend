using Diaspora.Modules.Jobs.Domain.Postings;

namespace Diaspora.Modules.Jobs.Extensibility.Ranking;

/// <summary>
/// Orders a set of postings for a particular surface.
/// <para>
/// <b>A seam rather than a sort, because ranking a job board is a product decision that will
/// change.</b> Whether a verified employer outranks a better-paid role, whether a visa-sponsored
/// posting is lifted for a candidate who needs one, how hard recency decays — none of those are
/// facts, and hard-coding them into a repository would make every future answer a schema change.
/// </para>
/// <para>
/// A strategy is chosen by <see cref="Key"/>, so a surface asks for the ranking it wants by name
/// and a deployment can register a different implementation under the same key. Strategies rank
/// an already-filtered, already-paged set: they reorder what the repository returned, and they
/// never reach the database.
/// </para>
/// </summary>
public interface IJobRankingStrategy
{
    /// <summary>The name a surface asks for. See <c>JobRankingKeys</c> for the shipped set.</summary>
    string Key { get; }

    /// <summary>Human-readable, for the capabilities endpoint and the documentation.</summary>
    string Description { get; }

    /// <summary>
    /// Scores one posting in a context. Higher sorts first. A strategy that cannot form an
    /// opinion returns zero for everything, which leaves the repository's order intact.
    /// </summary>
    double Score(JobPosting posting, JobRankingContext context);
}
