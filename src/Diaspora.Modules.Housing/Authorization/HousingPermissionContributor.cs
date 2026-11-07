using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Housing.Authorization;

/// <summary>
/// Declares the permissions the Housing module owns.
/// <para>
/// <b>One product judgement runs through the whole band: looking is Limited, committing is
/// Verified.</b> Browsing, reading a neighbourhood profile and reporting a scam cost nobody
/// anything and are exactly what somebody deciding whether to move should be doing. Listing a
/// property, booking a viewing and reserving a home each put a real identity, a stranger's
/// afternoon or somebody's deposit at stake.
/// </para>
/// <para>
/// <b>Booking a viewing is the sharpest line.</b> It is the act that unlocks an address, and a
/// member who could arrange viewings without proving who they are could map the empty homes in a
/// city. That is not a hypothetical risk; it is the reconnaissance step of a real crime.
/// </para>
/// <para>
/// <b>The restriction exemptions are where this module's values are most visible.</b> A restricted
/// member may still take their own listing down, cancel a viewing, cancel a reservation and report
/// a fraud. Each of those, blocked, would leave somebody else waiting on a person the platform has
/// silenced — a landlord standing outside a door, or a home held off the market for a fortnight —
/// which is the restriction working against the very people it exists to protect.
/// </para>
/// <para>
/// The kernel constructs this type before the container exists, so it has a public parameterless
/// constructor and no dependencies.
/// </para>
/// </summary>
public sealed class HousingPermissionContributor : IPermissionContributor
{
    private static readonly HashSet<string> StaffRoles =
        new(StringComparer.Ordinal) { "Moderator", "Admin", "SuperAdmin" };

    private static readonly HashSet<string> AdminRoles =
        new(StringComparer.Ordinal) { "Admin", "SuperAdmin" };

    public string Module => HousingModuleMetadata.Name;

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        foreach (var permission in LandlordPermissions())
        {
            yield return permission;
        }

        foreach (var permission in ListingPermissions())
        {
            yield return permission;
        }

        foreach (var permission in RelocationPermissions())
        {
            yield return permission;
        }

        foreach (var permission in NeighborhoodPermissions())
        {
            yield return permission;
        }

