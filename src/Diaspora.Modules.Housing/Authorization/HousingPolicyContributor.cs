using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Housing.Authorization;

/// <summary>
/// Declares the policies the module's endpoints reference.
/// <para>
/// Every policy names exactly one permission from <see cref="HousingPermissions"/>. The kernel
/// validates at startup that each named permission exists, so a typo fails the boot rather than
/// silently denying a route forever.
/// </para>
/// <para>
/// <b>A policy is a necessary condition, never a sufficient one, for anything listing-scoped.</b>
/// <c>housing.listing.publish</c> says the caller may publish properties somewhere; whether they
/// may publish <em>this</em> one is the aggregate's answer, and every such handler asks it. Reading
/// the policy list as the authorisation model would understate it by half.
/// </para>
/// </summary>
public sealed class HousingPolicyContributor : IPolicyContributor
{
    public string Module => HousingModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        // Landlord
        yield return Policy(HousingPolicies.LandlordCreate, HousingPermissions.LandlordCreate);
        yield return Policy(HousingPolicies.LandlordManage, HousingPermissions.LandlordManage);
        yield return Policy(
            HousingPolicies.LandlordManageAny, HousingPermissions.LandlordManageAny);
        yield return Policy(
            HousingPolicies.LandlordSubmitVerification,
            HousingPermissions.LandlordSubmitVerification);

        // Listing authoring and lifecycle
        yield return Policy(HousingPolicies.ListingCreate, HousingPermissions.ListingCreate);
        yield return Policy(HousingPolicies.ListingEditOwn, HousingPermissions.ListingEditOwn);
        yield return Policy(HousingPolicies.ListingEditAny, HousingPermissions.ListingEditAny);
        yield return Policy(
            HousingPolicies.ListingDeleteOwn, HousingPermissions.ListingDeleteOwn);
        yield return Policy(HousingPolicies.ListingPublish, HousingPermissions.ListingPublish);
        yield return Policy(HousingPolicies.ListingArchive, HousingPermissions.ListingArchive);
        yield return Policy(HousingPolicies.ListingRestore, HousingPermissions.ListingRestore);
        yield return Policy(
            HousingPolicies.ListingMarkOccupied, HousingPermissions.ListingMarkOccupied);
        yield return Policy(HousingPolicies.ListingFeature, HousingPermissions.ListingFeature);
        yield return Policy(
            HousingPolicies.ListingSubmitVerification,
            HousingPermissions.ListingSubmitVerification);

        // Relocation workflow
        yield return Policy(
            HousingPolicies.ViewingSchedule, HousingPermissions.ViewingSchedule);
        yield return Policy(HousingPolicies.ViewingManage, HousingPermissions.ViewingManage);
        yield return Policy(
            HousingPolicies.ReservationCreate, HousingPermissions.ReservationCreate);
        yield return Policy(
            HousingPolicies.ReservationManage, HousingPermissions.ReservationManage);
        yield return Policy(HousingPolicies.ReviewWrite, HousingPermissions.ReviewWrite);
        yield return Policy(HousingPolicies.ReviewRespond, HousingPermissions.ReviewRespond);

        // Neighbourhoods
        yield return Policy(
            HousingPolicies.NeighborhoodManage, HousingPermissions.NeighborhoodManage);
        yield return Policy(
            HousingPolicies.NeighborhoodApprove, HousingPermissions.NeighborhoodApprove);
        yield return Policy(HousingPolicies.InsightSubmit, HousingPermissions.InsightSubmit);
        yield return Policy(HousingPolicies.InsightEndorse, HousingPermissions.InsightEndorse);
        yield return Policy(
            HousingPolicies.InsightModerate, HousingPermissions.InsightModerate);

        // Shared
        yield return Policy(HousingPolicies.Report, HousingPermissions.Report);
        yield return Policy(HousingPolicies.ViewAnalytics, HousingPermissions.ViewAnalytics);

        // Staff
        yield return Policy(HousingPolicies.Moderate, HousingPermissions.Moderate);
        yield return Policy(
            HousingPolicies.ManageCatalogue, HousingPermissions.ManageCatalogue);
    }

    private static PolicyDefinition Policy(string name, string permission) =>
        PolicyDefinition.ForPermission(name, HousingModuleMetadata.Name, permission);
}
