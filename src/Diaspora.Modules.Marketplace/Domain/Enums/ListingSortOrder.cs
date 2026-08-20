namespace Diaspora.Modules.Marketplace.Domain.Enums;

/// <summary>
/// How a result set is ordered.
/// <para>
/// The shipped EF engine implements every value but <see cref="Relevance"/> and
/// <see cref="Nearest"/> honestly, and degrades those two to <see cref="Newest"/> — it has no
/// score and no geometry, and pretending otherwise would rank results by a number that means
/// nothing. A real engine behind <c>IMarketplaceSearchEngine</c> implements both properly and
/// nothing above the port changes.
/// </para>
/// </summary>
public enum ListingSortOrder
{
    /// <summary>Whatever the engine thinks best. Degrades to newest here.</summary>
    Relevance = 0,

    /// <summary>Most recently published first. The default browse.</summary>
    Newest = 1,

    PriceLowToHigh = 2,

    PriceHighToLow = 3,

    /// <summary>Most viewed. The brief's "popular".</summary>
    MostViewed = 4,

    /// <summary>Promoted listings first, then newest. Never buries a better match.</summary>
    Featured = 5,

    /// <summary>Closest first. Degrades to newest here.</summary>
    Nearest = 6,

    /// <summary>Expiring soonest. Listings that never expire sort last.</summary>
    EndingSoon = 7
}
