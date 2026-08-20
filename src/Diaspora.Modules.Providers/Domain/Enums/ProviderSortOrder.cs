namespace Diaspora.Modules.Providers.Domain.Enums;

/// <summary>
/// How a discovery result set is ordered.
/// <para>
/// The first three are the client's filter sheet exactly — its options are labelled
/// "Recommended", "Distance" and "Rating" over the values <c>default</c>, <c>distance</c> and
/// <c>rating</c>. <see cref="Recommended"/> is the client's <c>default</c> renamed to say what
/// it means.
/// </para>
/// <para>
/// <see cref="Recommended"/> is not a fourth sort implemented here — it delegates to whichever
/// <c>IProviderRankingStrategy</c> the deployment has registered, which is the seam that lets
/// B15 and B17 replace the ordering without B7 changing. The default strategy is documented in
/// <c>DefaultProviderRanking</c> and is deliberately simple and explicable.
/// </para>
/// </summary>
public enum ProviderSortOrder
{
    /// <summary>The registered ranking strategy decides. The client's <c>default</c>.</summary>
    Recommended = 0,

    /// <summary>Nearest first. Requires a spatial scope; falls back to <see cref="Recommended"/>.</summary>
    Distance = 1,

    /// <summary>Highest average rating first, then by review count so one 5★ does not top a hundred.</summary>
    Rating = 2,

    /// <summary>Most recently published first.</summary>
    Newest = 3,

    /// <summary>Most reviewed first. What "established" means without claiming quality.</summary>
    MostReviewed = 4,

    /// <summary>Alphabetical. Boring, predictable, and the only stable order for a directory.</summary>
    Name = 5
}
