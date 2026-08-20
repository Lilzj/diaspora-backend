namespace Diaspora.Modules.Guides.Domain.Enums;

/// <summary>
/// How a discovery page is ordered. Trending is a ranking-strategy seam; the rest translate
/// directly to indexed orderings the repository can promise.
/// </summary>
public enum GuideSortOrder
{
    Newest = 0,
    MostHelpful = 1,
    RecentlyUpdated = 2,
    Popular = 3,
    Trending = 4
}
