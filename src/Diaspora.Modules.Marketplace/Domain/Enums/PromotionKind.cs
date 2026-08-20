namespace Diaspora.Modules.Marketplace.Domain.Enums;

/// <summary>
/// What a promotion buys a listing.
/// <para>
/// <b>Placement, and never rank suppression.</b> Each of these lifts one listing; none of them
/// pushes another down. B9's ranking notes make the reason explicit and it is sharper here,
/// because this is the module where money changes hands: a marketplace where a paid slot can bury
/// a better-matched free listing is one where the search results stop being an answer to the
/// buyer's question.
/// </para>
/// <para>
/// Nothing here charges anybody. Payments are out of scope for B10; a promotion is granted by
/// staff or by a future billing module, and this records that it was.
/// </para>
/// </summary>
public enum PromotionKind
{
    /// <summary>Carried in the featured rail, and sorted first under the Featured order.</summary>
    Featured = 0,

    /// <summary>Republished to the top of "newest" without changing its publication date.</summary>
    Bump = 1,

    /// <summary>Given the hero slot on the marketplace home surface.</summary>
    Spotlight = 2,

    /// <summary>Pinned to the top of its own category page only.</summary>
    CategoryHighlight = 3
}
