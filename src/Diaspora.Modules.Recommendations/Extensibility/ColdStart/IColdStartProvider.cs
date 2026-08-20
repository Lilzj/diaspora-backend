using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Extensibility.Personalization;

namespace Diaspora.Modules.Recommendations.Extensibility.ColdStart;

/// <summary>
/// What to show somebody the platform knows nothing about.
/// <para>
/// <b>Its own seam because cold start is not degraded personalization — it is a different
/// product.</b> Every ranking strategy in this module answers "how well does this match what we know
/// about you", and for a member who registered ninety seconds ago the honest answer is zero for
/// everything. Running the normal pass and taking whatever floats to the top produces a page ranked
/// by nothing but popularity, in whatever country happens to have the most content.
/// </para>
/// <para>
/// <b>The one thing the platform does know is where they are, and the ladder is built on it.</b>
/// Country first, then the guides new arrivals actually read, then housing, jobs, organizations,
/// events, providers and marketplace — the order in which somebody who has just moved needs things,
/// which is also the order the brief sets out. It is a product opinion, and it is exactly the kind of
/// opinion that should be replaceable.
/// </para>
/// <para>
/// <b>Cold start must never require history, and the interface is what says so.</b> It is given the
/// context and returns candidates; there is no signal parameter it could come to depend on.
/// </para>
/// </summary>
public interface IColdStartProvider
{
    /// <summary>The key this provider is identified by in diagnostics and analytics.</summary>
    string Key { get; }

    /// <summary>
    /// Assembles a first page.
    /// <para>
    /// Returns candidates in the order it wants them shown. The engine still runs policies over the
    /// result and still explains every item — a cold-start page is not exempt from either, and the
    /// reason a new member is shown something is exactly the reason worth telling them.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<RecommendationCandidate>> BuildAsync(
        PersonalizationContext context, int limit, CancellationToken cancellationToken = default);
}
