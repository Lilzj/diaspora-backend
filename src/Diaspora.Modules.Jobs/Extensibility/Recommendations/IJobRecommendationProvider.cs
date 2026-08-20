using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Jobs.Extensibility.Recommendations;

/// <summary>
/// Suggests jobs to a candidate.
/// <para>
/// <b>The placeholder abstraction the brief asks for, and it is deliberately a placeholder.</b>
/// AI candidate matching and a recommendation engine are explicitly out of scope for B9. What
/// ships behind this port is a heuristic that filters on what the candidate already told the
/// platform — their skills, their preferred countries, whether they need sponsorship — and
/// nothing more. It does not learn, score behaviour, or model anybody.
/// </para>
/// <para>
/// <b>The port exists now so that the module that does model this later is an adapter rather
/// than a refactor.</b> It returns identifiers with a reason string, never postings: the read
/// path hydrates from the write model, so a recommender working from a stale snapshot can
/// suggest a filled job but cannot render a wrong one — and a candidate is always told why a
/// job was suggested, which is a property worth freezing into the contract before anything
/// sophisticated implements it.
/// </para>
/// </summary>
public interface IJobRecommendationProvider
{
    /// <summary>The provider's name, for diagnostics and the capabilities endpoint.</summary>
    string Name { get; }

    /// <summary>
    /// Jobs worth showing this member, best first.
    /// <para>
    /// Returning nothing is always valid: a candidate with no profile, a cold start, or a
    /// provider that is unavailable should produce an empty list rather than filler, because
    /// irrelevant recommendations teach a member to ignore the surface entirely.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<JobRecommendation>> RecommendAsync(
        UserId userId,
        CandidateProfileId? candidateId,
        int limit,
        CancellationToken cancellationToken = default);
}
