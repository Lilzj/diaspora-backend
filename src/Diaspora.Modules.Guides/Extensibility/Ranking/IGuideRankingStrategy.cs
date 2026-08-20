using Diaspora.Modules.Guides.Domain.Guides;

namespace Diaspora.Modules.Guides.Extensibility.Ranking;

/// <summary>
/// How a page of discovery results is ordered.
/// <para>
/// <b>The seam that lets a search or recommendation module take over ranking without B8
/// changing.</b> B8 ships four strategies that are simple, explicable and defensible; a later
/// module registers a better one by shipping a class, and a deployment configures its default
/// by key.
/// </para>
/// <para>
/// <b>Ranking, not filtering.</b> A strategy receives candidates the repository already
/// selected and returns them ordered. It cannot add a guide that did not match and cannot
/// remove one that did — removal is a visibility decision and belongs to the query, where it
/// can be audited. In-memory over one page, deliberately: a page is at most fifty rows, and
/// fifty rows of arithmetic is not where a knowledge base spends its time.
/// </para>
/// </summary>
public interface IGuideRankingStrategy
{
    /// <summary>
    /// The key a caller names. Unique across the platform; a duplicate fails startup rather
    /// than letting two modules silently overwrite each other.
    /// </summary>
    string Key { get; }

    /// <summary>The module contributing.</summary>
    string Module { get; }

    /// <summary>Orders one page of candidates. Same set out as in, in a different order.</summary>
    IReadOnlyList<Guide> Rank(IReadOnlyList<Guide> candidates, GuideRankingContext context);
}
