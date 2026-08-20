using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Marketplace.Authorization;

/// <summary>
/// Declares the policies the module's endpoints reference.
/// <para>
/// Every policy names exactly one permission from <see cref="MarketplacePermissions"/>. The kernel
/// validates at startup that each named permission exists, so a typo fails the boot rather than
/// silently denying a route forever.
/// </para>
/// <para>
/// <b>A policy is a necessary condition, never a sufficient one, for anything listing-scoped.</b>
/// <c>marketplace.listing.publish</c> says the caller may publish listings somewhere; whether they
/// may publish <em>this</em> listing is the aggregate's answer, and every such handler asks it.
/// Reading the policy list as the authorisation model would understate it by half.
/// </para>
/// </summary>
public sealed class MarketplacePolicyContributor : IPolicyContributor
{
    public string Module => MarketplaceModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        // Seller
        yield return Policy(MarketplacePolicies.SellerCreate, MarketplacePermissions.SellerCreate);
        yield return Policy(MarketplacePolicies.SellerManage, MarketplacePermissions.SellerManage);
        yield return Policy(
            MarketplacePolicies.SellerManageAny, MarketplacePermissions.SellerManageAny);
        yield return Policy(
            MarketplacePolicies.SellerSubmitVerification,
            MarketplacePermissions.SellerSubmitVerification);

        // Listing authoring and lifecycle
        yield return Policy(
            MarketplacePolicies.ListingCreate, MarketplacePermissions.ListingCreate);
        yield return Policy(
            MarketplacePolicies.ListingEditOwn, MarketplacePermissions.ListingEditOwn);
        yield return Policy(
            MarketplacePolicies.ListingEditAny, MarketplacePermissions.ListingEditAny);
        yield return Policy(
            MarketplacePolicies.ListingDeleteOwn, MarketplacePermissions.ListingDeleteOwn);
        yield return Policy(
            MarketplacePolicies.ListingPublish, MarketplacePermissions.ListingPublish);
        yield return Policy(
            MarketplacePolicies.ListingArchive, MarketplacePermissions.ListingArchive);
        yield return Policy(
            MarketplacePolicies.ListingRestore, MarketplacePermissions.ListingRestore);
        yield return Policy(
            MarketplacePolicies.ListingMarkSold, MarketplacePermissions.ListingMarkSold);
        yield return Policy(
            MarketplacePolicies.ListingFeature, MarketplacePermissions.ListingFeature);
        yield return Policy(
            MarketplacePolicies.ListingSubmitVerification,
            MarketplacePermissions.ListingSubmitVerification);

        // Buying
        yield return Policy(MarketplacePolicies.OfferMake, MarketplacePermissions.OfferMake);
        yield return Policy(MarketplacePolicies.OfferRespond, MarketplacePermissions.OfferRespond);
        yield return Policy(
            MarketplacePolicies.ReservationCreate, MarketplacePermissions.ReservationCreate);
        yield return Policy(
            MarketplacePolicies.ReservationManage, MarketplacePermissions.ReservationManage);
        yield return Policy(MarketplacePolicies.SavedManage, MarketplacePermissions.SavedManage);

        // Shared
        yield return Policy(MarketplacePolicies.Report, MarketplacePermissions.Report);
        yield return Policy(
            MarketplacePolicies.ViewAnalytics, MarketplacePermissions.ViewAnalytics);

        // Staff
        yield return Policy(MarketplacePolicies.Moderate, MarketplacePermissions.Moderate);
        yield return Policy(
            MarketplacePolicies.ManageCategories, MarketplacePermissions.ManageCategories);
        yield return Policy(
            MarketplacePolicies.ManagePromotions, MarketplacePermissions.ManagePromotions);
    }

    private static PolicyDefinition Policy(string name, string permission) =>
        PolicyDefinition.ForPermission(name, MarketplaceModuleMetadata.Name, permission);
}
