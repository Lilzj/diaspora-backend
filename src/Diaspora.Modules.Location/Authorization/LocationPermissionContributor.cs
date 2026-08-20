using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Location.Authorization;

/// <summary>
/// Declares the permissions the Location module owns.
/// <para>
/// Note how low the tier bands sit. Almost everything here opens at
/// <see cref="AccessTier.Limited"/>, and two of them survive restriction. That is not
/// laxity: a member who has not finished onboarding still has to name the country they live
/// in, and a suspended member still needs to see where they are in order to understand a
/// screen. Geography is context, not capability — the permissions that gate what a member can
/// <em>do</em> somewhere belong to the vertical that owns the doing.
/// </para>
/// </summary>
public sealed class LocationPermissionContributor : IPermissionContributor
{
    public string Module => LocationModuleMetadata.Name;

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        yield return new PermissionDefinition(
            LocationPermissions.ReadCatalog, Module,
            "Read the geographic catalogue: countries, regions, cities and neighbourhoods.")
        {
            MinimumTier = AccessTier.Onboarding,

            // The registration flow asks a member to choose their country of origin and
            // residence before they have any tier at all. A catalogue they cannot read is an
            // onboarding flow that cannot complete.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            LocationPermissions.Search, Module,
            "Search for a place, and resolve coordinates to one.")
        {
            MinimumTier = AccessTier.Onboarding,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            LocationPermissions.Hyperlocal, Module,
            "Run proximity queries: nearby, radius, bounding box, polygon and distance.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            LocationPermissions.ReadProfile, Module,
            "Read your own location profile, saved locations and preferences.")
        {
            MinimumTier = AccessTier.Limited,

            // A restricted member should still see where the platform thinks they are — that
            // is the screen that explains why their results look the way they do.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            LocationPermissions.ManageProfile, Module,
            "Change your own location, saved places and geographic preferences.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            LocationPermissions.ReadExperience, Module,
            "Read your own country experience and expertise.")
        {
            MinimumTier = AccessTier.Limited,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            LocationPermissions.ManageExperience, Module,
            "Declare or amend where you have lived and what you know about it.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            LocationPermissions.ReadPublic, Module,
            "Read another member's public country experience.")
        {
            MinimumTier = AccessTier.Limited
        };

        // The one staff permission. Editing the catalogue changes what every member on the
        // platform can find, so it carries a role as well as a tier.
        yield return new PermissionDefinition(
            LocationPermissions.ManageCatalog, Module,
            "Create or amend entries in the geographic catalogue.")
        {
            MinimumTier = AccessTier.Verified,
            Roles = new HashSet<string> { "Admin", "SuperAdmin" }
        };
    }
}
