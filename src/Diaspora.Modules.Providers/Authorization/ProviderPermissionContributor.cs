using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Providers.Authorization;

/// <summary>
/// Declares the permissions the Providers module owns.
/// <para>
/// The tier band encodes one product judgement repeatedly: <b>browsing is how a newcomer finds
/// the community, offering is a public commitment, and administering the catalogue is staff.</b>
/// Reading opens at <see cref="AccessTier.Onboarding"/>, because a member who cannot see a
/// single restaurant has no reason to finish verifying — the same loop-with-no-entrance
/// reasoning B5 applied to profile photographs and B6 to its feed.
/// </para>
/// <para>
/// <b>Registering opens at <see cref="AccessTier.Verified"/></b>, and it is the highest band in
/// the module. An unverified account publishing a money-transfer business into a diaspora
/// community is the single worst thing this module can be used for, and the people it would hurt
/// are the ones with the least recourse.
/// </para>
/// <para>
/// <b>Reviewing sits a rung lower, at <see cref="AccessTier.Limited"/>.</b> A member's first
/// honest review is often written before their verification clears, and a directory whose
/// ratings only come from fully verified members has ratings from almost nobody.
/// </para>
/// </summary>
public sealed class ProviderPermissionContributor : IPermissionContributor
{
    public string Module => ProvidersModuleMetadata.Name;

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        yield return new PermissionDefinition(
            ProviderPermissions.Read, Module, "Browse and read providers.")
        {
            MinimumTier = AccessTier.Limited,

            // A restricted member's screens still render. A directory that 403s looks like an
            // outage, and a member who cannot look anything up has no reason to come back.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            ProviderPermissions.Register, Module, "Register a provider.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            ProviderPermissions.Manage, Module, "Edit a provider you help run.")
        {
            MinimumTier = AccessTier.Verified,

            // A provider must be able to correct its own listing during a restriction. Taking
            // down a wrong phone number is never the thing to withhold.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            ProviderPermissions.ManageStatus, Module, "Publish, pause or close a provider.")
        {
            MinimumTier = AccessTier.Verified,

            // Closing your own business is a decision nobody else should be able to block.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            ProviderPermissions.ManageMembers, Module, "Manage the people attached to a provider.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            ProviderPermissions.ManageServices, Module, "Manage the services a provider offers.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            ProviderPermissions.ManageAvailability, Module, "Set operating hours and availability.")
        {
            MinimumTier = AccessTier.Verified,

            // Marking yourself closed is the same kind of act as closing: always available.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            ProviderPermissions.ManageMedia, Module, "Attach photographs and documents.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            ProviderPermissions.SubmitVerification, Module, "Submit a provider for verification.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            ProviderPermissions.Review, Module, "Review a provider.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            ProviderPermissions.Recommend, Module, "Recommend or endorse a provider.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            ProviderPermissions.Bookmark, Module, "Save a provider.")
        {
            MinimumTier = AccessTier.Limited,

            // Saving is private and costs nobody anything. A new member gathering places to come
            // back to is exactly the behaviour onboarding is trying to produce.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            ProviderPermissions.ReadStatistics, Module, "Read a provider's statistics.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            ProviderPermissions.AdministerCatalog, Module, "Manage the service category catalogue.")
        {
            MinimumTier = AccessTier.Verified,
            Roles = new HashSet<string> { "Admin", "SuperAdmin" }
        };

        yield return new PermissionDefinition(
            ProviderPermissions.Administer, Module, "Feature, suspend and restore any provider.")
        {
            MinimumTier = AccessTier.Verified,
            Roles = new HashSet<string> { "Moderator", "Admin", "SuperAdmin" }
        };
    }
}
