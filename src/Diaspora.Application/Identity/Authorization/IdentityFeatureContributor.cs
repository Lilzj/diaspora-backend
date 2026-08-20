using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Application.Identity.Authorization;

/// <summary>
/// Declares the feature gates the Identity module owns.
/// </summary>
public sealed class IdentityFeatureContributor : IFeatureContributor
{
    public string Module => IdentityModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            IdentityFeatures.ManageOwnProfile, Module, "Edit your profile.");

        yield return new FeatureDefinition(
            IdentityFeatures.ManageOwnSessions, Module, "See and revoke your signed-in devices.");

        yield return new FeatureDefinition(
            IdentityFeatures.ManageOwnDevices, Module, "Register and manage your devices.");

        yield return new FeatureDefinition(
            IdentityFeatures.AccessVerificationCenter, Module, "Reach the Verification Center.");

        yield return new FeatureDefinition(
            IdentityFeatures.SubmitVerification, Module, "Ask to be considered for verification.")
        {
            // Mirrors the permission of the same name: open at limited access, closed once
            // the request is with the Trust Engine.
            MinimumTier = AccessTier.Limited,
            MaximumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            IdentityFeatures.ModerateContent, Module, "Moderate community content.")
        {
            StaffOnly = true
        };

        yield return new FeatureDefinition(
            IdentityFeatures.ManageUsers, Module, "Administer member accounts.")
        {
            StaffOnly = true
        };
    }
}
