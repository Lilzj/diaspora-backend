namespace Diaspora.Modules.Marketplace.Extensibility.Ranking;

/// <summary>
/// Decides how a set of listings is ordered once the engine has found them.
/// <para>
/// <b>Separate from the search engine, because relevance and ranking are different jobs.</b> The
/// engine answers "which listings match"; a ranking answers "which of these should this member see
/// first", and that second question is where a marketplace expresses its values. Splitting them
/// means a deployment can change its values without changing its index.
/// </para>
/// <para>
/// <b>The constraint every shipped strategy obeys: a promotion may lift a listing and may never
/// bury a better match.</b> That is a product commitment, not a technical one, and it is stated
/// here because this is the interface somebody would implement to break it. A marketplace where a
/// paid slot outranks a closer, cheaper, better-matched listing has stopped answering the buyer's
/// question.
/// </para>
/// <para>
/// Found by assembly scan. A module that ships a strategy makes it available by existing.
/// </para>
/// </summary>
public interface IListingRankingStrategy
{
    /// <summary>
    /// The key a request names — <c>recency</c>, <c>trusted</c>, <c>balanced</c>. Unique across
    /// the platform; a duplicate is last-registration-wins, which the registry logs.
    /// </summary>
    string Key { get; }

    /// <summary>What a settings screen calls it.</summary>
    string DisplayName { get; }

    /// <summary>
    /// The score for one listing in one context. Higher sorts earlier. Bounded to a sane range by
    /// convention rather than by contract, because a strategy that returns wild numbers only
    /// misorders its own results.
    /// </summary>
    double Score(ListingRankingContext context);
}
