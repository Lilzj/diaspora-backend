namespace Diaspora.Modules.Marketplace;

/// <summary>
/// The authorisation policy names the module's endpoints reference.
/// <para>
/// Dot-separated, mirroring <see cref="MarketplacePermissions"/> one-for-one. The kernel
/// validates at startup that each policy names a permission that exists, so a typo fails the boot
/// rather than silently denying a route forever.
/// </para>
/// <para>
/// <b>A policy is a necessary condition, never a sufficient one, for anything listing-scoped.</b>
/// <c>marketplace.listing.edit.own</c> says the caller may edit listings somewhere; whether they
/// may edit <em>this</em> listing is the aggregate's answer, and every such handler asks it.
/// </para>
/// </summary>
public static class MarketplacePolicies
{
    // ── Seller ─────────────────────────────────────────────────

    public const string SellerCreate = "marketplace.seller.create";
    public const string SellerManage = "marketplace.seller.manage";
    public const string SellerSubmitVerification = "marketplace.seller.verify.submit";

    // ── Listing authoring and lifecycle ─────────────────────────────────────────────────

    public const string ListingCreate = "marketplace.listing.create";
    public const string ListingEditOwn = "marketplace.listing.edit.own";
    public const string ListingDeleteOwn = "marketplace.listing.delete.own";
    public const string ListingPublish = "marketplace.listing.publish";
    public const string ListingArchive = "marketplace.listing.archive";
    public const string ListingRestore = "marketplace.listing.restore";
    public const string ListingMarkSold = "marketplace.listing.mark-sold";
    public const string ListingFeature = "marketplace.listing.feature";
    public const string ListingSubmitVerification = "marketplace.listing.verify.submit";

    // ── Buying ─────────────────────────────────────────────────

    public const string OfferMake = "marketplace.offer.make";
    public const string OfferRespond = "marketplace.offer.respond";
    public const string ReservationCreate = "marketplace.reservation.create";
    public const string ReservationManage = "marketplace.reservation.manage";
    public const string SavedManage = "marketplace.saved.manage";
    public const string ReviewWrite = "marketplace.review.write";
    public const string ReviewRespond = "marketplace.review.respond";

    // ── Shared ─────────────────────────────────────────────────

    public const string Report = "marketplace.report";
    public const string ViewAnalytics = "marketplace.analytics.view";

    // ── Staff ─────────────────────────────────────────────────

    public const string Moderate = "marketplace.moderate";
    public const string ManageCategories = "marketplace.categories.manage";
    public const string ManagePromotions = "marketplace.promotions.manage";
}
