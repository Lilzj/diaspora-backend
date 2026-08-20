using Diaspora.Modules.Providers.Domain.Providers;

namespace Diaspora.Modules.Providers.Extensibility.Ranking;

/// <summary>
/// How a page of discovery results is ordered when the member asked for "Recommended".
/// <para>
/// <b>The seam that lets B15 and B17 take over ranking without B7 changing.</b> The brief is
/// explicit that B7 must not implement search or AI, and equally explicit that the client's
/// "Recommended" sort has to do something today. A strategy is the reconciliation: B7 ships one
/// that is simple, explicable and defensible, and a search or recommendation module registers a
/// better one by shipping a class.
/// </para>
/// <para>
/// <b>Ranking, not filtering.</b> A strategy receives candidates the repository already selected
/// and returns them ordered. It cannot add a provider that did not match, and it cannot remove
/// one that did — removal is a visibility decision and belongs to the query, where it can be
/// audited. A strategy that could silently drop results would make "why is my business not
/// showing" unanswerable.
/// </para>
/// <para>
/// In-memory over one page, deliberately. A strategy that pushed its scoring into SQL would have
/// to be written per database provider and would be untestable without one; a page is at most
/// fifty rows, and fifty rows of arithmetic is not where a directory spends its time.
/// </para>
/// </summary>
public interface IProviderRankingStrategy
{
    /// <summary>
    /// The key a caller names, and the key a deployment configures as its default.
    /// Unique across the platform; a duplicate fails startup rather than letting two modules
    /// silently overwrite each other.
    /// </summary>
    string Key { get; }

    /// <summary>The module contributing.</summary>
    string Module { get; }

    /// <summary>Orders one page of candidates. Same set out as in, in a different order.</summary>
    IReadOnlyList<Provider> Rank(IReadOnlyList<Provider> candidates, ProviderRankingContext context);
}
