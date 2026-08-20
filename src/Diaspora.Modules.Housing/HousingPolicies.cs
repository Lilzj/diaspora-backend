namespace Diaspora.Modules.Housing;

/// <summary>
/// The authorisation policy names the module's endpoints reference.
/// <para>
/// Dot-separated, mirroring <see cref="HousingPermissions"/> one-for-one. The kernel validates at
/// startup that each policy names a permission that exists, so a typo fails the boot rather than
/// silently denying a route forever.
/// </para>
/// <para>
/// <b>A policy is a necessary condition, never a sufficient one, for anything listing-scoped.</b>
/// <c>housing.listing.edit.own</c> says the caller may edit properties somewhere; whether they may
/// edit <em>this</em> one is the aggregate's answer, and every such handler asks it.
/// </para>
/// </summary>
public static class HousingPolicies
{
    // ── Landlord ───────────────────────────────────────────────

    public const string LandlordCreate = "housing.landlord.create";
    public const string LandlordManage = "housing.landlord.manage";
    public const string LandlordManageAny = "housing.landlord.manage.any";
    public const string LandlordSubmitVerification = "housing.landlord.verify.submit";

    // ── Listing authoring and lifecycle ─────────────────────────────────────────────────

    public const string ListingCreate = "housing.listing.create";
    public const string ListingEditOwn = "housing.listing.edit.own";
    public const string ListingEditAny = "housing.listing.edit.any";
    public const string ListingDeleteOwn = "housing.listing.delete.own";
    public const string ListingPublish = "housing.listing.publish";
    public const string ListingArchive = "housing.listing.archive";
    public const string ListingRestore = "housing.listing.restore";
    public const string ListingMarkOccupied = "housing.listing.mark-occupied";
    public const string ListingFeature = "housing.listing.feature";
    public const string ListingSubmitVerification = "housing.listing.verify.submit";

    // ── Relocation workflow ─────────────────────────────────────────────────

    public const string ViewingSchedule = "housing.viewing.schedule";
    public const string ViewingManage = "housing.viewing.manage";
    public const string ReservationCreate = "housing.reservation.create";
    public const string ReservationManage = "housing.reservation.manage";

    // ── Neighbourhoods ─────────────────────────────────────────────────

    public const string NeighborhoodManage = "housing.neighborhood.manage";
    public const string NeighborhoodApprove = "housing.neighborhood.approve";
    public const string InsightSubmit = "housing.insight.submit";
    public const string InsightEndorse = "housing.insight.endorse";
    public const string InsightModerate = "housing.insight.moderate";

    // ── Shared ─────────────────────────────────────────────────

    public const string Report = "housing.report";
    public const string ViewAnalytics = "housing.analytics.view";

    // ── Staff ─────────────────────────────────────────────────

    public const string Moderate = "housing.moderate";
    public const string ManageCatalogue = "housing.catalogue.manage";
}
