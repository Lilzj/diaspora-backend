using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Marketplace.Authorization;

/// <summary>
/// Declares the permissions the Marketplace module owns.
/// <para>
/// <b>One product judgement runs through the whole band: looking is Limited, transacting is
/// Verified.</b> Browsing, saving and reporting are private or protective acts that cost nobody
/// anything and are exactly what a new member should be doing. Listing an item, making an offer,
/// reserving stock and running a storefront each put money, goods or a real identity in front of a
/// stranger — which is precisely the progression B2.0.1 gates on proven identity.
/// </para>
/// <para>
/// <b>Listing-scoped authority is not here at all.</b> These permissions answer "may this person
/// sell"; whether they may edit <em>this</em> listing is an ownership question only the aggregate
/// can answer, and no permission catalogue can express it. Both checks run: the policy gates the
/// endpoint, and the handler asks the aggregate.
/// </para>
/// <para>
/// <b>The restriction exemptions are where this module's values are most visible.</b> A restricted
/// member may still take their own listing down, answer a live negotiation, cancel a reservation
/// and report a fraud. Each of those, blocked, would leave somebody else waiting on a person the
/// platform has silenced — which is the restriction working against the very people it exists to
/// protect.
/// </para>
/// <para>
/// The kernel constructs this type before the container exists, so it has a public parameterless
/// constructor and no dependencies.
/// </para>
/// </summary>
public sealed class MarketplacePermissionContributor : IPermissionContributor
{
    private static readonly HashSet<string> StaffRoles =
        new(StringComparer.Ordinal) { "Moderator", "Admin", "SuperAdmin" };

    private static readonly HashSet<string> AdminRoles =
        new(StringComparer.Ordinal) { "Admin", "SuperAdmin" };

    public string Module => MarketplaceModuleMetadata.Name;

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        foreach (var permission in SellerPermissions())
        {
            yield return permission;
        }

        foreach (var permission in ListingPermissions())
        {
            yield return permission;
        }

        foreach (var permission in BuyingPermissions())
        {
            yield return permission;
        }

        foreach (var permission in SharedAndStaffPermissions())
        {
            yield return permission;
        }
    }

    private static IEnumerable<PermissionDefinition> SellerPermissions()
    {
        yield return new PermissionDefinition(
            MarketplacePermissions.SellerCreate, MarketplaceModuleMetadata.Name,
            "Set up a seller profile.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            MarketplacePermissions.SellerManage, MarketplaceModuleMetadata.Name,
            "Manage your own seller profile.")
        {
            MinimumTier = AccessTier.Verified,

            // Your own storefront stays editable under restriction — including closing it, which
            // is the setting a restricted seller most needs to be able to reach.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            MarketplacePermissions.SellerManageAny, MarketplaceModuleMetadata.Name,
            "Manage any seller profile.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = StaffRoles
        };

        yield return new PermissionDefinition(
            MarketplacePermissions.SellerSubmitVerification, MarketplaceModuleMetadata.Name,
            "Submit a seller profile for verification.")
        {
            MinimumTier = AccessTier.Verified
        };
    }

    private static IEnumerable<PermissionDefinition> ListingPermissions()
    {
        // The key that pairs with the gate the host declared provisionally from B2.
        yield return new PermissionDefinition(
            MarketplacePermissions.ListingCreate, MarketplaceModuleMetadata.Name,
            "List an item for sale.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            MarketplacePermissions.ListingEditOwn, MarketplaceModuleMetadata.Name,
            "Edit your own listing.")
        {
            MinimumTier = AccessTier.Verified,

            // Correcting a live listing is never the thing to withhold: a wrong price or a wrong
            // collection address wastes buyers' journeys, not the restricted seller's.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            MarketplacePermissions.ListingEditAny, MarketplaceModuleMetadata.Name,
            "Edit any listing.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = StaffRoles
        };

        yield return new PermissionDefinition(
            MarketplacePermissions.ListingDeleteOwn, MarketplaceModuleMetadata.Name,
            "Delete your own listing.")
        {
            MinimumTier = AccessTier.Verified,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            MarketplacePermissions.ListingPublish, MarketplaceModuleMetadata.Name,
            "Publish a listing.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            MarketplacePermissions.ListingArchive, MarketplaceModuleMetadata.Name,
            "Take a listing down.")
        {
            MinimumTier = AccessTier.Verified,

            // Taking down an item that has sold or broken must never be blocked. Leaving it up is
            // what wastes buyers' time and earns the seller a complaint they cannot answer.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            MarketplacePermissions.ListingRestore, MarketplaceModuleMetadata.Name,
            "Restore an archived or deleted listing.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            MarketplacePermissions.ListingMarkSold, MarketplaceModuleMetadata.Name,
            "Record that a listing sold.")
        {
            MinimumTier = AccessTier.Verified,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            MarketplacePermissions.ListingFeature, MarketplaceModuleMetadata.Name,
            "Feature or unfeature any listing.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminRoles
        };

        yield return new PermissionDefinition(
            MarketplacePermissions.ListingSubmitVerification, MarketplaceModuleMetadata.Name,
            "Submit a listing for verification.")
        {
            MinimumTier = AccessTier.Verified
        };
    }

    private static IEnumerable<PermissionDefinition> BuyingPermissions()
    {
        yield return new PermissionDefinition(
            MarketplacePermissions.OfferMake, MarketplaceModuleMetadata.Name,
            "Make an offer on a listing.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            MarketplacePermissions.OfferRespond, MarketplaceModuleMetadata.Name,
            "Answer an offer: counter, accept, decline or withdraw.")
        {
            MinimumTier = AccessTier.Limited,

            // Leaving a negotiation must never be blocked. A restriction that trapped somebody in
            // a deal they wanted out of would be the platform working against the person it
            // restricted — and the counterparty is waiting on them either way.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            MarketplacePermissions.ReservationCreate, MarketplaceModuleMetadata.Name,
            "Reserve an item for collection.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            MarketplacePermissions.ReservationManage, MarketplaceModuleMetadata.Name,
            "Acknowledge, complete or cancel a reservation.")
        {
            MinimumTier = AccessTier.Limited,

            // Cancelling must survive a restriction, or the restriction holds somebody else's
            // stock off the market until the window lapses.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            MarketplacePermissions.SavedManage, MarketplaceModuleMetadata.Name,
            "Save and unsave listings.")
        {
            MinimumTier = AccessTier.Limited,

            // Saving is private and costs nobody anything.
            GrantedWhenRestricted = true
        };
    }

    private static IEnumerable<PermissionDefinition> SharedAndStaffPermissions()
    {
        yield return new PermissionDefinition(
            MarketplacePermissions.Report, MarketplaceModuleMetadata.Name,
            "Report a listing or a seller.")
        {
            MinimumTier = AccessTier.Limited,

            // Reporting a scam must survive a restriction, or the restriction silences the person
            // best placed to flag it. On a marketplace that advert is the most damaging content
            // the platform can carry.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            MarketplacePermissions.ViewAnalytics, MarketplaceModuleMetadata.Name,
            "Read the performance figures for your own listings.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            MarketplacePermissions.Moderate, MarketplaceModuleMetadata.Name,
            "Hide, suspend and annotate any listing or seller.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = StaffRoles
        };

        yield return new PermissionDefinition(
            MarketplacePermissions.ManageCategories, MarketplaceModuleMetadata.Name,
            "Manage the commerce category and attribute catalogues.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminRoles
        };

        yield return new PermissionDefinition(
            MarketplacePermissions.ManagePromotions, MarketplaceModuleMetadata.Name,
            "Grant and withdraw listing promotions.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminRoles
        };
    }
}