        foreach (var permission in SharedAndStaffPermissions())
        {
            yield return permission;
        }
    }

    private static IEnumerable<PermissionDefinition> LandlordPermissions()
    {
        yield return new PermissionDefinition(
            HousingPermissions.LandlordCreate, HousingModuleMetadata.Name,
            "Set up a landlord profile.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            HousingPermissions.LandlordManage, HousingModuleMetadata.Name,
            "Manage your own landlord profile.")
        {
            MinimumTier = AccessTier.Verified,

            // Your own profile stays editable under restriction — including closing it, which is
            // the setting a restricted landlord most needs to be able to reach.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            HousingPermissions.LandlordSubmitVerification, HousingModuleMetadata.Name,
            "Submit a landlord profile for verification.")
        {
            MinimumTier = AccessTier.Verified
        };
    }

    private static IEnumerable<PermissionDefinition> ListingPermissions()
    {
        yield return new PermissionDefinition(
            HousingPermissions.ListingCreate, HousingModuleMetadata.Name,
            "List a property.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            HousingPermissions.ListingEditOwn, HousingModuleMetadata.Name,
            "Edit your own listing.")
        {
            MinimumTier = AccessTier.Verified,

            // Correcting a live listing is never the thing to withhold: a wrong rent or a wrong
            // available-from date wastes members' journeys, not the restricted landlord's.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            HousingPermissions.ListingDeleteOwn, HousingModuleMetadata.Name,
            "Delete your own listing.")
        {
            MinimumTier = AccessTier.Verified,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            HousingPermissions.ListingPublish, HousingModuleMetadata.Name,
            "Publish a listing.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            HousingPermissions.ListingArchive, HousingModuleMetadata.Name,
            "Take a property off the market.")
        {
            MinimumTier = AccessTier.Verified,

            // Withdrawing a property that has been let must never be blocked. Leaving it up wastes
            // every enquiry that follows and earns the landlord complaints they cannot answer.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            HousingPermissions.ListingRestore, HousingModuleMetadata.Name,
            "Restore an archived or deleted listing.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            HousingPermissions.ListingMarkOccupied, HousingModuleMetadata.Name,
            "Record that somebody has moved in.")
        {
            MinimumTier = AccessTier.Verified,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            HousingPermissions.ListingFeature, HousingModuleMetadata.Name,
            "Feature or unfeature any listing.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminRoles
        };

        yield return new PermissionDefinition(
            HousingPermissions.ListingSubmitVerification, HousingModuleMetadata.Name,
            "Submit a property for verification.")
        {
            MinimumTier = AccessTier.Verified
        };
    }

    private static IEnumerable<PermissionDefinition> RelocationPermissions()
    {
        yield return new PermissionDefinition(
            HousingPermissions.ViewingSchedule, HousingModuleMetadata.Name,
            "Ask to view a property.")
        {
            // The gate that guards addresses. See the class summary.
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            HousingPermissions.ViewingManage, HousingModuleMetadata.Name,
            "Confirm, complete or cancel a viewing.")
        {
            MinimumTier = AccessTier.Limited,

            // Cancelling must survive a restriction, or somebody stands outside a door waiting for
            // a person the platform has silenced.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            HousingPermissions.ReservationCreate, HousingModuleMetadata.Name,
            "Reserve a property.")
        {
            MinimumTier = AccessTier.Verified
        };

        // The real gate on a review is the tenancy the handler checks for, not this tier — a
        // member who moved into a place has already cleared Verified to reserve it.
        yield return new PermissionDefinition(
            HousingPermissions.ReviewWrite, HousingModuleMetadata.Name,
            "Review a landlord whose property you moved into.")
        {
            MinimumTier = AccessTier.Verified
        };

        // A restricted landlord keeps the right of reply. Answering a review is the one thing a
        // landlord under restriction most needs, and silencing it would let a restriction decide
        // how a public dispute reads.
        yield return new PermissionDefinition(
            HousingPermissions.ReviewRespond, HousingModuleMetadata.Name,
            "Reply publicly to a review of your listings.")
        {
            MinimumTier = AccessTier.Limited,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            HousingPermissions.ReservationManage, HousingModuleMetadata.Name,
            "Accept, decline, complete or cancel a reservation.")
        {
            MinimumTier = AccessTier.Limited,

            // Cancelling must survive a restriction, or the restriction holds somebody else's home
            // off the market until the hold lapses.
            GrantedWhenRestricted = true
        };
    }

    private static IEnumerable<PermissionDefinition> NeighborhoodPermissions()
    {
        yield return new PermissionDefinition(
            HousingPermissions.NeighborhoodManage, HousingModuleMetadata.Name,
            "Write and revise neighbourhood profiles.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = StaffRoles
        };

        yield return new PermissionDefinition(
            HousingPermissions.NeighborhoodApprove, HousingModuleMetadata.Name,
            "Approve a neighbourhood profile for publication.")
        {
            // A narrower set than writing, deliberately: publishing prose about the places members
            // live under the platform's name is a different act from drafting it.
            MinimumTier = AccessTier.Limited,
            Roles = AdminRoles
        };

        yield return new PermissionDefinition(
            HousingPermissions.InsightSubmit, HousingModuleMetadata.Name,
            "Share what you know about a neighbourhood.")
        {
            // Limited, because the thing that makes neighbourhood insights worth having is that
            // ordinary residents write them — and everything they write is moderated anyway.
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            HousingPermissions.InsightEndorse, HousingModuleMetadata.Name,
            "Agree with a neighbourhood insight.")
        {
            MinimumTier = AccessTier.Limited,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            HousingPermissions.InsightModerate, HousingModuleMetadata.Name,
            "Approve, reject or retire neighbourhood insights.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = StaffRoles
        };
    }

    private static IEnumerable<PermissionDefinition> SharedAndStaffPermissions()
    {
        yield return new PermissionDefinition(
            HousingPermissions.Report, HousingModuleMetadata.Name,
            "Report a listing or a landlord.")
        {
            MinimumTier = AccessTier.Limited,

            // Reporting a rental scam must survive a restriction, or the restriction silences the
            // person best placed to flag the single most damaging content this platform can carry.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            HousingPermissions.ViewAnalytics, HousingModuleMetadata.Name,
            "Read the performance figures for your own properties.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            HousingPermissions.Moderate, HousingModuleMetadata.Name,
            "Hide, suspend and annotate any listing or landlord.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = StaffRoles
        };

        yield return new PermissionDefinition(
            HousingPermissions.ManageCatalogue, HousingModuleMetadata.Name,
            "Manage the accommodation-type and amenity catalogues.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminRoles
        };
    }
}
