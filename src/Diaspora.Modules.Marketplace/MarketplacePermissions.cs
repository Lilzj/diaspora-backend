namespace Diaspora.Modules.Marketplace;

/// <summary>
/// The permissions the Marketplace module owns.
/// <para>
/// Colon-separated keys with the module prefix, per the platform convention (policies use dots,
/// see <see cref="MarketplacePolicies"/>). Tier floors live with the definitions in
/// <c>Authorization/MarketplacePermissionContributor</c>, because the module is the only thing
/// that knows what each capability is worth.
/// </para>
/// <para>
/// The split that matters: <b>looking is Limited, transacting is Verified.</b> Browsing listings,
/// saving them and reporting a fraud are private or protective acts that cost nobody anything.
/// Listing an item, making an offer, reserving stock and running a storefront each put money,
/// goods or a real identity in front of a stranger, which is exactly the progression B2.0.1
/// gates on proven identity.
/// </para>
/// <para>
/// <b>Seller-scoped authority is not here at all.</b> These permissions answer "may this person
/// sell"; whether they may edit <em>this</em> listing is an ownership question only the listing
/// and its seller profile can answer, and no permission catalogue can express it. Both checks
/// run: the policy gates the endpoint, and the handler asks the aggregate.
/// </para>
/// <para>
/// Staff-only capabilities are role-scoped, not tier-scoped, per the access-tier semantics: a
/// tier measures progression, a role grants privilege.
/// </para>
/// </summary>
public static class MarketplacePermissions
{
    // ── Seller ─────────────────────────────────────────────────

    public const string SellerCreate = "marketplace:seller:create";
    public const string SellerManage = "marketplace:seller:manage";
    public const string SellerSubmitVerification = "marketplace:seller:verify:submit";

    // ── Listing authoring and lifecycle ─────────────────────────────────────────────────

    public const string ListingCreate = "marketplace:listings:create";
    public const string ListingEditOwn = "marketplace:listing:edit:own";
    public const string ListingDeleteOwn = "marketplace:listing:delete:own";
    public const string ListingPublish = "marketplace:listing:publish";
    public const string ListingArchive = "marketplace:listing:archive";
    public const string ListingRestore = "marketplace:listing:restore";
    public const string ListingMarkSold = "marketplace:listing:mark-sold";
    public const string ListingFeature = "marketplace:listing:feature";
    public const string ListingSubmitVerification = "marketplace:listing:verify:submit";

    // ── Buying ─────────────────────────────────────────────────

    public const string OfferMake = "marketplace:offer:make";
    public const string OfferRespond = "marketplace:offer:respond";
    public const string ReservationCreate = "marketplace:reservation:create";
    public const string ReservationManage = "marketplace:reservation:manage";
    public const string SavedManage = "marketplace:saved:manage";
    public const string ReviewWrite = "marketplace:review:write";
    public const string ReviewRespond = "marketplace:review:respond";

    // ── Shared ─────────────────────────────────────────────────

    public const string Report = "marketplace:report";
    public const string ViewAnalytics = "marketplace:analytics:view";

    // ── Staff ─────────────────────────────────────────────────

    public const string Moderate = "marketplace:moderate";
    public const string ManageCategories = "marketplace:categories:manage";
    public const string ManagePromotions = "marketplace:promotions:manage";
}
